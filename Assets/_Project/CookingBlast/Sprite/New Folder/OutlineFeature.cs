using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>URP 17 camera-shared silhouette field for SkeletonOutline and Shader.shader.</summary>
public sealed class OutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class Settings
    {
        [Tooltip("Use 1 for smooth outlines. Lower values reduce GPU cost but can produce stair-stepped edges.")]
        [Range(0.25f, 1f)] public float resolutionScale = 1f;
        [Tooltip("Maximum Outline Width + Glow Width, in pixels at 1080 screen height.")]
        [Range(1f, 128f)] public float maxDistance = 32f;
        [Range(0.01f, 0.95f)] public float alphaThreshold = 0.1f;
        public LayerMask layerMask = ~0;
        public bool showInSceneView = true;
    }

    public Settings settings = new Settings();
    [SerializeField] private Shader maskShader;
    [SerializeField] private Shader distanceShader;
    private OutlinePass pass;

    public override void Create()
    {
        pass?.Dispose();
        if (maskShader == null) maskShader = Shader.Find("Hidden/CookingBlast/URP Outline Mask");
        if (distanceShader == null) distanceShader = Shader.Find("Hidden/CookingBlast/URP Outline Distance");
        if (maskShader != null && distanceShader != null)
            pass = new OutlinePass(settings, maskShader, distanceShader);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || !SystemInfo.IsFormatSupported(GraphicsFormat.R16G16_SFloat, GraphicsFormatUsage.Render)) return;
        Camera camera = renderingData.cameraData.camera;
        if (camera.cameraType == CameraType.Preview || (!settings.showInSceneView && camera.cameraType == CameraType.SceneView)) return;
        // This 2D implementation intentionally uses a non-array screen field.
        if (renderingData.cameraData.cameraTargetDescriptor.dimension != TextureDimension.Tex2D) return;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) { pass?.Dispose(); pass = null; }

    private sealed class OutlinePass : ScriptableRenderPass
    {
        private struct MaskDraw
        {
            public Mesh mesh;
            public Matrix4x4 matrix;
            public Texture atlas;
            public int submesh;
        }
        private sealed class PassData
        {
            public TextureHandle mask, fieldA, fieldB;
            public List<MaskDraw> draws = new List<MaskDraw>();
            public Material maskMaterial, distanceMaterial;
            public Matrix4x4 viewProjection;
            public int width, height, reach, jumpCount;
            public float threshold;
        }
        private readonly Settings settings;
        private readonly Material maskMaterial, distanceMaterial;
        private readonly List<Material> sourceMaterials = new List<Material>();
        private readonly HashSet<Renderer> visited = new HashSet<Renderer>();
        private readonly Plane[] planes = new Plane[6];
        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private RTHandle compatibilityMask, compatibilityA, compatibilityB;
        private readonly List<MaskDraw> compatibilityDraws = new List<MaskDraw>();
        private static readonly int SeedsId = Shader.PropertyToID("_OutlineSeeds");
        private static readonly int SizeId = Shader.PropertyToID("_OutlineSeedsSize");
        private static readonly int StepId = Shader.PropertyToID("_OutlineJumpStep");
        private static readonly int ThresholdId = Shader.PropertyToID("_OutlineAlphaThreshold");
        private static readonly int AtlasId = Shader.PropertyToID("_OutlineAtlas");
        private static readonly int MatrixId = Shader.PropertyToID("_OutlineViewProjection");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly Vector4 ScaleBias = new Vector4(1,1,0,0);

        public OutlinePass(Settings settings, Shader mask, Shader distance)
        {
            this.settings = settings;
            maskMaterial = CoreUtils.CreateEngineMaterial(mask);
            distanceMaterial = CoreUtils.CreateEngineMaterial(distance);
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        private RenderTextureDescriptor Descriptor(RenderTextureDescriptor cameraDescriptor)
        {
            cameraDescriptor.width = Mathf.Max(16, Mathf.RoundToInt(cameraDescriptor.width * settings.resolutionScale));
            cameraDescriptor.height = Mathf.Max(16, Mathf.RoundToInt(cameraDescriptor.height * settings.resolutionScale));
            cameraDescriptor.msaaSamples = 1;
            cameraDescriptor.depthBufferBits = 0;
            cameraDescriptor.depthStencilFormat = GraphicsFormat.None;
            cameraDescriptor.useMipMap = false;
            cameraDescriptor.autoGenerateMips = false;
            cameraDescriptor.enableRandomWrite = false;
            cameraDescriptor.useDynamicScale = false;
            cameraDescriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            return cameraDescriptor;
        }

        private void Gather(Camera camera, List<MaskDraw> draws)
        {
            draws.Clear();
            visited.Clear();
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            foreach (SkeletonOutline outline in SkeletonOutline.ActiveOutlines)
            {
                if (outline == null || !outline.isActiveAndEnabled || outline.OutlineMaterial == null) continue;
                MeshRenderer renderer = outline.SourceRenderer;
                Mesh mesh = outline.SourceMesh;
                if (renderer == null || mesh == null || mesh.vertexCount == 0 || !renderer.enabled || renderer.forceRenderingOff ||
                    !renderer.gameObject.activeInHierarchy || outline.SourceSkeleton == null || !outline.SourceSkeleton.isActiveAndEnabled ||
                    (camera.cullingMask & settings.layerMask.value & (1 << renderer.gameObject.layer)) == 0 ||
                    !GeometryUtility.TestPlanesAABB(planes, renderer.bounds) || !visited.Add(renderer)) continue;
                renderer.GetSharedMaterials(sourceMaterials);
                for (int i=0; i<mesh.subMeshCount && i<sourceMaterials.Count; i++)
                {
                    Material material = sourceMaterials[i];
                    if (material == null) continue;
                    propertyBlock.Clear();
                    renderer.GetPropertyBlock(propertyBlock, i);
                    Texture atlas = propertyBlock.GetTexture(MainTexId);
                    if (atlas == null) atlas = material.mainTexture;
                    if (atlas == null) continue;
                    draws.Add(new MaskDraw { mesh = mesh, matrix = renderer.localToWorldMatrix, atlas = atlas, submesh = i });
                }
            }
        }

        private static int JumpCount(int reach)
        {
            int count = 1; // Extra unit step improves corner accuracy.
            for (int step=Mathf.NextPowerOfTwo(reach); step>=1; step>>=1) count++;
            return count;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            RenderTextureDescriptor descriptor = Descriptor(cameraData.cameraTargetDescriptor);
            TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "Outline Mask", false, FilterMode.Point);
            descriptor.graphicsFormat = GraphicsFormat.R16G16_SFloat;
            TextureHandle fieldA = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "Outline Seeds A", false, FilterMode.Point);
            TextureHandle fieldB = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "Outline Seeds B", false, FilterMode.Point);
            using (var builder = renderGraph.AddUnsafePass<PassData>("Skeleton Outline Field", out var data))
            {
                Gather(cameraData.camera, data.draws);
                data.mask = mask; data.fieldA = fieldA; data.fieldB = fieldB;
                data.maskMaterial = maskMaterial; data.distanceMaterial = distanceMaterial;
                data.width = descriptor.width; data.height = descriptor.height;
                data.threshold = settings.alphaThreshold;
                data.reach = Mathf.Max(1, Mathf.CeilToInt(settings.maxDistance * descriptor.height / 1080f));
                data.jumpCount = JumpCount(data.reach);
                // The mask is always a texture; don't use the deprecated
                // compatibility-only GetGPUProjectionMatrix() RenderGraph stub.
                data.viewProjection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true) * cameraData.GetViewMatrix();
                // These textures are initialized and then read inside this
                // unsafe pass, so there is no incoming read dependency.
                builder.UseTexture(mask, AccessFlags.WriteAll);
                builder.UseTexture(fieldA, AccessFlags.WriteAll);
                builder.UseTexture(fieldB, AccessFlags.WriteAll);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(data.jumpCount % 2 == 0 ? fieldA : fieldB, SeedsId);
                builder.SetRenderFunc(static (PassData d, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    RTHandle maskRT = d.mask, a = d.fieldA, b = d.fieldB;
                    RenderField(cmd, maskRT, a, b, d.draws, d.maskMaterial, d.distanceMaterial,
                        d.viewProjection, d.width, d.height, d.threshold, d.reach);
                });
            }
        }

        private static RTHandle RenderField(CommandBuffer cmd, RTHandle mask, RTHandle a, RTHandle b,
            List<MaskDraw> draws, Material maskMaterial, Material distanceMaterial, Matrix4x4 vp,
            int width, int height, float threshold, int reach)
        {
            CoreUtils.SetRenderTarget(cmd, mask, ClearFlag.Color, Color.clear);
            cmd.SetViewport(new Rect(0,0,width,height));
            cmd.SetGlobalMatrix(MatrixId, vp);
            foreach (MaskDraw draw in draws)
            {
                if (draw.mesh == null || draw.atlas == null) continue;
                cmd.SetGlobalTexture(AtlasId, draw.atlas);
                cmd.DrawMesh(draw.mesh, draw.matrix, maskMaterial, draw.submesh, 0);
            }
            cmd.SetGlobalVector(SizeId, new Vector4(width,height,1f/width,1f/height));
            cmd.SetGlobalFloat(ThresholdId, threshold);
            CoreUtils.SetRenderTarget(cmd, a);
            Blitter.BlitTexture(cmd, mask, ScaleBias, distanceMaterial, 0);
            RTHandle read = a, write = b;
            for (int step=Mathf.NextPowerOfTwo(reach); step>=1; step>>=1)
                Jump(cmd, ref read, ref write, distanceMaterial, step);
            Jump(cmd, ref read, ref write, distanceMaterial, 1);
            return read;
        }

        private static void Jump(CommandBuffer cmd, ref RTHandle read, ref RTHandle write, Material material, int step)
        {
            cmd.SetGlobalFloat(StepId, step);
            CoreUtils.SetRenderTarget(cmd, write);
            Blitter.BlitTexture(cmd, read, ScaleBias, material, 1);
            RTHandle swap = read; read = write; write = swap;
        }

        // Also support URP's Compatibility Mode; RenderGraph remains enabled by default.
        [System.Obsolete("Compatibility path for URP without RenderGraph.")]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = Descriptor(renderingData.cameraData.cameraTargetDescriptor);
            RenderingUtils.ReAllocateHandleIfNeeded(ref compatibilityMask, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name:"Outline Mask");
            descriptor.graphicsFormat = GraphicsFormat.R16G16_SFloat;
            RenderingUtils.ReAllocateHandleIfNeeded(ref compatibilityA, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name:"Outline Seeds A");
            RenderingUtils.ReAllocateHandleIfNeeded(ref compatibilityB, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name:"Outline Seeds B");
            Gather(renderingData.cameraData.camera, compatibilityDraws);
            CommandBuffer cmd = CommandBufferPool.Get("Skeleton Outline Field");
            try
            {
                RTHandle result = RenderField(cmd, compatibilityMask, compatibilityA, compatibilityB, compatibilityDraws,
                    maskMaterial, distanceMaterial, renderingData.cameraData.GetGPUProjectionMatrix() * renderingData.cameraData.GetViewMatrix(),
                    descriptor.width, descriptor.height, settings.alphaThreshold,
                    Mathf.Max(1, Mathf.CeilToInt(settings.maxDistance * descriptor.height / 1080f)));
                cmd.SetGlobalTexture(SeedsId, result);
                ScriptableRenderer renderer = renderingData.cameraData.renderer;
                CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
                cmd.SetViewport(renderingData.cameraData.camera.pixelRect);
                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public void Dispose()
        {
            compatibilityMask?.Release(); compatibilityMask = null;
            compatibilityA?.Release(); compatibilityA = null;
            compatibilityB?.Release(); compatibilityB = null;
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(distanceMaterial);
        }
    }
}
