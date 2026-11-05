using UnityEditor;

namespace FlowerEscape.EditorTools
{
    /// <summary>Import settings for every sprite exported from Figma (exported at 2x).</summary>
    public class FlowerEscapeSpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FlowerEscapeUIBuilder.SpritesFolder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 200f; // 2x export
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            ti.filterMode = UnityEngine.FilterMode.Bilinear;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
