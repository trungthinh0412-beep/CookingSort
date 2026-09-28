using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BuildRevealShadow : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly Color ShadowTint = new Color(0f, 0f, 0f, .24f);

    private Transform target;
    private float shadowOffset;

    public static BuildRevealShadow Create(Transform target, Bounds targetBounds)
    {
        if (target == null || target.parent == null)
            return null;

        GameObject shadowObject = new GameObject(
            $"{target.gameObject.name}_BuildShadow",
            typeof(RectTransform)
        );
        shadowObject.transform.SetParent(target.parent, false);
        shadowObject.layer = target.gameObject.layer;

        BuildRevealShadow shadow = shadowObject.AddComponent<BuildRevealShadow>();
        shadow.Configure(target, targetBounds);
        return shadow;
    }

    public void DestroyShadow()
    {
        if (gameObject != null)
            Destroy(gameObject);
    }

    private void Configure(Transform targetTransform, Bounds targetBounds)
    {
        target = targetTransform;
        shadowOffset = Mathf.Max(.02f, targetBounds.size.y * .075f);

        if (!CreateVisualShadow(target, transform))
        {
            Destroy(gameObject);
            return;
        }

        transform.position = target.position + Vector3.down * shadowOffset;
        transform.rotation = target.rotation;
        transform.localScale = target.localScale;
        transform.SetSiblingIndex(target.GetSiblingIndex());
    }

    private bool CreateVisualShadow(Transform source, Transform shadowRoot)
    {
        bool hasVisual = false;

        RectTransform sourceRect = source as RectTransform;
        RectTransform shadowRect = shadowRoot as RectTransform;

        if (sourceRect != null && shadowRect != null)
            CopyRectTransform(sourceRect, shadowRect);
        else
            CopyTransform(source, shadowRoot);

        Image image = source.GetComponent<Image>();

        if (image != null)
        {
            Image shadowImage = shadowRoot.gameObject.AddComponent<Image>();
            shadowImage.sprite = image.sprite;
            shadowImage.type = image.type;
            shadowImage.preserveAspect = image.preserveAspect;
            shadowImage.fillCenter = image.fillCenter;
            shadowImage.fillMethod = image.fillMethod;
            shadowImage.fillAmount = image.fillAmount;
            shadowImage.fillClockwise = image.fillClockwise;
            shadowImage.fillOrigin = image.fillOrigin;
            shadowImage.useSpriteMesh = image.useSpriteMesh;
            shadowImage.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
            shadowImage.color = MultiplyAlpha(ShadowTint, image.color.a);
            shadowImage.raycastTarget = false;
            shadowImage.maskable = image.maskable;
            hasVisual = true;
        }

        RawImage rawImage = source.GetComponent<RawImage>();

        if (rawImage != null)
        {
            RawImage shadowRawImage = shadowRoot.gameObject.AddComponent<RawImage>();
            shadowRawImage.texture = rawImage.texture;
            shadowRawImage.uvRect = rawImage.uvRect;
            shadowRawImage.color = MultiplyAlpha(ShadowTint, rawImage.color.a);
            shadowRawImage.raycastTarget = false;
            shadowRawImage.maskable = rawImage.maskable;
            hasVisual = true;
        }

        SpriteRenderer spriteRenderer = source.GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            SpriteRenderer shadowRenderer =
                shadowRoot.gameObject.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = spriteRenderer.sprite;
            shadowRenderer.flipX = spriteRenderer.flipX;
            shadowRenderer.flipY = spriteRenderer.flipY;
            shadowRenderer.drawMode = spriteRenderer.drawMode;
            shadowRenderer.size = spriteRenderer.size;
            shadowRenderer.maskInteraction = spriteRenderer.maskInteraction;
            shadowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            shadowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            shadowRenderer.color = MultiplyAlpha(ShadowTint, spriteRenderer.color.a);
            shadowRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
            hasVisual = true;
        }

        MeshFilter meshFilter = source.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = source.GetComponent<MeshRenderer>();

        if (meshFilter != null && meshFilter.sharedMesh != null && meshRenderer != null)
        {
            MeshFilter shadowMeshFilter =
                shadowRoot.gameObject.AddComponent<MeshFilter>();
            MeshRenderer shadowMeshRenderer =
                shadowRoot.gameObject.AddComponent<MeshRenderer>();
            shadowMeshFilter.sharedMesh = meshFilter.sharedMesh;
            shadowMeshRenderer.sharedMaterials = meshRenderer.sharedMaterials;
            shadowMeshRenderer.sortingLayerID = meshRenderer.sortingLayerID;
            shadowMeshRenderer.sortingOrder = meshRenderer.sortingOrder - 1;

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorId, ShadowTint);
            propertyBlock.SetColor(BaseColorId, ShadowTint);
            shadowMeshRenderer.SetPropertyBlock(propertyBlock);
            hasVisual = true;
        }

        for (int i = 0; i < source.childCount; i++)
        {
            Transform sourceChild = source.GetChild(i);
            GameObject childObject = new GameObject(
                sourceChild.gameObject.name,
                sourceChild is RectTransform
                    ? typeof(RectTransform)
                    : typeof(Transform)
            );
            childObject.transform.SetParent(shadowRoot, false);
            childObject.layer = sourceChild.gameObject.layer;

            bool childHasVisual = CreateVisualShadow(
                sourceChild,
                childObject.transform
            );

            if (!childHasVisual)
                Destroy(childObject);
            else
                hasVisual = true;
        }

        return hasVisual;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = target.position + Vector3.down * shadowOffset;
        transform.rotation = target.rotation;
        transform.localScale = target.localScale;
    }

    private static void CopyTransform(Transform source, Transform destination)
    {
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static void CopyRectTransform(
        RectTransform source,
        RectTransform destination
    )
    {
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.pivot = source.pivot;
        destination.offsetMin = source.offsetMin;
        destination.offsetMax = source.offsetMax;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static Color MultiplyAlpha(Color color, float sourceAlpha)
    {
        color.a *= Mathf.Clamp01(sourceAlpha);
        return color;
    }
}
