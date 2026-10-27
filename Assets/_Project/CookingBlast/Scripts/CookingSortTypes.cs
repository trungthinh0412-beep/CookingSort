using System;
using System.Collections.Generic;
using UnityEngine;

namespace CookingSort2D
{
    public enum FoodColor
    {
        Blue,
        Pink,
        Yellow,
        Green,
        Red,
        Purple,
        Cyan,
        Orange,
        White,
        Black,
        Neutral,
        SoftPink
    }

    [Serializable]
    public class CustomerOrderData
    {
        public FoodColor color;
        [Min(1)] public int demand = 10;

        public CustomerOrderData() { }
        public CustomerOrderData(FoodColor color, int demand)
        {
            this.color = color;
            this.demand = Mathf.Max(1, demand);
        }
    }

    [Serializable]
    public class BoardColumnData
    {
        [Tooltip("Index 0 is the exposed bottom tile. Last index is the top tile.")]
        public List<FoodColor> bottomToTop = new List<FoodColor>();
    }

    [Serializable]
    public class BoardClusterData
    {
        public const int DefaultValue = 40;
        public const int DefaultWidth = 2;
        public const int DefaultHeight = 2;

        public FoodColor color;
        [Min(1)] public int value = DefaultValue;
        [Min(0)] public int anchorColumn;
        [Min(0)] public int anchorRow;
        [Min(1)] public int width = DefaultWidth;
        [Min(1)] public int height = DefaultHeight;

        public bool Contains(int column, int row)
        {
            return column >= anchorColumn && column < anchorColumn + width &&
                   row >= anchorRow && row < anchorRow + height;
        }
    }

    [Serializable]
    public class CustomerLaneData
    {
        [Tooltip("Index 0 is the front customer. Last index is the back customer.")]
        public List<CustomerOrderData> frontToBack = new List<CustomerOrderData>();
    }

    public static class CookingSortPalette
    {
        public static Color Get(FoodColor color)
        {
            switch (color)
            {
                case FoodColor.Blue: return new Color32(67, 141, 235, 255);
                case FoodColor.Pink: return new Color32(246, 113, 156, 255);
                case FoodColor.Yellow: return new Color32(255, 203, 61, 255);
                case FoodColor.Green: return new Color32(78, 183, 93, 255);
                case FoodColor.Red: return new Color32(235, 77, 83, 255);
                case FoodColor.Purple: return new Color32(167, 91, 220, 255);
                case FoodColor.Cyan: return new Color32(89, 214, 201, 255);
                case FoodColor.Orange: return new Color32(245, 147, 53, 255);
                case FoodColor.White: return new Color32(241, 241, 235, 255);
                case FoodColor.Black: return new Color32(92, 101, 115, 255);
                case FoodColor.Neutral: return new Color32(135, 142, 151, 255);
                case FoodColor.SoftPink: return new Color32(255, 137, 186, 255);
                default: return Color.white;
            }
        }

        public static string ShortName(FoodColor color)
        {
            switch (color)
            {
                case FoodColor.Blue: return "B";
                case FoodColor.Pink: return "P";
                case FoodColor.Yellow: return "Y";
                case FoodColor.Green: return "G";
                case FoodColor.Red: return "R";
                case FoodColor.Purple: return "V";
                case FoodColor.Cyan: return "C";
                case FoodColor.Orange: return "O";
                case FoodColor.White: return "W";
                case FoodColor.Black: return "D";
                case FoodColor.Neutral: return "N";
                case FoodColor.SoftPink: return "SP";
                default: return "?";
            }
        }
    }
}
