using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "TargetConfig",
    menuName = "ScriptableObject/TargetConfig")]
public class TargetConfig : ScriptableObject
{
    [Tooltip("Keo texture da import Sprite Mode = Multiple vao day de tu dong gan icon theo thu tu.")]
    [SerializeField] private Texture2D spriteSheet;

    public List<TargetIconData> targets =
        new List<TargetIconData>();

    public Texture2D SpriteSheet => spriteSheet;

    public Sprite GetTargetSprite(CardType cardType)
    {
        TargetIconData data = targets?.Find(
            item => item != null && item.cardType == cardType
        );

        return data != null ? data.icon : null;
    }
}

[Serializable]
public class TargetIconData
{
    public CardType cardType;
    public Sprite icon;
}
