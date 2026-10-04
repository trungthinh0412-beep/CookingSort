using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SimpleShadow : MonoBehaviour
{
    [Header("Shadow References")]
    [Tooltip("The shadow renderer (must be a child of this object)")]
    public SpriteRenderer shadowRenderer;

    [Header("Base Settings")]
    [Tooltip("The target world scale the shadow will animate towards when enabled.")]
    public Vector3 baseScale = new Vector3(1.2f, 0.5f, 1f);
    
    [Tooltip("The target alpha the shadow will have when fully enabled.")]
    [Range(0f, 1f)] public float baseAlpha = 0.5f;

    [Header("Animation Speeds")]
    [Tooltip("How fast the shadow fades in/out and scales X/Z.")]
    public float transitionSpeed = 15f;
    
    [Tooltip("How fast the shadow follows the source object's Y scale (set lower than transitionSpeed for a lagging effect).")]
    public float yScaleFollowSpeed = 8f;

    [Tooltip("How fast the shadow moves between its default position and a custom target position.")]
    public float positionTransitionSpeed = 15f;

    // Internal State
    private SpriteRenderer sourceRenderer;
    private bool isShadowEnabled = false;
    private float currentAlpha = 0f;
    private Vector3 currentWorldScale;

    // Position State
    private Vector3 baseLocalPosition;
    private Vector3 currentWorldPosition;
    private bool hasCustomPosition = false;
    private Vector3 customTargetPosition;

    void Start()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();
        
        if (shadowRenderer != null)
        {
            currentWorldScale = transform.lossyScale;
            currentAlpha = 0f;
            baseLocalPosition = shadowRenderer.transform.localPosition;
            currentWorldPosition = shadowRenderer.transform.position;
            
            Color c = shadowRenderer.color;
            c.a = 0f;
            shadowRenderer.color = c;
            
            shadowRenderer.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Enables or disables the shadow, triggering the smooth scale and alpha transitions.
    /// </summary>
    public void SetEnabled(bool enable)
    {
        if (shadowRenderer == null) return;

        // If turning ON from an OFF state, snap the starting values to the source's current state
        if (enable && !isShadowEnabled)
        {
            shadowRenderer.gameObject.SetActive(true);
            currentWorldScale = transform.lossyScale;
            currentAlpha = 0f;
            
            // Snap position to parent if not currently moving to a custom position
            if (!hasCustomPosition)
            {
                currentWorldPosition = transform.TransformPoint(baseLocalPosition);
            }
        }

        isShadowEnabled = enable;
    }

    /// <summary>
    /// Commands the shadow to move toward and stick to a specific world position.
    /// </summary>
    public void SetCustomPosition(Vector3 worldPosition)
    {
        hasCustomPosition = true;
        customTargetPosition = worldPosition;
    }

    /// <summary>
    /// Releases the shadow from the custom position, allowing it to return to the source object.
    /// </summary>
    public void ClearCustomPosition()
    {
        hasCustomPosition = false;
    }

    void LateUpdate()
    {
        if (shadowRenderer == null) return;

        Vector3 sourceScale = transform.lossyScale;

        // --- 1. ALPHA ANIMATION ---
        float targetAlpha = isShadowEnabled ? baseAlpha : 0f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, transitionSpeed * Time.deltaTime);

        if (!isShadowEnabled && currentAlpha <= 0.001f)
        {
            if (shadowRenderer.gameObject.activeSelf)
            {
                shadowRenderer.gameObject.SetActive(false);
            }
            return; 
        }

        // --- 2. POSITION ANIMATION ---
        Vector3 targetWorldPosition = hasCustomPosition 
            ? customTargetPosition 
            : transform.TransformPoint(baseLocalPosition);

        currentWorldPosition = Vector3.Lerp(currentWorldPosition, targetWorldPosition, positionTransitionSpeed * Time.deltaTime);
        shadowRenderer.transform.position = currentWorldPosition;

        // --- 3. SCALE ANIMATION (World Space) ---
        Vector3 targetWorldScale;
        
        if (isShadowEnabled)
        {
            // Stick to baseScale for X and Z. Smoothly follow source's Y scale.
            targetWorldScale = new Vector3(
                baseScale.x,
                baseScale.y * sourceScale.y,
                baseScale.z
            );
        }
        else
        {
            // Animate back into the source object's exact shape before disappearing
            targetWorldScale = sourceScale;
        }

        currentWorldScale.x = Mathf.Lerp(currentWorldScale.x, targetWorldScale.x, transitionSpeed * Time.deltaTime);
        currentWorldScale.z = Mathf.Lerp(currentWorldScale.z, targetWorldScale.z, transitionSpeed * Time.deltaTime);
        
        float currentYSpeed = isShadowEnabled ? yScaleFollowSpeed : transitionSpeed;
        currentWorldScale.y = Mathf.Lerp(currentWorldScale.y, targetWorldScale.y, currentYSpeed * Time.deltaTime);


        // --- 4. APPLY TO LOCAL TRANSFORM ---
        float safeX = Mathf.Abs(sourceScale.x) > 0.001f ? currentWorldScale.x / sourceScale.x : 0f;
        float safeY = Mathf.Abs(sourceScale.y) > 0.001f ? currentWorldScale.y / sourceScale.y : 0f;
        float safeZ = Mathf.Abs(sourceScale.z) > 0.001f ? currentWorldScale.z / sourceScale.z : 0f;

        shadowRenderer.transform.localScale = new Vector3(safeX, safeY, safeZ);

        // --- 5. SYNC VISUALS ---
        Color c = shadowRenderer.color;
        c.a = currentAlpha;
        shadowRenderer.color = c;
        
        shadowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
    }
}