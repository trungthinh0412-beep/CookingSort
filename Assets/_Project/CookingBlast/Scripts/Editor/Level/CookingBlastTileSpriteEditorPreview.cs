using System.Collections.Generic;
using CookingSort2D;
using UnityEditor;
using UnityEngine;

// Level Editor preview uses the same prefab mappings as gameplay so the
// displayed animal/tile always matches the serialized FoodColor value.
internal static class CookingBlastTileSpriteEditorPreview
{
    private static readonly Dictionary<FoodColor, Sprite> Cache =
        new Dictionary<FoodColor, Sprite>();

    private static bool animalLoaded;
    private static int configuredAnimalCount;
    private static CustomerQueue levelQueue;
    private static readonly Dictionary<FoodColor, Sprite> AnimalCache =
        new Dictionary<FoodColor, Sprite>();
private static bool loaded;
    private static int configuredTileCount;
    private static BoardController levelBoard;
    private static int sourceHash;

    internal static void SetLevelContext(CookingBlastLevel level)
    {
        BoardController nextBoard = level != null ? level.Board : null;
        CustomerQueue nextQueue = level != null ? level.CustomerQueue : null;
        int nextHash = CalculateSourceHash(nextBoard, nextQueue);
        if (levelBoard == nextBoard && levelQueue == nextQueue && sourceHash == nextHash)
            return;

        levelBoard = nextBoard;
        levelQueue = nextQueue;
        sourceHash = nextHash;
        loaded = false;
        animalLoaded = false;
        configuredTileCount = 0;
        configuredAnimalCount = 0;
        Cache.Clear();
        AnimalCache.Clear();
    }

    private static int CalculateSourceHash(BoardController board, CustomerQueue queue)
    {
        unchecked
        {
            int hash = 17;
            int tileCount = board != null ? board.ConfiguredTilePrefabCount : 0;
            hash = hash * 31 + tileCount;
            for (int i = 0; i < tileCount; i++)
            {
                FoodTileView prefab = board.GetConfiguredTilePrefab(i);
                hash = hash * 31 + (prefab != null ? prefab.GetInstanceID() : 0);
            }

            int animalCount = queue != null ? queue.ConfiguredAnimalPrefabCount : 0;
            hash = hash * 31 + animalCount;
            for (int i = 0; i < animalCount; i++)
            {
                CustomerView prefab = queue.GetConfiguredAnimalPrefab(i);
                hash = hash * 31 + (prefab != null ? prefab.GetInstanceID() : 0);
            }
            return hash;
        }
    }

    internal static Sprite GetSprite(FoodColor type)
    {
        if (!loaded)
        {
            loaded = true;
            BoardController board = levelBoard;
            if (board == null)
            {
                GameObject boardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/CookingBlast/Prefabs/Board.prefab");
                board = boardAsset != null ? boardAsset.GetComponent<BoardController>() : null;
            }
            if (board != null)
            {
                configuredTileCount = board.ConfiguredTilePrefabCount;
                for (int index = 0; index < board.ConfiguredTilePrefabCount; index++)
                {
                    FoodTileView tile = board.GetConfiguredTilePrefab(index);
                    if (tile == null) continue;
                    SpriteRenderer renderer = tile.GetComponentInChildren<SpriteRenderer>(true);
                    if (renderer != null && renderer.sprite != null)
                        Cache[(FoodColor)index] = renderer.sprite;
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
            CustomerQueue queue = levelQueue;
            if (queue == null)
            {
                GameObject queueAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/CookingBlast/Prefabs/CustomerQueue.prefab");
                queue = queueAsset != null ? queueAsset.GetComponent<CustomerQueue>() : null;
            }
            if (queue != null)
            {
                configuredAnimalCount = queue.ConfiguredAnimalPrefabCount;
                for (int index = 0; index < queue.ConfiguredAnimalPrefabCount; index++)
                {
                    CustomerView animal = queue.GetConfiguredAnimalPrefab(index);
                    if (animal == null) continue;
                    SpriteRenderer sr = animal.GetComponentInChildren<SpriteRenderer>(true);
                    if (sr != null && sr.sprite != null) AnimalCache[(FoodColor)index] = sr.sprite;
                }
            }
        }
        AnimalCache.TryGetValue(type, out Sprite result);
        return result;
    }

    internal static List<FoodColor> GetConfiguredColors()
    {
        // Trigger each cache once, then expose only indexes explicitly present
        // in BOTH gameplay lists. Fallback prefabs and extra enum values are excluded.
        GetSprite(default);
        GetAnimalSprite(default);
        int count = Mathf.Min(configuredTileCount, configuredAnimalCount);
        var result = new List<FoodColor>(count);
        for (int index = 0; index < count; index++)
        {
            FoodColor color = (FoodColor)index;
            if (Cache.ContainsKey(color) && AnimalCache.ContainsKey(color))
                result.Add(color);
        }
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
