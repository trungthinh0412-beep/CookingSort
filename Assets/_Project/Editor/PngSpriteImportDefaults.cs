using System;
using UnityEditor;

internal sealed class PngSpriteImportDefaults : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        // Apply defaults only on first import so later Inspector edits persist.
        if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            assetImporter.importSettingsMissing == false)
            return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
    }
}
