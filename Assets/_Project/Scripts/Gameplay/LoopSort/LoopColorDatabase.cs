using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicSoft.LoopSort
{
    [Serializable]
    public sealed class LoopColorEntry
    {
        public LoopCubeColor color;
        public Color displayColor = Color.white;
        public Material cubeMaterial;
        public Material containerMaterial;
    }

    [CreateAssetMenu(
        fileName = "Loop Color Database",
        menuName = "Magic Soft/Loop Sort/Color Database")]
    public sealed class LoopColorDatabase : ScriptableObject
    {
        public List<LoopColorEntry> colors =
            new List<LoopColorEntry>();

        public Color GetColor(LoopCubeColor color)
        {
            LoopColorEntry entry = Find(color);
            return entry != null
                ? entry.displayColor
                : GetFallbackColor(color);
        }

        public Material GetCubeMaterial(LoopCubeColor color)
        {
            LoopColorEntry entry = Find(color);
            return entry != null ? entry.cubeMaterial : null;
        }

        public Material GetContainerMaterial(LoopCubeColor color)
        {
            LoopColorEntry entry = Find(color);
            return entry != null ? entry.containerMaterial : null;
        }

        private LoopColorEntry Find(LoopCubeColor color)
        {
            if (colors == null)
                return null;

            for (int i = 0; i < colors.Count; i++)
            {
                if (colors[i] != null && colors[i].color == color)
                    return colors[i];
            }

            return null;
        }

        public static Color GetFallbackColor(LoopCubeColor color)
        {
            switch (color)
            {
                case LoopCubeColor.Red:
                    return new Color32(239, 74, 78, 255);
                case LoopCubeColor.Blue:
                    return new Color32(55, 139, 235, 255);
                case LoopCubeColor.Green:
                    return new Color32(62, 193, 112, 255);
                case LoopCubeColor.Yellow:
                    return new Color32(246, 195, 66, 255);
                case LoopCubeColor.Purple:
                    return new Color32(154, 91, 219, 255);
                case LoopCubeColor.Orange:
                    return new Color32(244, 132, 51, 255);
                default:
                    return Color.white;
            }
        }
    }
}
