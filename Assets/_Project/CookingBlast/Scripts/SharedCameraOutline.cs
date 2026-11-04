using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CookingSort2D
{
    /// <summary>One shared silhouette/distance field per Built-in camera.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    [AddComponentMenu("CookingBlast/Shared Camera Outline")]
    public sealed class SharedCameraOutline : MonoBehaviour
    {
        [Tooltip("Material using World/Custom/Screen Space Outline. Widths are pixels at 1080 screen height.")]
        [SerializeField] private Material outlineStyle;
        [SerializeField, Range(0.25f, 1f)] private float resolutionScale = 0.5f;
        [SerializeField, Range(0.01f, 0.95f)] private float alphaThreshold = 0.1f;
        [Tooltip("Invert mask Y when compositing. Enable for the D3D screen render path; toggle if the outline is mirrored vertically.")]
        [SerializeField] private bool flipMaskY = true;
        [SerializeField] private LayerMask targetLayers = ~0;
        [Header("Shader references (auto assigned on Reset; keep for builds)")]
        [SerializeField] private Shader maskShader;
        [SerializeField] private Shader distanceShader;
        [SerializeField] private Shader compositeShader;
        [Header("Diagnostics (updated when the camera renders)")]
        [SerializeField] private string renderStatus;
        [SerializeField] private int renderedSubmeshes;

        private Camera owner;
        private CommandBuffer commands;
        private Material maskMaterial, seedMaterial, compositeMaterial;
        private readonly List<Material> jumpMaterials = new List<Material>();
        private readonly List<Material> sourceMaterials = new List<Material>();
        private MaterialPropertyBlock properties;
        private RenderTexture mask, fieldA, fieldB;
        private readonly Plane[] planes = new Plane[6];
        private sealed class OutlineDraw
        {
            public GameObject gameObject;
            public Mesh mesh;
            public MeshRenderer renderer;
            public int id;
        }
        private readonly Dictionary<CameraOutlineTarget, OutlineDraw> outlineDraws = new Dictionary<CameraOutlineTarget, OutlineDraw>();
        private readonly List<CameraOutlineTarget> staleTargets = new List<CameraOutlineTarget>();
        private readonly List<CameraOutlineTarget> sortedTargets = new List<CameraOutlineTarget>();
        private readonly Vector3[] corners = new Vector3[4];
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private const CameraEvent DrawEvent = CameraEvent.BeforeForwardAlpha;

        private void Reset() { FindShaders(); }
        private void OnValidate() { FindShaders(); }
        private void FindShaders()
        {
            if (maskShader == null) maskShader = Shader.Find("Hidden/CookingBlast/Camera Outline Mask");
            if (distanceShader == null) distanceShader = Shader.Find("Hidden/CookingBlast/Spine Silhouette Distance");
            if (compositeShader == null) compositeShader = Shader.Find("Hidden/CookingBlast/Camera Outline Composite");
        }

        private void OnEnable()
        {
            owner = GetComponent<Camera>();
            commands = new CommandBuffer { name = "Shared skeleton camera outline" };
            owner.AddCommandBuffer(DrawEvent, commands);
            properties = new MaterialPropertyBlock();
            FindShaders();
        }

        private void OnPreCull()
        {
            if (commands == null) return;
            commands.Clear();
            foreach (OutlineDraw draw in outlineDraws.Values)
                if (draw.renderer != null) draw.renderer.enabled = false;
            RemoveStaleTargets();
            renderedSubmeshes = 0;
            if (GraphicsSettings.currentRenderPipeline != null)
            { renderStatus = "Requires Built-in render pipeline"; return; }
            if (outlineStyle == null || !outlineStyle.HasProperty("_FillWidth"))
            { renderStatus = "Assign a World/Custom/Screen Space Outline material"; return; }
            if (CameraOutlineTarget.Active.Count == 0)
            { renderStatus = "No enabled Camera Outline Target"; return; }
            if (!EnsureResources())
            { renderStatus = "Missing/unsupported shader or ARGBHalf render texture"; return; }

            float width = Mathf.Max(0, outlineStyle.GetFloat("_FillWidth"));
            float glow = Mathf.Max(0, outlineStyle.GetFloat("_GlowWidth"));
            float softness = outlineStyle.HasProperty("_Softness") ? outlineStyle.GetFloat("_Softness") : 1;
            float strength = outlineStyle.HasProperty("_GlowStrength") ? outlineStyle.GetFloat("_GlowStrength") : 1;
            if (width <= 0 && (glow <= 0 || strength <= 0))
            { renderStatus = "Outline and glow widths are zero"; return; }
            float referencePixelsPerTexel = 1080f / mask.height;
            maskMaterial.SetFloat("_AlphaThreshold", alphaThreshold);
            GeometryUtility.CalculateFrustumPlanes(owner, planes);
            commands.SetRenderTarget(mask);
            commands.SetViewport(new Rect(0, 0, mask.width, mask.height));
            commands.ClearRenderTarget(false, true, Color.clear);
            commands.SetViewProjectionMatrices(owner.worldToCameraMatrix, GL.GetGPUProjectionMatrix(owner.projectionMatrix, true));
            int draws = 0;
            sortedTargets.Clear();
            foreach (CameraOutlineTarget target in CameraOutlineTarget.Active)
                if (target != null && target.Renderer != null) sortedTargets.Add(target);
            sortedTargets.Sort(CompareTargets);
            foreach (CameraOutlineTarget target in sortedTargets)
            {
                if (target == null || target.Renderer == null || target.Filter == null) continue;
                MeshRenderer renderer = target.Renderer;
                int layer = 1 << target.gameObject.layer;
                Mesh mesh = target.Filter.sharedMesh;
                if (!renderer.enabled || renderer.forceRenderingOff || mesh == null ||
                    (layer & targetLayers.value & owner.cullingMask) == 0 ||
                    !GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;
                renderer.GetSharedMaterials(sourceMaterials);
                OutlineDraw outlineDraw = GetOutlineDraw(target);
                UpdateOutlineQuad(target, outlineDraw, width + (strength > 0 ? glow : 0) + softness + 3);
                for (int submesh = 0; submesh < mesh.subMeshCount && submesh < sourceMaterials.Count; submesh++)
                {
                    Material source = sourceMaterials[submesh];
                    if (source == null || source.mainTexture == null) continue;
                    // Preserve atlas overrides supplied through property blocks.
                    renderer.GetPropertyBlock(properties, submesh);
                    if (properties.GetTexture(MainTex) == null) properties.SetTexture(MainTex, source.mainTexture);
                    properties.SetFloat("_OutlineId", outlineDraw.id);
                    commands.DrawMesh(mesh, renderer.localToWorldMatrix, maskMaterial, submesh, 0, properties);
                    draws++;
                }
            }
            commands.SetViewProjectionMatrices(owner.worldToCameraMatrix, GL.GetGPUProjectionMatrix(owner.projectionMatrix, owner.targetTexture != null));
            commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            commands.SetViewport(owner.pixelRect);
            renderedSubmeshes = draws;
            if (draws == 0)
            { renderStatus = "No target mesh visible to this camera"; return; }
            renderStatus = "Rendering shared outline";

            seedMaterial.SetFloat("_AlphaThreshold", alphaThreshold);
            commands.Blit(mask, fieldA, seedMaterial, 0);
            RenderTexture read = fieldA, write = fieldB;
            int reach = Mathf.Clamp(Mathf.CeilToInt((width + (strength > 0 ? glow : 0) + softness + 2) / referencePixelsPerTexel), 1, Mathf.Max(mask.width, mask.height));
            int passIndex = 0;
            for (int step = Mathf.NextPowerOfTwo(reach); step >= 1; step >>= 1)
                Jump(ref read, ref write, step, passIndex++);
            Jump(ref read, ref write, 1, passIndex);
            compositeMaterial.SetTexture("_MaskTex", mask);
            compositeMaterial.SetColor("_FillColor", outlineStyle.GetColor("_FillColor"));
            compositeMaterial.SetColor("_GlowColor", outlineStyle.GetColor("_GlowColor"));
            compositeMaterial.SetFloat("_FillWidth", width);
            compositeMaterial.SetFloat("_OutlineAlpha", outlineStyle.GetFloat("_OutlineAlpha"));
            compositeMaterial.SetFloat("_GlowWidth", glow);
            compositeMaterial.SetFloat("_GlowStrength", strength);
            compositeMaterial.SetFloat("_Softness", softness);
            compositeMaterial.SetFloat("_ReferencePixelsPerTexel", referencePixelsPerTexel);
            compositeMaterial.SetFloat("_FlipMaskY", flipMaskY ? 1f : 0f);
            // Unity sorts these small quads with the scene's transparent renderers.
            // Only the distance field is shared; it is no longer a final overlay.
            compositeMaterial.SetTexture(MainTex, read);
            commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            commands.SetViewport(owner.pixelRect);
        }

        private int CompareTargets(CameraOutlineTarget a, CameraOutlineTarget b)
        {
            int layer = SortingLayer.GetLayerValueFromID(a.Renderer.sortingLayerID)
                .CompareTo(SortingLayer.GetLayerValueFromID(b.Renderer.sortingLayerID));
            if (layer != 0) return layer;
            int order = a.Renderer.sortingOrder.CompareTo(b.Renderer.sortingOrder);
            if (order != 0) return order;
            return owner.WorldToViewportPoint(b.Renderer.bounds.center).z
                .CompareTo(owner.WorldToViewportPoint(a.Renderer.bounds.center).z);
        }

        private void Jump(ref RenderTexture read, ref RenderTexture write, int step, int index)
        {
            // Separate material per recorded pass: material properties are evaluated
            // at execution time, so reusing one would make every jump step identical.
            while (jumpMaterials.Count <= index) jumpMaterials.Add(NewMaterial(distanceShader));
            Material material = jumpMaterials[index];
            material.SetFloat("_Step", step);
            commands.Blit(read, write, material, 1);
            RenderTexture swap = read; read = write; write = swap;
        }

        private bool EnsureResources()
        {
            if (maskShader == null || distanceShader == null || compositeShader == null ||
                !maskShader.isSupported || !distanceShader.isSupported || !compositeShader.isSupported ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return false;
            if (maskMaterial == null) maskMaterial = NewMaterial(maskShader);
            if (seedMaterial == null) seedMaterial = NewMaterial(distanceShader);
            if (compositeMaterial == null) compositeMaterial = NewMaterial(compositeShader);
            int w = Mathf.Max(16, Mathf.RoundToInt(owner.pixelWidth * resolutionScale));
            int h = Mathf.Max(16, Mathf.RoundToInt(owner.pixelHeight * resolutionScale));
            if (mask == null || mask.width != w || mask.height != h || !mask.IsCreated())
            {
                ReleaseTextures();
                mask = NewTexture(w, h, RenderTextureFormat.ARGBHalf, FilterMode.Point);
                fieldA = NewTexture(w, h, RenderTextureFormat.ARGBHalf, FilterMode.Point);
                fieldB = NewTexture(w, h, RenderTextureFormat.ARGBHalf, FilterMode.Point);
            }
            return true;
        }

        private static Material NewMaterial(Shader shader)
        { return new Material(shader) { hideFlags = HideFlags.HideAndDontSave }; }
        private static RenderTexture NewTexture(int w, int h, RenderTextureFormat format, FilterMode filter)
        {
            var texture = new RenderTexture(w, h, 0, format, RenderTextureReadWrite.Linear)
            { hideFlags = HideFlags.HideAndDontSave, filterMode = filter, wrapMode = TextureWrapMode.Clamp, useMipMap = false, antiAliasing = 1 };
            texture.Create();
            return texture;
        }
        private void ReleaseTextures()
        { Release(ref mask); Release(ref fieldA); Release(ref fieldB); }
        private static void Release(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release(); Dispose(texture); texture = null;
        }
        private static void Dispose(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private OutlineDraw GetOutlineDraw(CameraOutlineTarget target)
        {
            if (outlineDraws.TryGetValue(target, out OutlineDraw draw)) return draw;
            // Half-float exactly represents these integer IDs. Reuse free IDs.
            int id = 1;
            bool used;
            do
            {
                used = false;
                foreach (OutlineDraw existing in outlineDraws.Values)
                    if (existing.id == id) { used = true; id++; break; }
            } while (used);
            var go = new GameObject("Camera Outline (generated)") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(target.transform, false);
            var mesh = new Mesh { name = "Sorted camera outline quad", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            mesh.vertices = new Vector3[4];
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = compositeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var block = new MaterialPropertyBlock();
            block.SetFloat("_OutlineId", id);
            renderer.SetPropertyBlock(block);
            draw = new OutlineDraw { gameObject = go, mesh = mesh, renderer = renderer, id = id };
            outlineDraws.Add(target, draw);
            return draw;
        }

        private void UpdateOutlineQuad(CameraOutlineTarget target, OutlineDraw draw, float padding)
        {
            Bounds bounds = target.Renderer.bounds;
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            for (int n = 0; n < 8; n++)
            {
                Vector3 p = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((n & 1) == 0 ? -1 : 1, (n & 2) == 0 ? -1 : 1, (n & 4) == 0 ? -1 : 1));
                Vector3 v = owner.WorldToViewportPoint(p);
                minX = Mathf.Min(minX, v.x); minY = Mathf.Min(minY, v.y);
                maxX = Mathf.Max(maxX, v.x); maxY = Mathf.Max(maxY, v.y);
            }
            float yPad = padding / 1080f;
            float xPad = yPad * owner.pixelHeight / Mathf.Max(1f, owner.pixelWidth);
            float depth = owner.WorldToViewportPoint(bounds.center).z;
            corners[0] = new Vector3(minX - xPad, minY - yPad, depth);
            corners[1] = new Vector3(maxX + xPad, minY - yPad, depth);
            corners[2] = new Vector3(maxX + xPad, maxY + yPad, depth);
            corners[3] = new Vector3(minX - xPad, maxY + yPad, depth);
            for (int n = 0; n < 4; n++)
                corners[n] = draw.gameObject.transform.InverseTransformPoint(owner.ViewportToWorldPoint(corners[n]));
            draw.mesh.vertices = corners;
            draw.mesh.RecalculateBounds();
            draw.gameObject.layer = target.gameObject.layer;
            draw.renderer.sortingLayerID = target.Renderer.sortingLayerID;
            draw.renderer.sortingOrder = Mathf.Max(-32768, target.Renderer.sortingOrder - 1);
            draw.renderer.enabled = true;
        }

        private void OnPostRender()
        {
            // Prevent generated quads from leaking into a different camera.
            foreach (OutlineDraw draw in outlineDraws.Values)
                if (draw.renderer != null) draw.renderer.enabled = false;
        }

        private void RemoveStaleTargets()
        {
            staleTargets.Clear();
            foreach (var pair in outlineDraws)
                if (pair.Key == null || !CameraOutlineTarget.Active.Contains(pair.Key)) staleTargets.Add(pair.Key);
            foreach (CameraOutlineTarget target in staleTargets)
            {
                OutlineDraw draw = outlineDraws[target];
                Dispose(draw.gameObject); Dispose(draw.mesh);
                outlineDraws.Remove(target);
            }
        }
        private void OnDisable()
        {
            foreach (OutlineDraw draw in outlineDraws.Values)
            { Dispose(draw.gameObject); Dispose(draw.mesh); }
            outlineDraws.Clear();
            if (owner != null && commands != null) owner.RemoveCommandBuffer(DrawEvent, commands);
            commands?.Release(); commands = null;
            ReleaseTextures();
            Dispose(maskMaterial); Dispose(seedMaterial); Dispose(compositeMaterial);
            maskMaterial = seedMaterial = compositeMaterial = null;
            foreach (Material material in jumpMaterials) Dispose(material);
            jumpMaterials.Clear();
        }
    }
}
