using UnityEditor;
using UnityEngine;

// Preserve the supplied PNG artwork and its alpha; bounds.json only trims the sprite rectangle.
public class WesternArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        bool stigmaIcon = assetPath.StartsWith("Assets/Resources/Stigmas/Icons/");
        if (!stigmaIcon && !assetPath.StartsWith("Assets/Resources/Western/") && !assetPath.StartsWith("Assets/Resources/Combat/")) return;
        var importer = (TextureImporter)assetImporter;
        // Catalog artwork is converted at runtime; direct prefab references need imported Sprite subassets.
        bool robeManSprite = assetPath == "Assets/Resources/Combat/Enemy_RobeMan_temp.png";
        bool importedSprite = stigmaIcon || robeManSprite;
        importer.textureType = importedSprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        if (importedSprite) importer.spriteImportMode = SpriteImportMode.Single;
        if (robeManSprite)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(.5f, 0);
            importer.SetTextureSettings(settings);
        }
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = assetPath.StartsWith("Assets/Resources/Combat/");
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
