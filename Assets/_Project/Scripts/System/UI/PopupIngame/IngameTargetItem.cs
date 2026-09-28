using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class IngameTargetItem : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI countText;

    public void Setup(Sprite sprite, int remaining)
    {
        if (icon != null)
            icon.sprite = sprite;

        SetRemaining(remaining);
    }

    public void SetRemaining(int remaining)
    {
        if (countText != null)
            countText.text = Mathf.Max(0, remaining).ToString();
    }
}
