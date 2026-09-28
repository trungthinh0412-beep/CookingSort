using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public static class UtilEx
{
    /// <summary>
    /// clear all child of transform
    /// </summary>
    /// <param name="transform"></param>
    // public static void Clear(
    //     this Transform transform)
    // {
    //     var childs = transform.childCount;
    //     for (int i = childs - 1; i >= 0; i--)
    //     {
    //         Object.DestroyImmediate(transform.GetChild(i)
    //             .gameObject);
    //     }
    // }

    /// <summary>
    /// 
    /// </summary>
    public static void ActiveObject(
        Image[] darks,
        Image[] lights,
        int number)
    {
        switch (number)
        {
            case 1:
                darks[0]
                    .gameObject.SetActive(false);
                lights[0]
                    .gameObject.SetActive(true);
                break;
            case 2:
                darks[0]
                    .gameObject.SetActive(false);
                darks[1]
                    .gameObject.SetActive(false);
                lights[0]
                    .gameObject.SetActive(true);
                lights[1]
                    .gameObject.SetActive(true);
                break;
            case 3:
                foreach (var d in darks)
                {
                    d.gameObject.SetActive(false);
                }

                foreach (var l in lights)
                {
                    l.gameObject.SetActive(true);
                }

                break;
            default:
                foreach (var dark in darks)
                {
                    dark.gameObject.SetActive(true);
                }

                foreach (var image in lights)
                {
                    image.gameObject.SetActive(false);
                }

                break;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="bounds"></param>
    /// <param name="array"></param>
    public static void GetPointsNoAlloc(
        this Bounds bounds,
        Vector3[] array)
    {
        var min = bounds.min;
        var max = bounds.max;

        array[0] = new Vector3(min.x, min.y, min.z);
        array[1] = new Vector3(min.x, min.y, max.z);
        array[2] = new Vector3(min.x, max.y, min.z);
        array[3] = new Vector3(min.x, max.y, max.z);
        array[4] = new Vector3(max.x, min.y, min.z);
        array[5] = new Vector3(max.x, min.y, max.z);
        array[6] = new Vector3(max.x, max.y, min.z);
        array[7] = new Vector3(max.x, max.y, max.z);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="corners"></param>
    /// <returns></returns>
    public static Bounds BoundsFromCorners(
        Vector3[] corners)
    {
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var minZ = float.MaxValue;

        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var maxZ = float.MinValue;

        foreach (var corner in corners)
        {
            if (corner.x < minX)
            {
                minX = corner.x;
            }

            if (corner.y < minY)
            {
                minY = corner.y;
            }

            if (corner.z < minZ)
            {
                minZ = corner.z;
            }

            if (corner.x > minX)
            {
                maxX = corner.x;
            }

            if (corner.y > minY)
            {
                maxY = corner.y;
            }

            if (corner.z > minZ)
            {
                maxZ = corner.z;
            }
        }

        return new Bounds() { min = new Vector3(minX, minY, minZ), max = new Vector3(maxX, maxY, maxZ) };
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="i"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T GetClamped<T>(this IList<T> source, int i)
    {
        return source[Mathf.Clamp(i, 0, source.Count - 1)];
    }

    /// <summary>
    /// Splits an array into several smaller arrays.
    /// </summary>
    /// <typeparam name="T">The type of the array.</typeparam>
    /// <param name="array">The array to split.</param>
    /// <param name="size">The size of the smaller arrays.</param>
    /// <returns>An array containing smaller arrays.</returns>
    public static IEnumerable<IEnumerable<T>> Split<T>(this T[] array, int size)
    {
        for (var i = 0; i < (float)array.Length / size; i++)
        {
            yield return array.Skip(i * size).Take(size);
        }
    }

    /// <summary>
    /// Splits an array into several smaller arrays.
    /// </summary>
    /// <typeparam name="T">The type of the array.</typeparam>
    /// <param name="array">The array to split.</param>
    /// <param name="size">The size of the smaller arrays.</param>
    /// <returns>An array containing smaller arrays.</returns>
    public static IEnumerable<IEnumerable<T>> Split<T>(this List<T> array, int size)
    {
        for (var i = 0; i < (float)array.Count / size; i++)
        {
            yield return array.Skip(i * size).Take(size);
        }
    }

    public static bool CalculateIdealCount(float availableSpace, float minSize, float maxSize, int defaultCount, out int count, out float size)
    {
        int minCount = Mathf.FloorToInt(availableSpace / maxSize);
        int maxCount = Mathf.FloorToInt(availableSpace / minSize);
        bool goodness = defaultCount >= minCount && defaultCount <= maxCount;
        count = Mathf.Clamp(defaultCount, minCount, maxCount);
        size = availableSpace / count;
        return goodness;
    }
}