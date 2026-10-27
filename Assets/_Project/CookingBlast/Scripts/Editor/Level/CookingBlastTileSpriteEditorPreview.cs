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

private static BoardController GetBoardSource()
    {
        if (levelBoard != null) return levelBoard;
        GameObject boardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/CookingBlast/Prefabs/Board.prefab");
        return boardAsset != null ? boardAsset.GetComponent<BoardController>() : null;
    }

private static CustomerQueue GetAnimalSource()
    {
        if (levelQueue != null) return levelQueue;
        GameObject queueAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/CookingBlast/Prefabs/CustomerQueue.prefab");
        return queueAsset != null ? queueAsset.GetComponent<CustomerQueue>() : null;
    }

internal static List<FoodTileView> GetConfiguredTilePrefabs()
    {
        var result = new List<FoodTileView>();
        BoardController board = GetBoardSource();
        if (board == null) return result;
        for (int i = 0; i < board.ConfiguredTilePrefabCount; i++)
        {
            FoodTileView tile = board.GetConfiguredTilePrefab(i);
            if (tile != null && tile.Type != FoodColor.Neutral)
                result.Add(tile);
        }
        return result;
    }

internal static List<CustomerView> GetConfiguredAnimalPrefabs()
    {
        var result = new List<CustomerView>();
        CustomerQueue queue = GetAnimalSource();
        if (queue == null) return result;
        for (int i = 0; i < queue.ConfiguredAnimalPrefabCount; i++)
        {
            CustomerView animal = queue.GetConfiguredAnimalPrefab(i);
            if (animal != null && animal.Type != FoodColor.Neutral)
                result.Add(animal);
        }
        return result;
    }

internal static List<FoodColor> GetConfiguredTileColors()
    {
        var colors = new List<FoodColor>();
        foreach (FoodTileView tile in GetConfiguredTilePrefabs())
            if (!colors.Contains(tile.Type)) colors.Add(tile.Type);
        return colors;
    }

internal static List<FoodColor> GetConfiguredAnimalColors()
    {
        var colors = new List<FoodColor>();
        foreach (CustomerView animal in GetConfiguredAnimalPrefabs())
            if (!colors.Contains(animal.Type)) colors.Add(animal.Type);
        return colors;
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
                if (prefab == null) continue;
                hash = hash * 31 + (int)prefab.Type;
                hash = hash * 31 + prefab.FootprintWidth;
                hash = hash * 31 + prefab.FootprintHeight;
                hash = hash * 31 + prefab.FootprintValue;
                SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
                hash = hash * 31 + (renderer != null && renderer.sprite != null
                    ? renderer.sprite.GetInstanceID() : 0);
            }

            int animalCount = queue != null ? queue.ConfiguredAnimalPrefabCount : 0;
            hash = hash * 31 + animalCount;
            for (int i = 0; i < animalCount; i++)
            {
                CustomerView prefab = queue.GetConfiguredAnimalPrefab(i);
                hash = hash * 31 + (prefab != null ? prefab.GetInstanceID() : 0);
                if (prefab == null) continue;
                hash = hash * 31 + (int)prefab.Type;
                SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
                hash = hash * 31 + (renderer != null && renderer.sprite != null
                    ? renderer.sprite.GetInstanceID() : 0);
            }
            return hash;
        }
    }

internal static Sprite GetSprite(FoodColor type)
    {
        if (!loaded)
        {
            loaded = true;
            Cache.Clear();
            BoardController board = GetBoardSource();
            configuredTileCount = board != null ? board.ConfiguredTilePrefabCount : 0;
            for (int index = 0; index < configuredTileCount; index++)
            {
                FoodTileView tile = board.GetConfiguredTilePrefab(index);
                if (tile == null || tile.Type == FoodColor.Neutral) continue;
                SpriteRenderer renderer = tile.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null && renderer.sprite != null &&
                    !Cache.ContainsKey(tile.Type))
                    Cache.Add(tile.Type, renderer.sprite);
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
            AnimalCache.Clear();
            CustomerQueue queue = GetAnimalSource();
            configuredAnimalCount = queue != null ? queue.ConfiguredAnimalPrefabCount : 0;
            for (int index = 0; index < configuredAnimalCount; index++)
            {
                CustomerView animal = queue.GetConfiguredAnimalPrefab(index);
                if (animal == null || animal.Type == FoodColor.Neutral) continue;
                SpriteRenderer renderer = animal.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null && renderer.sprite != null &&
                    !AnimalCache.ContainsKey(animal.Type))
                    AnimalCache.Add(animal.Type, renderer.sprite);
            }
        }
        AnimalCache.TryGetValue(type, out Sprite found);
        return found;
    }

internal static List<FoodColor> GetConfiguredColors()
    {
        // Only playable match types: BOTH an explicitly configured Tile AND
        // Animal must be present. Their list order and list lengths may differ.
        GetSprite(default(FoodColor));
        GetAnimalSprite(default(FoodColor));
        var result = new List<FoodColor>();
        foreach (FoodColor type in System.Enum.GetValues(typeof(FoodColor)))
            if (type != FoodColor.Neutral &&
                Cache.ContainsKey(type) && AnimalCache.ContainsKey(type))
                result.Add(type);
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
