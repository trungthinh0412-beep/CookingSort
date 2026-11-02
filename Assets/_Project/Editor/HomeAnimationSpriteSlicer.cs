using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

internal static class HomeAnimationSpriteSlicer
{
    private const string SourceAssetPath =
        "Assets/_Project/Textures/GUI/1. Home/Home_animation.png";

    private const int FrameWidth = 753;
    private const int FrameHeight = 642;
    private const int Columns = 10;
    private const int Rows = 6;
    private const int FramesPerSheet = Columns * Rows;
    private const int SheetWidth = FrameWidth * Columns;
    private const int SheetHeight = FrameHeight * Rows;
    private const int AndroidMaxSize = 8192;

    private static readonly SheetDefinition[] Sheets =
    {
        new SheetDefinition(
            "Assets/_Project/Textures/GUI/1. Home/Home_animation_000_059.png",
            0
        ),
        new SheetDefinition(
            "Assets/_Project/Textures/GUI/1. Home/Home_animation_060_119.png",
            60
        ),
        new SheetDefinition(
            "Assets/_Project/Textures/GUI/1. Home/Home_animation_120_179.png",
            120
        )
    };

    [InitializeOnLoadMethod]
    private static void ScheduleValidation()
    {
        EditorApplication.delayCall += EnsureCorrectLayout;
    }

    [MenuItem("Tools/Cooking Sort/Build Sharp Home Animation Sheets")]
    private static void BuildFromMenu()
    {
        ApplyAllSheets(true);
    }

    private static void EnsureCorrectLayout()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureCorrectLayout;
            return;
        }

        ApplyAllSheets(false);
    }

    private static void ApplyAllSheets(bool force)
    {
        ConfigureSourceTexture();

        foreach (SheetDefinition sheet in Sheets)
        {
            ApplySheet(sheet, force);
        }
    }

    private static void ConfigureSourceTexture()
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(SourceAssetPath) as TextureImporter;

        if (importer == null || !NeedsQualityUpdate(importer))
            return;

        ApplyMaximumQualitySettings(importer);
        importer.SaveAndReimport();
    }

    private static void ApplySheet(SheetDefinition sheet, bool force)
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(sheet.AssetPath) as TextureImporter;

        if (importer == null)
        {
            Debug.LogWarning(
                $"[HomeAnimationSpriteSlicer] Texture not found: {sheet.AssetPath}"
            );
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.npotScale = TextureImporterNPOTScale.None;

        SpriteDataProviderFactories factories =
            new SpriteDataProviderFactories();
        factories.Init();

        ISpriteEditorDataProvider dataProvider =
            factories.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        SpriteRect[] existingRects = dataProvider.GetSpriteRects();
        bool layoutIsCorrect = HasCorrectLayout(
            existingRects,
            sheet.FirstFrame
        );
        bool qualityIsCorrect = !NeedsQualityUpdate(importer);

        if (!force && layoutIsCorrect && qualityIsCorrect)
            return;

        Dictionary<string, GUID> existingIds = existingRects
            .GroupBy(spriteRect => spriteRect.name)
            .ToDictionary(group => group.Key, group => group.First().spriteID);

        SpriteRect[] spriteRects = new SpriteRect[FramesPerSheet];
        List<SpriteNameFileIdPair> nameFileIdPairs =
            new List<SpriteNameFileIdPair>(FramesPerSheet);

        for (int localIndex = 0; localIndex < FramesPerSheet; localIndex++)
        {
            int globalIndex = sheet.FirstFrame + localIndex;
            int row = localIndex / Columns;
            int column = localIndex % Columns;
            string spriteName = $"Home_animation_{globalIndex}";

            GUID spriteId = existingIds.TryGetValue(
                spriteName,
                out GUID existingId
            )
                ? existingId
                : GUID.Generate();

            SpriteRect spriteRect = new SpriteRect
            {
                name = spriteName,
                rect = new Rect(
                    column * FrameWidth,
                    (Rows - 1 - row) * FrameHeight,
                    FrameWidth,
                    FrameHeight
                ),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
                spriteID = spriteId
            };

            spriteRects[localIndex] = spriteRect;
            nameFileIdPairs.Add(
                new SpriteNameFileIdPair(spriteName, spriteId)
            );
        }

        dataProvider.SetSpriteRects(spriteRects);

        ISpriteNameFileIdDataProvider nameFileIdDataProvider =
            dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameFileIdDataProvider?.SetNameFileIdPairs(nameFileIdPairs);

        dataProvider.Apply();
        ApplyMaximumQualitySettings(importer);
        importer.SaveAndReimport();

        Texture2D importedTexture =
            AssetDatabase.LoadAssetAtPath<Texture2D>(sheet.AssetPath);
        if (importedTexture == null ||
            importedTexture.width != SheetWidth ||
            importedTexture.height != SheetHeight)
        {
            Debug.LogError(
                $"[HomeAnimationSpriteSlicer] Unexpected imported size for " +
                $"{sheet.AssetPath}. Expected {SheetWidth}x{SheetHeight}, got " +
                $"{importedTexture?.width ?? 0}x{importedTexture?.height ?? 0}."
            );
            return;
        }

        Debug.Log(
            $"[HomeAnimationSpriteSlicer] Built {sheet.AssetPath} with " +
            $"frames {sheet.FirstFrame}-{sheet.FirstFrame + FramesPerSheet - 1} " +
            $"at exact size {SheetWidth}x{SheetHeight}."
        );
    }

    private static void ApplyMaximumQualitySettings(TextureImporter importer)
    {
        importer.maxTextureSize = AndroidMaxSize;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.compressionQuality = 100;
        importer.crunchedCompression = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;

        TextureImporterPlatformSettings androidSettings =
            importer.GetPlatformTextureSettings("Android");
        androidSettings.name = "Android";
        androidSettings.overridden = true;
        androidSettings.maxTextureSize = AndroidMaxSize;
        androidSettings.format = TextureImporterFormat.RGBA32;
        androidSettings.textureCompression =
            TextureImporterCompression.Uncompressed;
        androidSettings.compressionQuality = 100;
        androidSettings.crunchedCompression = false;
        importer.SetPlatformTextureSettings(androidSettings);
    }

    private static bool NeedsQualityUpdate(TextureImporter importer)
    {
        TextureImporterPlatformSettings androidSettings =
            importer.GetPlatformTextureSettings("Android");

        return
            importer.maxTextureSize != AndroidMaxSize ||
            importer.npotScale != TextureImporterNPOTScale.None ||
            importer.mipmapEnabled ||
            importer.filterMode != FilterMode.Point ||
            importer.textureCompression != TextureImporterCompression.Uncompressed ||
            importer.compressionQuality != 100 ||
            importer.crunchedCompression ||
            !androidSettings.overridden ||
            androidSettings.maxTextureSize != AndroidMaxSize ||
            androidSettings.format != TextureImporterFormat.RGBA32 ||
            androidSettings.textureCompression != TextureImporterCompression.Uncompressed ||
            androidSettings.compressionQuality != 100 ||
            androidSettings.crunchedCompression;
    }

    private static bool HasCorrectLayout(
        SpriteRect[] spriteRects,
        int firstFrame
    )
    {
        if (spriteRects == null || spriteRects.Length != FramesPerSheet)
            return false;

        Dictionary<string, SpriteRect> spritesByName = spriteRects
            .GroupBy(spriteRect => spriteRect.name)
            .ToDictionary(group => group.Key, group => group.First());

        for (int localIndex = 0; localIndex < FramesPerSheet; localIndex++)
        {
            int globalIndex = firstFrame + localIndex;
            int row = localIndex / Columns;
            int column = localIndex % Columns;
            string spriteName = $"Home_animation_{globalIndex}";

            if (!spritesByName.TryGetValue(
                    spriteName,
                    out SpriteRect spriteRect
                ))
            {
                return false;
            }

            Rect expectedRect = new Rect(
                column * FrameWidth,
                (Rows - 1 - row) * FrameHeight,
                FrameWidth,
                FrameHeight
            );

            if (spriteRect.rect != expectedRect)
                return false;
        }

        return true;
    }

    private readonly struct SheetDefinition
    {
        public readonly string AssetPath;
        public readonly int FirstFrame;

        public SheetDefinition(string assetPath, int firstFrame)
        {
            AssetPath = assetPath;
            FirstFrame = firstFrame;
        }
    }
}
