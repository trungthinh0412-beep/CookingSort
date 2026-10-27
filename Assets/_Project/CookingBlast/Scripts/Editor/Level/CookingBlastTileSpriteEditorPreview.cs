using System.Collections.Generic;
using CookingSort2D;
using UnityEditor;
using UnityEngine;

// The level editor can override preview artwork without changing gameplay's
// Board prefab mapping. Colors without an override still use Board.prefab.
internal static class CookingBlastTileSpriteEditorPreview
{
    private static readonly Dictionary<FoodColor, string> EditorSpriteOverrides =
        new Dictionary<FoodColor, string>
        {
            { FoodColor.Blue, "Assets/_Project/CookingBlast/Art/Element_block/Blue_.png" },
            { FoodColor.Pink, "Assets/_Project/CookingBlast/Art/Element_block/Green_.png" },
            { FoodColor.Yellow, "Assets/_Project/CookingBlast/Art/Element_block/Orange_.png" },
            { FoodColor.Green, "Assets/_Project/CookingBlast/Art/Element_block/Pink.png" },
            // Runtime slot 4 uses Tile 5 / Animal 5, whose artwork is Purple.
            // Keep serialized enum indexes stable and mirror that mapping here.
            { FoodColor.Red, "Assets/_Project/CookingBlast/Art/Element_block/Purple_.png" },
            { FoodColor.Purple, "Assets/_Project/CookingBlast/Art/Element_block/Red_.png" },
            { FoodColor.Cyan, "Assets/_Project/CookingBlast/Art/Element_block/Cyan.png" },
            // Runtime slots for Yellow and Orange use the opposite artwork.
            { FoodColor.Orange, "Assets/_Project/CookingBlast/Art/Element_block/Yellow_.png" },
            { FoodColor.White, "Assets/_Project/CookingBlast/Art/Element_block/White.png" },
            { FoodColor.Gray, "Assets/_Project/CookingBlast/Art/Element_block/Black_.png" },
            { FoodColor.SoftPink, "Assets/_Project/CookingBlast/Art/Element_block/SoftPink.png" }
        };

    private static readonly Dictionary<FoodColor, Sprite> Cache =
        new Dictionary<FoodColor, Sprite>();

    private static readonly Dictionary<FoodColor, string> EditorAnimalPrefabOverrides =
        new Dictionary<FoodColor, string>
        {
            { FoodColor.Blue, "Assets/_Project/CookingBlast/Prefabs/Animal/Animal 1.prefab" },
            { FoodColor.Pink, "Assets/_Project/CookingBlast/Prefabs/Animal/Animal 4.prefab" },
            { FoodColor.Yellow, "Assets/_Project/CookingBlast/Prefabs/Animal/Animal 6.prefab" },
            { FoodColor.Green, "Assets/_Project/CookingBlast/Prefabs/Animal/Animal 2.prefab" },
            { FoodColor.Purple, "Assets/_Project/CookingBlast/Prefabs/Animal/Animal 5.prefab" },
            { FoodColor.Orange, "Assets/_Project/CookingBlast/Prefabs/Animal/Animal 3.prefab" }
        };

    private static bool animalLoaded;
    private static readonly Dictionary<FoodColor, Sprite> AnimalCache =
        new Dictionary<FoodColor, Sprite>();
private static bool loaded;

    internal static Sprite GetSprite(FoodColor type)
    {
        if (!loaded)
        {
            loaded = true;
            GameObject boardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/CookingBlast/Prefabs/Board.prefab");
            BoardController board = boardAsset != null ?
                boardAsset.GetComponent<BoardController>() : null;
            if (board != null)
            {
                foreach (FoodColor color in System.Enum.GetValues(typeof(FoodColor)))
                {
                    if (EditorSpriteOverrides.TryGetValue(color, out string spritePath))
                    {
                        Sprite editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                        if (editorSprite != null)
                        {
                            Cache[color] = editorSprite;
                            continue;
                        }
                    }

                    FoodTileView tile = board.GetTilePrefab(color);
                    if (tile == null || tile.gameObject.name.Contains("TileBase"))
                        continue;
                    SpriteRenderer renderer = tile.GetComponentInChildren<SpriteRenderer>(true);
                    if (renderer != null && renderer.sprite != null)
                        Cache[color] = renderer.sprite;
                }
            }
        }

        Cache.TryGetValue(type, out Sprite found);
        return found;
    }

internal static Sprite GetAnimalSprite(FoodColor type)
    {
        if (!animalLoaded)
        {
            animalLoaded = true;
            GameObject queueAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/CookingBlast/Prefabs/CustomerQueue.prefab");
            CustomerQueue queue = queueAsset != null
                ? queueAsset.GetComponent<CustomerQueue>() : null;
            if (queue != null)
            {
                foreach (FoodColor color in System.Enum.GetValues(typeof(FoodColor)))
                {
                    if (EditorAnimalPrefabOverrides.TryGetValue(color, out string prefabPath))
                    {
                        GameObject animalAsset =
                            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        SpriteRenderer editorRenderer = animalAsset != null
                            ? animalAsset.GetComponentInChildren<SpriteRenderer>(true)
                            : null;
                        if (editorRenderer != null && editorRenderer.sprite != null)
                            AnimalCache[color] = editorRenderer.sprite;
                        continue;
                    }

                    CustomerView animal = queue.GetAnimalPrefab(color);
                    if (animal == null || animal.gameObject.name.Contains("AnimalBase"))
                        continue;
                    SpriteRenderer sr = animal.GetComponentInChildren<SpriteRenderer>(true);
                    if (sr != null && sr.sprite != null) AnimalCache[color] = sr.sprite;
                }
            }
        }
        AnimalCache.TryGetValue(type, out Sprite result);
        return result;
    }

internal static bool DrawAnimal(Rect area, FoodColor type)
    {
        Sprite sprite = GetAnimalSprite(type);
        if (sprite == null || sprite.texture == null) return false;

        Rect source = sprite.rect;
        Texture2D texture = sprite.texture;
        float factor = Mathf.Min(area.width / source.width, area.height / source.height);
        float width = source.width * factor;
        float height = source.height * factor;
        Rect destination = new Rect(area.center.x - width / 2f,
            area.center.y - height / 2f, width, height);
        Rect uv = new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height);
        GUI.DrawTextureWithTexCoords(destination, texture, uv, true);
        return true;
    }



internal static FoodColor DrawAvailableColorPopup(
        FoodColor current, params GUILayoutOption[] options)
    {
        var supported = CookingBlastTileFromImageWindow.GetAvailableTilePalette();
        if (supported.Count == 0) return current;

        var values = new List<FoodColor>();
        var names = new List<string>();
        // Preserve the old serialized color while offering ONLY configured
        // prefab colors as replacement choices.
        if (!supported.Contains(current))
        {
            values.Add(current);
            names.Add(current + " (legacy - no Tile prefab)");
        }
        foreach (FoodColor color in supported)
        {
            values.Add(color);
            names.Add(color.ToString());
        }
        int selected = Mathf.Max(0, values.IndexOf(current));
        int next = EditorGUILayout.Popup(selected, names.ToArray(), options);
        return values[Mathf.Clamp(next, 0, values.Count - 1)];
    }


    internal static bool Draw(Rect area, FoodColor type)
    {
        Sprite sprite = GetSprite(type);
        if (sprite == null || sprite.texture == null) return false;

        Rect source = sprite.rect;
        Texture2D texture = sprite.texture;
        float factor = Mathf.Min(area.width / source.width, area.height / source.height);
        float width = source.width * factor;
        float height = source.height * factor;
        Rect destination = new Rect(area.center.x - width / 2f,
            area.center.y - height / 2f, width, height);
        Rect uv = new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height);
        GUI.DrawTextureWithTexCoords(destination, texture, uv, true);
        return true;
    }
}
