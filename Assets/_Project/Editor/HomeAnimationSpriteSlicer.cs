using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

internal static class HomeAnimationSpriteSlicer
{
    private const string SourceAssetPath =
        "Assets/_Project/Textures/GUI/1. Home/Home_animation2.png";
    private const string PopupHomeAssetPath =
        "Assets/_Project/Prefabs/Popup/PopupHome.prefab";

    private const int FrameWidth = 720;
    private const int FrameHeight = 724;
    private const int Columns = 10;
    private const int Rows = 8;
    private const int FramesPerSheet = Columns * Rows;
    private const int SheetWidth = FrameWidth * Columns;
    private const int SheetHeight = FrameHeight * Rows;
    private const int AndroidMaxSize = 8192;
    private const int SourcePreviewMaxSize = 2048;

    private static readonly SheetDefinition[] Sheets =
    {
        new SheetDefinition(
            "Assets/_Project/Textures/GUI/1. Home/Home_animation2_000_079.png",
            0
        ),
        new SheetDefinition(
            "Assets/_Project/Textures/GUI/1. Home/Home_animation2_080_159.png",
            80
        ),
        new SheetDefinition(
            "Assets/_Project/Textures/GUI/1. Home/Home_animation2_160_239.png",
            160
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
        // Opening prefab contents while entering Play Mode invokes Awake on
        // components inside preview scenes. Validate assets only in Edit Mode.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

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

        ConfigurePopupHomeAnimation();
    }

    private static void ConfigureSourceTexture()
    {
        TextureImporter importer =
            AssetImporter.GetAtPath(SourceAssetPath) as TextureImporter;

        if (importer == null || !NeedsSourcePreviewUpdate(importer))
            return;

        importer.textureType = TextureImporterType.Default;
        importer.maxTextureSize = SourcePreviewMaxSize;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.compressionQuality = 50;
        importer.crunchedCompression = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.ClearPlatformTextureSettings("Android");
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
            sheet
        );
        bool qualityIsCorrect = !NeedsQualityUpdate(importer);

        if (!force && layoutIsCorrect && qualityIsCorrect)
            return;

        Dictionary<Rect, GUID> existingIdsByRect = existingRects
            .GroupBy(spriteRect => spriteRect.rect)
            .ToDictionary(group => group.Key, group => group.First().spriteID);

        SpriteRect[] spriteRects = new SpriteRect[FramesPerSheet];
        List<SpriteNameFileIdPair> nameFileIdPairs =
            new List<SpriteNameFileIdPair>(FramesPerSheet);

        for (int localIndex = 0; localIndex < FramesPerSheet; localIndex++)
        {
            int row = localIndex / Columns;
            int column = localIndex % Columns;
            int globalIndex = GetGlobalFrameIndex(sheet, row, column);
            string spriteName = $"Home_animation2_{globalIndex}";
            Rect frameRect = new Rect(
                column * FrameWidth,
                (Rows - 1 - row) * FrameHeight,
                FrameWidth,
                FrameHeight
            );

            GUID spriteId = existingIdsByRect.TryGetValue(
                frameRect,
                out GUID existingId
            )
                ? existingId
                : GUID.Generate();

            SpriteRect spriteRect = new SpriteRect
            {
                name = spriteName,
                rect = frameRect,
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
        ApplyOptimizedSheetSettings(importer);
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
            $"sequential frames {sheet.FirstFrameIndex}-" +
            $"{sheet.FirstFrameIndex + FramesPerSheet - 1} " +
            $"at exact size {SheetWidth}x{SheetHeight}."
        );
    }

    private static void ApplyOptimizedSheetSettings(TextureImporter importer)
    {
        importer.maxTextureSize = AndroidMaxSize;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.compressionQuality = 100;
        importer.crunchedCompression = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;

        TextureImporterPlatformSettings androidSettings =
            importer.GetPlatformTextureSettings("Android");
        androidSettings.name = "Android";
        androidSettings.overridden = true;
        androidSettings.maxTextureSize = AndroidMaxSize;
        androidSettings.format = TextureImporterFormat.ASTC_6x6;
        androidSettings.textureCompression =
            TextureImporterCompression.CompressedHQ;
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
            importer.textureCompression != TextureImporterCompression.CompressedHQ ||
            importer.compressionQuality != 100 ||
            importer.crunchedCompression ||
            !androidSettings.overridden ||
            androidSettings.maxTextureSize != AndroidMaxSize ||
            androidSettings.format != TextureImporterFormat.ASTC_6x6 ||
            androidSettings.textureCompression != TextureImporterCompression.CompressedHQ ||
            androidSettings.compressionQuality != 100 ||
            androidSettings.crunchedCompression;
    }

    private static bool NeedsSourcePreviewUpdate(TextureImporter importer)
    {
        TextureImporterPlatformSettings androidSettings =
            importer.GetPlatformTextureSettings("Android");

        return
            importer.textureType != TextureImporterType.Default ||
            importer.maxTextureSize != SourcePreviewMaxSize ||
            importer.npotScale != TextureImporterNPOTScale.None ||
            importer.mipmapEnabled ||
            importer.filterMode != FilterMode.Bilinear ||
            importer.textureCompression != TextureImporterCompression.Compressed ||
            importer.compressionQuality != 50 ||
            importer.crunchedCompression ||
            androidSettings.overridden;
    }

    private static bool HasCorrectLayout(
        SpriteRect[] spriteRects,
        SheetDefinition sheet
    )
    {
        if (spriteRects == null || spriteRects.Length != FramesPerSheet)
            return false;

        Dictionary<string, SpriteRect> spritesByName = spriteRects
            .GroupBy(spriteRect => spriteRect.name)
            .ToDictionary(group => group.Key, group => group.First());

        for (int localIndex = 0; localIndex < FramesPerSheet; localIndex++)
        {
            int row = localIndex / Columns;
            int column = localIndex % Columns;
            int globalIndex = GetGlobalFrameIndex(sheet, row, column);
            string spriteName = $"Home_animation2_{globalIndex}";

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

    private static int GetGlobalFrameIndex(
        SheetDefinition sheet,
        int row,
        int column
    )
    {
        return sheet.FirstFrameIndex + row * Columns + column;
    }

    private static void ConfigurePopupHomeAnimation()
    {
        Dictionary<string, Sprite> framesByName =
            new Dictionary<string, Sprite>(Sheets.Length * FramesPerSheet);
        foreach (SheetDefinition sheet in Sheets)
        {
            List<Sprite> sheetFrames = AssetDatabase
                .LoadAllAssetsAtPath(sheet.AssetPath)
                .OfType<Sprite>()
                .ToList();

            if (sheetFrames.Count != FramesPerSheet)
                return;

            foreach (Sprite sprite in sheetFrames)
            {
                if (!framesByName.TryAdd(sprite.name, sprite))
                {
                    Debug.LogError(
                        $"[HomeAnimationSpriteSlicer] Duplicate frame name: " +
                        sprite.name
                    );
                    return;
                }
            }
        }

        List<Sprite> allFrames =
            new List<Sprite>(Sheets.Length * FramesPerSheet);
        for (int frameIndex = 0;
             frameIndex < Sheets.Length * FramesPerSheet;
             frameIndex++)
        {
            string frameName = $"Home_animation2_{frameIndex}";
            if (!framesByName.TryGetValue(frameName, out Sprite sprite))
            {
                Debug.LogError(
                    $"[HomeAnimationSpriteSlicer] Missing frame: {frameName}"
                );
                return;
            }

            allFrames.Add(sprite);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PopupHomeAssetPath);
        try
        {
            SpriteAnimator animator = root
                .GetComponentsInChildren<SpriteAnimator>(true)
                .FirstOrDefault(component => component.gameObject.name == "Image");

            if (animator == null)
            {
                Debug.LogError(
                    "[HomeAnimationSpriteSlicer] PopupHome animation object was not found."
                );
                return;
            }

            SerializedObject serializedAnimator = new SerializedObject(animator);
            SerializedProperty frameSource =
                serializedAnimator.FindProperty("frameSource");
            SerializedProperty spriteSheet =
                serializedAnimator.FindProperty("spriteSheet");
            SerializedProperty frames = serializedAnimator.FindProperty("frames");
            SerializedProperty generateGridFrames =
                serializedAnimator.FindProperty("generateGridFrames");
            SerializedProperty targetImage =
                serializedAnimator.FindProperty("targetImage");
            UnityEngine.UI.Image image =
                targetImage.objectReferenceValue as UnityEngine.UI.Image;

            bool isCurrent =
                frameSource.enumValueIndex ==
                    (int)SpriteAnimatorFrameSource.FrameByFrame &&
                spriteSheet.objectReferenceValue == null &&
                !generateGridFrames.boolValue &&
                frames.arraySize == allFrames.Count &&
                image != null &&
                image.preserveAspect &&
                image.rectTransform.sizeDelta ==
                    new Vector2(FrameWidth, FrameHeight);

            if (isCurrent)
            {
                for (int i = 0; i < allFrames.Count; i++)
                {
                    if (frames.GetArrayElementAtIndex(i).objectReferenceValue !=
                        allFrames[i])
                    {
                        isCurrent = false;
                        break;
                    }
                }
            }

            if (isCurrent)
                return;

            frameSource.enumValueIndex =
                (int)SpriteAnimatorFrameSource.FrameByFrame;
            spriteSheet.objectReferenceValue = null;
            generateGridFrames.boolValue = false;
            frames.arraySize = allFrames.Count;
            for (int i = 0; i < allFrames.Count; i++)
            {
                frames.GetArrayElementAtIndex(i).objectReferenceValue =
                    allFrames[i];
            }

            serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

            if (image != null)
            {
                image.sprite = allFrames[0];
                image.preserveAspect = true;
                image.rectTransform.sizeDelta =
                    new Vector2(FrameWidth, FrameHeight);
                EditorUtility.SetDirty(image);
                EditorUtility.SetDirty(image.rectTransform);
            }

            EditorUtility.SetDirty(animator);
            PrefabUtility.SaveAsPrefabAsset(root, PopupHomeAssetPath);
            Debug.Log(
                $"[HomeAnimationSpriteSlicer] PopupHome now uses " +
                $"Home_animation2 with {allFrames.Count} frames."
            );
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private readonly struct SheetDefinition
    {
        public readonly string AssetPath;
        public readonly int FirstFrameIndex;

        public SheetDefinition(string assetPath, int firstFrameIndex)
        {
            AssetPath = assetPath;
            FirstFrameIndex = firstFrameIndex;
        }
    }
}
