using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the profile scroll hierarchy predictable while preserving the
/// existing prefab layout and item spacing.
/// </summary>
public sealed class ProfileScrollViewSystem : MonoBehaviour
{
    private void Awake()
    {
        NormalizeHierarchy();
    }

    private void OnEnable()
    {
        ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void NormalizeHierarchy()
    {
        ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (scrollRect == null)
            return;

        scrollRect.gameObject.name = "AvatarScrollView";

        if (scrollRect.viewport != null)
            scrollRect.viewport.gameObject.name = "AvatarScrollViewport";

        if (scrollRect.content != null)
            scrollRect.content.gameObject.name = "AvatarScrollContent";
    }
}
