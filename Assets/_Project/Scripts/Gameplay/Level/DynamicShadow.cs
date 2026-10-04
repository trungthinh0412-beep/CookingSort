using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DynamicShadow : MonoBehaviour
{
    [Header("State")]
    public bool isShadowEnabled = true;

    public bool preventShadowDisable = false;

    [Header("Shadow References")]
    public SpriteRenderer shadowObject;
    
    public SpriteRenderer shadowObject2;

    [Header("Base Shadow Settings")]
    public Vector3 baseShadowScale = Vector3.one;
    public float scaleRate = 1.2f;
    public float baseDownOffset = 0.1f;
    public float scaleSmoothing = 15f;

    [Header("Sync Settings")]
    public bool followSourceScale = true;
    public bool followSourceRotation = false;
    public bool followSourceYRotation = false;

    [Header("Scale Modifiers (When Scale > 1)")]
    public bool enableScaleMultiplier = true;
    public bool enableDistanceMultiplier = true;
    public float excessDistanceMultiplier = 1.0f;
    public float excessScaleMultiplier = 0.5f;

    [Header("Update Down Settings")]
    public float maxUpdateDownOffset = 2.0f;

    [Header("Update Scale Up Settings")]
    public float maxShadowScale = 2.5f;

    [Header("Curve Animation Settings (Standard)")]
    public float curveSagMultiplier = 0.1f;
    public float curveScaleMultiplier = 0.02f;

    [Header("Curve Animation Settings (Vertical Only)")]
    public float verticalCurveSagMultiplier = 0.15f;
    public float verticalCurveScaleMultiplier = 0.03f;

    [Header("Deck Curve Animation Settings (Standard)")]
    public float deckCurveSagMultiplier = 0.1f;
    public float deckCurveScaleMultiplier = 0.02f;

    [Header("Deck Curve Animation Settings (Vertical Only)")]
    public float deckVerticalCurveSagMultiplier = 0.15f;
    public float deckVerticalCurveScaleMultiplier = 0.03f;

    [Header("Merge Flight Curve Animation Settings (Standard)")]
    public float mergeFlightCurveSagMultiplier = 0.1f;
    public float mergeFlightCurveScaleMultiplier = 0.02f;

    [Header("Merge Flight Curve Animation Settings (Vertical Only)")]
    public float mergeFlightVerticalCurveSagMultiplier = 0.15f;
    public float mergeFlightVerticalCurveScaleMultiplier = 0.03f;

    [Header("3D Tilt Illusion Settings")]
    public bool enableTiltIllusion = true;
    public bool invertTiltDirection = false;
    public float tiltHorizontalOffset = 0.01f;
    public float tiltVerticalOffset = 0.0f;
    public float maxHorizontalOffset = 0.3f;
    public float maxVerticalOffset = 0.3f;
    public float tiltScaleMultiplier = 0.02f;
    public float illusionMultiplier = 0.4f;
    public float oppositeTiltMultiplier = 0.5f;

    [Header("Alpha Fading Settings")]
    public float fadeScaleRange = 0.5f;
    public float maxFadeDistance = 5.0f;
    [Range(0f, 1f)] public float minDistanceAlpha = 0.1f;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer activeShadow;
    
    private bool isCurrentlyAnimating;
    private Vector3 currentGroundPosition;
    private float currentCurveDistance;
    private float currentCurveScale;
    private float currentFlightSideOffset;
    private float smoothedScaleExcess;

    private bool isUpdateDownCalledThisFrame;
    private float currentUpdateDownOffset;

    private bool isUpdateScaleUpCalledThisFrame;
    private float currentUpdateScaleBonus;

    private bool useAutoSorting = true;
    private int customSortingOrder = 0;

    void Start()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();
        
        activeShadow = shadowObject;
        if (shadowObject2 != null) shadowObject2.gameObject.SetActive(false);
    }

    public void SwitchShadow(bool useShadow2)
    {
        SpriteRenderer prevShadow = activeShadow;
        activeShadow = useShadow2 ? shadowObject2 : shadowObject;
        
        if (prevShadow != activeShadow && prevShadow != null)
        {
            prevShadow.gameObject.SetActive(false);
            
            if (activeShadow != null)
            {
                activeShadow.transform.position = prevShadow.transform.position;
                activeShadow.transform.localScale = prevShadow.transform.localScale;
                activeShadow.transform.rotation = prevShadow.transform.rotation;
                activeShadow.color = prevShadow.color;
                activeShadow.sortingOrder = prevShadow.sortingOrder;
                activeShadow.sortingLayerID = prevShadow.sortingLayerID;
                
                if (isShadowEnabled && prevShadow.gameObject.activeSelf)
                {
                    activeShadow.gameObject.SetActive(true);
                }
            }
        }
    }

    public void SetSortingOrder(int order)
    {
        useAutoSorting = false;
        customSortingOrder = order;
        
        if (shadowObject != null) shadowObject.sortingOrder = order;
        if (shadowObject2 != null) shadowObject2.sortingOrder = order;
    }

    public void SetShadowEnabled(bool enable)
    {
        if (enable && !isShadowEnabled)
        {
            if (activeShadow != null)
            {
                activeShadow.transform.localPosition = Vector3.zero;
                activeShadow.transform.localScale = baseShadowScale * scaleRate;
                activeShadow.transform.localRotation = Quaternion.identity;
            }

            currentUpdateDownOffset = 0f;
            currentUpdateScaleBonus = 0f;
            smoothedScaleExcess = 0f;
            currentCurveDistance = 0f;
            currentCurveScale = 0f;
            currentFlightSideOffset = 0f;
            isCurrentlyAnimating = false;
            isUpdateDownCalledThisFrame = false;
            isUpdateScaleUpCalledThisFrame = false;
        }

        isShadowEnabled = enable;
        
        if (!isShadowEnabled && activeShadow != null && activeShadow.gameObject.activeSelf)
        {
            activeShadow.gameObject.SetActive(false);
        }
    }

    public void UpdateCurveMovement(float progress, Vector3 startPosition, Vector3 targetPosition)
    {
        progress = Mathf.Clamp01(progress);
        currentGroundPosition = Vector3.Lerp(startPosition, targetPosition, progress);
        bool isVerticalJump = Mathf.Abs(startPosition.x - targetPosition.x) < 0.005f;

        float activeSagMultiplier = isVerticalJump ? verticalCurveSagMultiplier : curveSagMultiplier;
        float activeScaleMultiplier = isVerticalJump ? verticalCurveScaleMultiplier : curveScaleMultiplier;

        float parabola = 4f * progress * (1f - progress);
        float jumpDistance = Vector3.Distance(startPosition, targetPosition);
        
        currentCurveDistance = parabola * (jumpDistance * activeSagMultiplier);
        currentCurveScale = parabola * (jumpDistance * activeScaleMultiplier);

        isCurrentlyAnimating = progress < 1f;
    }

    public void UpdateFlight(
        Vector3 groundPosition,
        float height,
        float dropPerHeight,
        float sidePerHeight,
        float scalePerHeight)
    {
        height = Mathf.Max(0f, height);
        currentGroundPosition = groundPosition;
        currentCurveDistance = height * dropPerHeight;
        currentCurveScale = height * scalePerHeight;
        currentFlightSideOffset = height * sidePerHeight;
        isCurrentlyAnimating = true;
    }

    public void UpdateDeckCurveMovement(float progress, Vector3 startPosition, Vector3 targetPosition)
    {
        progress = Mathf.Clamp01(progress);
        currentGroundPosition = Vector3.Lerp(startPosition, targetPosition, progress);
        bool isVerticalJump = Mathf.Abs(startPosition.x - targetPosition.x) < 0.005f;

        float activeSagMultiplier = isVerticalJump ? deckVerticalCurveSagMultiplier : deckCurveSagMultiplier;
        float activeScaleMultiplier = isVerticalJump ? deckVerticalCurveScaleMultiplier : deckCurveScaleMultiplier;

        float parabola = 4f * progress * (1f - progress);
        float jumpDistance = Vector3.Distance(startPosition, targetPosition);
        
        currentCurveDistance = parabola * (jumpDistance * activeSagMultiplier);
        currentCurveScale = parabola * (jumpDistance * activeScaleMultiplier);

        isCurrentlyAnimating = progress < 1f;
    }

    public void UpdateMergeFlightCurveMovement(float progress, Vector3 startPosition, Vector3 targetPosition)
    {
        progress = Mathf.Clamp01(progress);
        currentGroundPosition = Vector3.Lerp(startPosition, targetPosition, progress);
        bool isVerticalJump = Mathf.Abs(startPosition.x - targetPosition.x) < 0.005f;

        float activeSagMultiplier = isVerticalJump ? mergeFlightVerticalCurveSagMultiplier : mergeFlightCurveSagMultiplier;
        float activeScaleMultiplier = isVerticalJump ? mergeFlightVerticalCurveScaleMultiplier : mergeFlightCurveScaleMultiplier;

        float parabola = 4f * progress * (1f - progress);
        float jumpDistance = Vector3.Distance(startPosition, targetPosition);
        
        currentCurveDistance = parabola * (jumpDistance * activeSagMultiplier);
        currentCurveScale = parabola * (jumpDistance * activeScaleMultiplier);

        isCurrentlyAnimating = progress < 1f;
    }

    public void UpdateDown(float progress)
    {
        progress = Mathf.Clamp01(progress);
        currentUpdateDownOffset = progress * maxUpdateDownOffset;
        isUpdateDownCalledThisFrame = true;
    }

    public void UpdateScaleUp(float progress)
    {
        progress = Mathf.Clamp01(progress);
        float targetScale = Mathf.Lerp(scaleRate, maxShadowScale, progress);
        currentUpdateScaleBonus = targetScale - scaleRate;
        isUpdateScaleUpCalledThisFrame = true;
    }

    void LateUpdate()
    {
        if (activeShadow == null) return;

        if (!isShadowEnabled)
        {
            if (activeShadow.gameObject.activeSelf) activeShadow.gameObject.SetActive(false);
            return;
        }

        if (!isUpdateDownCalledThisFrame)
        {
            currentUpdateDownOffset = Mathf.MoveTowards(currentUpdateDownOffset, 0f, 5f * Time.deltaTime);
        }

        if (!isUpdateScaleUpCalledThisFrame)
        {
            currentUpdateScaleBonus = Mathf.MoveTowards(currentUpdateScaleBonus, 0f, 5f * Time.deltaTime);
        }

        bool isUpdateDownAnimating = currentUpdateDownOffset > 0.001f;
        bool isUpdateScaleAnimating = currentUpdateScaleBonus > 0.001f;

        float actualYRot = Mathf.DeltaAngle(0f, transform.eulerAngles.y);
        float actualZRot = Mathf.DeltaAngle(0f, transform.eulerAngles.z);
        float tiltYRotation = 0f; 

        if (Mathf.Abs(actualZRot) > 0.01f && Mathf.Abs(actualYRot) > 0.01f)
        {
            float direction = invertTiltDirection ? -1f : 1f;
            tiltYRotation = Mathf.Abs(actualYRot) * Mathf.Sign(actualZRot) * illusionMultiplier * direction;
        }

        bool hasTiltIllusion = Mathf.Abs(tiltYRotation) > 0.01f;

        Vector3 srcScale = followSourceScale ? transform.localScale : Vector3.one;
        float maxScale = Mathf.Max(srcScale.x, Mathf.Max(srcScale.y, srcScale.z));
        float targetScaleExcess = Mathf.Max(0f, maxScale - 1f); 
        
        smoothedScaleExcess = Mathf.Lerp(smoothedScaleExcess, targetScaleExcess, Time.deltaTime * scaleSmoothing);
        
        bool anyScaleAboveOne = srcScale.x > 1f || srcScale.y > 1f || srcScale.z > 1f;
        bool isScaleAnimating = smoothedScaleExcess > 0.01f;

        if (!preventShadowDisable && !anyScaleAboveOne && !hasTiltIllusion && !isScaleAnimating && !isCurrentlyAnimating && !isUpdateDownAnimating && !isUpdateScaleAnimating)
        {
            if (activeShadow.gameObject.activeSelf) activeShadow.gameObject.SetActive(false);
            smoothedScaleExcess = 0f;
            isUpdateDownCalledThisFrame = false;
            isUpdateScaleUpCalledThisFrame = false;
            isCurrentlyAnimating = false;
            return; 
        }

        if (!activeShadow.gameObject.activeSelf)
        {
            activeShadow.gameObject.SetActive(true);
        }

        float activeExcessScale = enableScaleMultiplier ? (smoothedScaleExcess * excessScaleMultiplier) : 0f;
        float finalScaleModifiers = scaleRate + currentCurveScale + activeExcessScale + currentUpdateScaleBonus;
        Vector3 referencePosition = isCurrentlyAnimating ? currentGroundPosition : transform.position;

        float activeExcessDistance = enableDistanceMultiplier ? (smoothedScaleExcess * excessDistanceMultiplier) : 0f;
        float totalDownwardDistance = baseDownOffset + activeExcessDistance + currentCurveDistance + currentUpdateDownOffset;
        Vector3 finalOffset = Vector3.down * totalDownwardDistance;

        finalOffset.y = Mathf.Min(finalOffset.y, -baseDownOffset);
        finalOffset.x += currentFlightSideOffset;

        float targetZRotation = followSourceRotation ? transform.eulerAngles.z : 0f;
        float targetYRotation = followSourceYRotation ? transform.eulerAngles.y : 0f;

        if (enableTiltIllusion && hasTiltIllusion)
        {
            float rawHorizontalOffset = tiltYRotation * tiltHorizontalOffset;
            float clampedHorizontalOffset = Mathf.Clamp(rawHorizontalOffset, -maxHorizontalOffset, maxHorizontalOffset);
            finalOffset += Vector3.right * clampedHorizontalOffset;
            
            float rawVerticalOffset = Mathf.Abs(tiltYRotation) * tiltVerticalOffset;
            float clampedVerticalOffset = Mathf.Clamp(rawVerticalOffset, -maxVerticalOffset, maxVerticalOffset);
            finalOffset += Vector3.up * clampedVerticalOffset;

            finalScaleModifiers += Mathf.Abs(tiltYRotation) * tiltScaleMultiplier;
            
            targetZRotation += -actualZRot * oppositeTiltMultiplier;
        }

        if (followSourceScale)
        {
            activeShadow.transform.localScale = baseShadowScale * finalScaleModifiers;
        }
        else
        {
            Vector3 parentScale = transform.lossyScale;
            Vector3 desiredWorldScale = baseShadowScale * finalScaleModifiers;
            
            float safeX = Mathf.Abs(parentScale.x) > 0.001f ? desiredWorldScale.x / parentScale.x : 0f;
            float safeY = Mathf.Abs(parentScale.y) > 0.001f ? desiredWorldScale.y / parentScale.y : 0f;
            float safeZ = Mathf.Abs(parentScale.z) > 0.001f ? desiredWorldScale.z / parentScale.z : 0f;

            activeShadow.transform.localScale = new Vector3(safeX, safeY, safeZ);
        }

        activeShadow.transform.position = referencePosition + finalOffset;
        activeShadow.transform.rotation = Quaternion.Euler(0f, targetYRotation, targetZRotation);

        activeShadow.sortingLayerID = sourceRenderer.sortingLayerID;
        
        if (useAutoSorting)
        {
            activeShadow.sortingOrder = sourceRenderer.sortingOrder - 1;
        }
        else
        {
            activeShadow.sortingOrder = customSortingOrder;
        }

        float baseAlpha = 1f;
        if (fadeScaleRange > 0f)
        {
            float scaleAlpha = Mathf.Clamp01(smoothedScaleExcess / fadeScaleRange);
            float rotationAlpha = Mathf.Clamp01(Mathf.Abs(tiltYRotation) / 5f);
            float animationAlpha = (preventShadowDisable || isCurrentlyAnimating || isUpdateDownAnimating || isUpdateScaleAnimating) ? 1f : 0f;
            
            baseAlpha = Mathf.Max(scaleAlpha, Mathf.Max(rotationAlpha, animationAlpha));
        }

        float distanceAlpha = 1f;
        if (maxFadeDistance > 0f)
        {
            float distancePercent = Mathf.Clamp01(finalOffset.magnitude / maxFadeDistance);
            distanceAlpha = Mathf.Lerp(1f, minDistanceAlpha, distancePercent);
        }

        float finalAlpha = baseAlpha * distanceAlpha;
        Color shadowColor = activeShadow.color;
        shadowColor.a = finalAlpha;
        activeShadow.color = shadowColor;

        isUpdateDownCalledThisFrame = false;
        isUpdateScaleUpCalledThisFrame = false;
        isCurrentlyAnimating = false;
        currentFlightSideOffset = 0f;
    }
}