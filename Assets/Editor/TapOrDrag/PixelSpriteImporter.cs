using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Percas.TapOrDrag.Editor
{
    /// <summary>
    /// Import rules for everything under Assets/Arts/Sprites:
    /// point filter, no compression, no mips, PPU 12 (108x216 px = 9x18 units),
    /// and pivots that sit exactly on a pixel boundary so sprites never land on half pixels.
    /// </summary>
    public class PixelSpriteImporter : AssetPostprocessor
    {
        public const string SpriteFolder = "Assets/Arts/Sprites";
        public const int PixelsPerUnit = 12;

        // Pivots in pixels, measured from the bottom-left corner.
        static readonly Dictionary<string, Vector2Int> CustomPivots = new Dictionary<string, Vector2Int>
        {
            { "bird_dash_trail", new Vector2Int(29, 11) },
        };

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteFolder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = PivotFor(importer, Path.GetFileNameWithoutExtension(assetPath));
            importer.SetTextureSettings(settings);
        }

        static Vector2 PivotFor(TextureImporter importer, string name)
        {
            importer.GetSourceTextureWidthAndHeight(out var w, out var h);
            if (w <= 0 || h <= 0) return new Vector2(0.5f, 0.5f);

            if (CustomPivots.TryGetValue(name, out var p)) return new Vector2((float)p.x / w, (float)p.y / h);
            // Pipe pieces and hills stack from their bottom edge; clouds from their corner.
            if (name.StartsWith("pipe_") || name.StartsWith("bg_hills")) return new Vector2((w / 2) / (float)w, 0f);
            if (name.StartsWith("cloud_")) return Vector2.zero;
            return new Vector2((w / 2) / (float)w, (h / 2) / (float)h);
        }
    }
}
