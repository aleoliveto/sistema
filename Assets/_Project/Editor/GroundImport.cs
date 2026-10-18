using UnityEditor;
using UnityEngine;

/// <summary>
/// The baked ground materials (Resources/Ground, made by Tools/ArtGen/ground.py) repeat across the
/// whole map and are seen from far away: plain textures, repeating, with mipmaps.
/// </summary>
public sealed class GroundImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Ground/")) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Default;
        t.mipmapEnabled = true;
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 4;
        t.sRGBTexture = true;
        t.alphaSource = TextureImporterAlphaSource.None;
        t.maxTextureSize = 1024;
        t.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
