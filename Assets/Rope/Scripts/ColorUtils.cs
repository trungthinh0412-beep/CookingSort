using UnityEngine;

public static class ColorUtils
    {
        public static readonly Color pink = new Color32(255, 105, 180, 255);
        public static readonly Color orange = new Color32(255, 162, 0, 255);
        public static readonly Color gold = new Color32(255, 208, 180, 255);
        public static readonly Color lightGreen = new Color32(11, 255, 111, 255);
        public static readonly Color lightBlue = new Color32(127, 232, 255, 255);
        public static readonly Color niceBlue = new Color32(51, 153, 255, 255);



        /// <summary>
        /// Return new color with the HSV value shifted by <paramref name="dh"/>, <paramref name="ds"/>, <paramref name="dv"/>
        /// </summary>
        public static Color ShiftHSV(Color c, float dh = 0, float ds = 0, float dv = 0)
        {
            void shift(ref float f, float df)
            {
                f += df;
                while (f < 0) f += 1;
                f = f % 1;
            }

            float a = c.a;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (dh != 0) shift(ref h, dh);
            if (ds != 0) shift(ref s, ds);
            if (dv != 0) shift(ref v, dv);

            var col = Color.HSVToRGB(h, s, v);
            col.a = a;
            return col;
        }


        public static string RichTextTag(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(c);
        }

        public static float GetHSVValue(Color c, HSVValueType type = HSVValueType.Hue)
        {
            Color.RGBToHSV(c, out var h, out var s, out var v);
            if (type == HSVValueType.Hue) return h;
            else if (type == HSVValueType.Saturation) return s;
            else if (type == HSVValueType.Value) return v;
            else return 0;
        }

        public static Vector3 GetHSVVector(Color c)
        {
            Color.RGBToHSV(c, out var h, out var s, out var v);
            return new Vector3(h, s, v);
        }

        public static Color FromHSVVector(Vector3 v)
        {
            return Color.HSVToRGB(v.x, v.y, v.z);
        }

        public enum HSVValueType
        {
            Hue, Saturation, Value
        }

    }