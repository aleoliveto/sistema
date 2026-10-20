using UnityEditor;
using UnityEngine;

/// <summary>
/// The HUD's painted pieces (Resources/UI/Art, made by Tools/ArtGen/ui.py) are drawn at twice their
/// screen size and scaled down by UI Toolkit: plain textures with mipmaps, clamped, uncompressed so the
/// gold edges stay clean on any monitor.
/// </summary>
public sealed class UiArtImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/UI/Art/")) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Default;
        t.mipmapEnabled = true;
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Trilinear;
        t.sRGBTexture = true;
        t.alphaIsTransparency = true;
        t.npotScale = TextureImporterNPOTScale.None;
        t.maxTextureSize = 512;
        t.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
