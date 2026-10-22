using System;
using System.Collections.Generic;
using CookingSort2D;
using UnityEngine;

// Reads the actual PIXEL CELLS, not the JPG/PNG dimensions.
// Screenshots with drawn grid: detect gridline period and sample cell centers.
// Original small PNG pixel art: treat each source pixel as one cell.
internal static class CookingBlastPixelGridDetector
{
    internal const int MaxCellsX = 64;
    internal const int MaxCellsY = 64;
    private const int MaxClusterCount = 6;
    private const int KMeansIterations = 12;

internal static bool TryRead(Texture2D image, IList<FoodColor> palette,
        out FoodColor[,] tiles, out string message)
    {
        tiles = null;
        message = "Choose a pixel image.";
        if (image == null || palette == null || palette.Count == 0)
            return false;

        int[] xCenters = GridCenters(image, true);
        int[] yCenters = GridCenters(image, false);
        bool hasGrid = xCenters != null && yCenters != null;
        if (!hasGrid)
        {
            if (image.width > MaxCellsX || image.height > MaxCellsY)
            {
                message = "Could not identify a regular pixel grid. Use an original small " +
                    "pixel PNG or an image with visible grid lines.";
                return false;
            }

            xCenters = new int[image.width];
            yCenters = new int[image.height];
            for (int x = 0; x < xCenters.Length; x++) xCenters[x] = x;
            for (int y = 0; y < yCenters.Length; y++) yCenters[y] = y;
        }

        int left = xCenters.Length, right = -1;
        int bottom = yCenters.Length, top = -1;
        for (int y = 0; y < yCenters.Length; y++)
        for (int x = 0; x < xCenters.Length; x++)
        {
            Color color = image.GetPixel(xCenters[x], yCenters[y]);
            if (IsBackground(color)) continue;

            if (x < left) left = x;
            if (x > right) right = x;
            if (y < bottom) bottom = y;
            if (y > top) top = y;
        }

        if (left > right || bottom > top)
        {
            message = "The image contains only white/transparent background.";
            return false;
        }

        int columns = right - left + 1;
        int rows = top - bottom + 1;
        if (columns > MaxCellsX || rows > MaxCellsY)
        {
            message = "Artwork contains " + columns + " x " + rows +
                " cells; the safety limit is 64 x 64.";
            return false;
        }

        // Collect content colors AFTER cropping. Near-black outlines and neutral
        // white background are handled separately so they don't distort the
        // main-color clusters (especially yellow and pastel pink).
        var samples = new List<Color>();
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < columns; col++)
        {
            Color color = image.GetPixel(xCenters[left + col], yCenters[bottom + row]);
            if (!IsBackground(color) && !IsDarkOutline(color))
                samples.Add(color);
        }

        int count = Mathf.Min(MaxClusterCount, palette.Count);
        List<Color> centers = BuildColorClusters(samples, count);
        var clusterToTile = new List<FoodColor>(centers.Count);
        foreach (Color center in centers)
            clusterToTile.Add(Nearest(center, palette));

        FoodColor backgroundTile = Nearest(Color.white, palette);
        FoodColor outlineTile = Nearest(Color.black, palette);
        tiles = new FoodColor[columns, rows];
        for (int row = 0; row < rows; row++)
        for (int col = 0; col < columns; col++)
        {
            Color color = image.GetPixel(xCenters[left + col], yCenters[bottom + row]);
            if (IsBackground(color))
                tiles[col, row] = backgroundTile;
            else if (IsDarkOutline(color))
                tiles[col, row] = outlineTile;
            else if (centers.Count != 0)
                tiles[col, row] = clusterToTile[FindNearestCluster(color, centers)];
            else
                tiles[col, row] = Nearest(color, palette);
        }

        message = (hasGrid ? "Screenshot grid detected" : "Original pixel art detected") +
            ": " + columns + " x " + rows + " Tiles. Cropped white margins; " +
            centers.Count + " color clusters mapped to existing FoodColor.";
        return true;
    }

    private static bool IsBackground(Color c)
    {
        if (c.a <= .10f) return true;
        Color.RGBToHSV(c, out float hue, out float saturation, out float value);
        return value > .905f && saturation < .115f;
    }

private static bool IsDarkOutline(Color color)
    {
        if (color.a <= .10f) return false;
        float hue, saturation, value;
        Color.RGBToHSV(color, out hue, out saturation, out value);
        return value <= .28f;
    }

private static List<Color> BuildColorClusters(List<Color> samples, int maxColors)
    {
        var centers = new List<Color>();
        if (samples == null || samples.Count == 0 || maxColors < 1)
            return centers;

        // Farthest-point initialization is deterministic, avoids choosing six
        // similar shades, and keeps small-but-distinct hues represented.
        centers.Add(samples[0]);
        while (centers.Count < Mathf.Min(maxColors, samples.Count))
        {
            float largestDistance = 0.004f;
            int farthestIndex = -1;
            for (int i = 0; i < samples.Count; i++)
            {
                float nearest = float.PositiveInfinity;
                foreach (Color center in centers)
                    nearest = Mathf.Min(nearest, ColorSimilarity(samples[i], center));
                if (nearest <= largestDistance) continue;
                largestDistance = nearest;
                farthestIndex = i;
            }
            if (farthestIndex < 0) break;
            centers.Add(samples[farthestIndex]);
        }

        for (int iteration = 0; iteration < KMeansIterations; iteration++)
        {
            var sums = new Color[centers.Count];
            var populations = new int[centers.Count];
            foreach (Color sample in samples)
            {
                int index = FindNearestCluster(sample, centers);
                sums[index] += sample;
                populations[index]++;
            }

            for (int i = 0; i < centers.Count; i++)
            {
                if (populations[i] == 0) continue;
                // Use visible RGB average for the representative pixel, while
                // ColorSimilarity groups hues perceptually during assignment.
                centers[i] = sums[i] / populations[i];
            }
        }
        return centers;
    }

private static int FindNearestCluster(Color sample, List<Color> centers)
    {
        int best = 0;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < centers.Count; i++)
        {
            float distance = ColorSimilarity(sample, centers[i]);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = i;
        }
        return best;
    }

private static float ColorSimilarity(Color a, Color b)
    {
        float hueA, saturationA, valueA;
        float hueB, saturationB, valueB;
        Color.RGBToHSV(a, out hueA, out saturationA, out valueA);
        Color.RGBToHSV(b, out hueB, out saturationB, out valueB);

        float red = a.r - b.r;
        float green = a.g - b.g;
        float blue = a.b - b.b;
        float hue = Mathf.Abs(hueA - hueB);
        hue = Mathf.Min(hue, 1f - hue);

        // Hue is circular; saturation weighting means nearly white/gray
        // shades are not arbitrarily treated as red because their hue is 0.
        float deltaSaturation = saturationA - saturationB;
        float deltaValue = valueA - valueB;
        return (red * red + green * green + blue * blue) * .23f +
               hue * hue * 1.25f * Mathf.Min(saturationA, saturationB) +
               deltaSaturation * deltaSaturation * .18f +
               deltaValue * deltaValue * .18f;
    }





private static FoodColor Nearest(Color pixel, IList<FoodColor> palette)
    {
        FoodColor best = palette[0];
        float bestScore = float.PositiveInfinity;

        float sourceHue, sourceSaturation, sourceValue;
        Color.RGBToHSV(pixel, out sourceHue, out sourceSaturation, out sourceValue);
        foreach (FoodColor candidate in palette)
        {
            Color tile = CookingSortPalette.Get(candidate);
            float tileHue, tileSaturation, tileValue;
            Color.RGBToHSV(tile, out tileHue, out tileSaturation, out tileValue);

            float dr = pixel.r - tile.r;
            float dg = pixel.g - tile.g;
            float db = pixel.b - tile.b;
            float rgbDistance = (dr * dr + dg * dg + db * db) / 3f;

            float hueDifference = Mathf.Abs(sourceHue - tileHue);
            hueDifference = Mathf.Min(hueDifference, 1f - hueDifference);
            float satDifference = sourceSaturation - tileSaturation;
            float valueDifference = sourceValue - tileValue;

            // Preserve hue in pastel pixel art: RGB alone considers lightly
            // colored pink/yellow artwork closer to white than to colored tiles.
            float distance = rgbDistance * .7f;
            if (sourceSaturation > .10f && tileSaturation > .10f)
                distance += hueDifference * hueDifference * .75f +
                            satDifference * satDifference * .05f;
            distance += valueDifference * valueDifference * .08f;

            // Avoid mapping colorful pastel artwork to White/Gray just because
            // it is bright. Keep neutral colors for genuinely neutral pixels.
            if (sourceSaturation > .14f && tileSaturation < .12f)
                distance += .24f;
            else if (sourceSaturation < .08f && tileSaturation > .30f)
                distance += .20f;

            if (distance < bestScore)
            {
                bestScore = distance;
                best = candidate;
            }
        }
        return best;
    }

    private static int[] GridCenters(Texture2D image, bool verticalLines)
    {
        int length = verticalLines ? image.width : image.height;
        int cross = verticalLines ? image.height : image.width;
        if (length < 12 || cross < 12) return null;
        var points = new List<int>();

        // Sampling both opposite canvas margins filters out most art details.
        int[] strips = { 4, 6, cross - 5, cross - 7 };
        for (int i = 0; i < length; i++)
        {
            int matches = 0;
            for (int j = 0; j < strips.Length; j++)
            {
                int p = Mathf.Clamp(strips[j], 0, cross - 1);
                Color c = verticalLines ? image.GetPixel(i, p) : image.GetPixel(p, i);
                float high = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float low = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                float bright = (c.r + c.g + c.b) / 3f;
                if (high - low < .075f && bright > .42f && bright < .955f)
                    matches++;
            }
            if (matches >= 2) points.Add(i);
        }
        if (points.Count < 5) return null;

        var lines = new List<int>();
        int start = points[0];
        int last = start;
        for (int i = 1; i < points.Count; i++)
        {
            if (points[i] - last <= 2)
            {
                last = points[i];
                continue;
            }
            lines.Add((start + last) / 2);
            start = last = points[i];
        }
        lines.Add((start + last) / 2);
        if (lines.Count < 5) return null;

        var gaps = new List<int>();
        for (int i = 1; i < lines.Count; i++)
        {
            int gap = lines[i] - lines[i - 1];
            if (gap >= 4 && gap <= 100) gaps.Add(gap);
        }
        if (gaps.Count < 4) return null;
        gaps.Sort();
        int spacing = gaps[gaps.Count / 2];

        int regular = 0;
        foreach (int gap in gaps)
            if (Mathf.Abs(gap - spacing) <= Mathf.Max(2f, spacing * .20f))
                regular++;
        if (regular < gaps.Count * .68f) return null;

        var centers = new List<int>();
        float first = lines[0] - spacing * .5f;
        while (first < 0) first += spacing;
        while (first >= spacing) first -= spacing;
        for (float center = first; center < length; center += spacing)
        {
            int p = Mathf.RoundToInt(center);
            if (p >= 0 && p < length) centers.Add(p);
        }
        return centers.Count >= 2 ? centers.ToArray() : null;
    }
}