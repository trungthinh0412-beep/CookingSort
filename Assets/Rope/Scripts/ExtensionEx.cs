using System.Collections.Generic;
using UnityEngine;

public static class ExtensionEx
{
    public static Rect Grown(this Rect r, float f)
    {
        return r.Grown(Vector2.one * f);
    }
    
    public static Rect Grown(this Rect r, Vector2 half)
    {
        return new Rect(r.position - half, r.size + half * 2);
    }
    
    public static K GetOrDefault<T, K>(this IDictionary<T, K> dict, T key, K defaultOverride = default)
    {
        if (dict.TryGetValue(key, out var val))
        {
            return val;
        }
        return defaultOverride;
    }
    
    /// <summary>
    /// Calculate normalized texturerect of a sprite (0->1)
    /// </summary>
    public static Rect LocalTextureRect(this Sprite sprite)
    {
        Vector2 txcPos = sprite.textureRect.position;
        Vector2 txcSize = sprite.textureRect.size;
        txcPos.x /= sprite.texture.width;
        txcPos.y /= sprite.texture.height;
        txcSize.x /= sprite.texture.width;
        txcSize.y /= sprite.texture.height;
        return new Rect(txcPos, txcSize);
    }
    
    public static Vector3 Mult(this Vector3 v, float x, float y, float z)
    {
        return v.Mult(new Vector3(x, y, z));
    }

    public static Vector3 Mult(this Vector3 v, Vector3 other)
    {
        return Vector3.Scale(v, other);
    }
}
