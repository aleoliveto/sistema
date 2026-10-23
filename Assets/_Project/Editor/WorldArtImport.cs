using UnityEditor;
using UnityEngine;

/// <summary>
/// The painted pieces cut from the ChatGPT world sheets (Tools/ArtGen/cut_world.py): furniture, cars,
/// doors and the minigame pieces. Seen at every zoom, so mipmaps and a clean transparent edge.
/// </summary>
public sealed class WorldArtImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        bool ours = path.Contains("/Resources/Props/w_") || path.Contains("/Resources/Vehicles/wcar") ||
                    path.Contains("/Resources/Doors/") || path.Contains("/Resources/UI/Minigame/");
        if (!ours) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Default;
        t.alphaIsTransparency = true;
        t.mipmapEnabled = !path.Contains("/UI/");
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Trilinear;
        t.npotScale = TextureImporterNPOTScale.None;
        t.maxTextureSize = 1024;
        t.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}
