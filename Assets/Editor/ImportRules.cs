using UnityEditor;
using UnityEngine;

namespace VoidStunts.EditorTools
{
    /// <summary>Import settings by file name: *_n normal maps, *_m / noise masks are linear data, *_a colour (sRGB). ASTC on iOS.</summary>
    public class ImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Textures/") && !assetPath.Contains("/Icons/")) return;
            var ti = (TextureImporter)assetImporter; string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            ti.mipmapEnabled = !assetPath.Contains("/Icons/"); ti.anisoLevel = 4; ti.wrapMode = TextureWrapMode.Repeat; ti.filterMode = FilterMode.Trilinear; ti.isReadable = false;
            var ios = new TextureImporterPlatformSettings { name = "iPhone", overridden = true, maxTextureSize = 2048, compressionQuality = 60 };
            if (assetPath.Contains("/Icons/")) { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.npotScale = TextureImporterNPOTScale.None; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.isReadable = true; return; }
            if (n.EndsWith("_n")) { ti.textureType = TextureImporterType.NormalMap; ios.format = TextureImporterFormat.ASTC_4x4; }
            else if (n.EndsWith("_m") || n.StartsWith("noise")) { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; ios.format = TextureImporterFormat.ASTC_5x5; }
            else
            {
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ios.format = TextureImporterFormat.ASTC_6x6;
                if (n.StartsWith("leaves") || n.StartsWith("livery") || n.StartsWith("board")) { ti.alphaIsTransparency = true; ti.alphaSource = TextureImporterAlphaSource.FromInput; ios.format = TextureImporterFormat.ASTC_5x5; }
                if (n.StartsWith("livery") || n.StartsWith("board")) ti.wrapMode = TextureWrapMode.Clamp;
            }
            ti.SetPlatformTextureSettings(ios);
        }
    }
}
