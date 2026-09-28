using CustomTween;
using UnityEngine;

public class WinBonusArea : MonoBehaviour
{
    [SerializeField] private int multiBonus = 1;
    [SerializeField] private Transform txt;
    [SerializeField] private float scaleUp = 1.5f;
    [SerializeField] private float timeScale = 0.2f;

    public int MultiBonus
    {
        get => multiBonus;
        set => multiBonus = value;
    }

    public void ChangeColor(bool isSelected)
    {

    }
    public void ScaleUp()
    {
        Tween.Scale(txt.transform, scaleUp, timeScale);
    }
    public void ScaleDown()
    {
        Tween.Scale(txt.transform, 1, timeScale);
    }

}
