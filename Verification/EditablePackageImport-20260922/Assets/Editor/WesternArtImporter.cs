using UnityEditor;
using UnityEngine;

// Preserve the supplied PNG artwork and its alpha; bounds.json only trims the sprite rectangle.
public class WesternArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/Western/") && !assetPath.StartsWith("Assets/Resources/Combat/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
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
