using UnityEditor;
using UnityEngine;

// Applies pixel-art import settings to the game's art automatically,
// so new sprites never come in blurry.
public class ArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        bool ui = assetPath.StartsWith("Assets/Resources/UI/");
        if (!ui && !assetPath.StartsWith("Assets/Art/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.isReadable = assetPath.Contains("/font"); // the game reads the font's pixels

        // "_9s" sprites stretch from the middle and keep their corners (buttons, panels).
        if (assetPath.Contains("_9s")) importer.spriteBorder = new Vector4(5, 5, 5, 5);

        // Things that stand on the ground pivot at their feet, so sorting by Y
        // makes you walk in front of or behind them correctly.
        bool standsOnGround = assetPath.Contains("/Props/") || assetPath.Contains("/Characters/");
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)(standsOnGround ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
        importer.SetTextureSettings(settings);
    }
}
