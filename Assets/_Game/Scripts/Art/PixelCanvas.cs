using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    public static class Pal
    {
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        public static readonly Color32 Ink = Hex("2a1633");
        public static readonly Color32 Shadow = Hex("120a1f");
        public static readonly Color32 White = Hex("ffffff");
        public static readonly Color32 Orange = Hex("ff8a2a");
        public static readonly Color32 OrangeLight = Hex("ffc46b");
        public static readonly Color32 Gold = Hex("ffd23f");
        public static readonly Color32 Red = Hex("ff4d6d");
        public static readonly Color32 Rose = Hex("ff5f8f");
        public static readonly Color32 Lilac = Hex("c9b6f0");

        public static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        public static Color32 Alpha(Color32 c, byte a)
        {
            c.a = a;
            return c;
        }

        /// <summary>k &lt; 1 darkens, k &gt; 1 moves towards white.</summary>
        public static Color32 Shade(Color32 c, float k)
        {
            if (k <= 1f)
                return new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), c.a);
            float t = Mathf.Clamp01(k - 1f);
            return new Color32(
                (byte)Mathf.Lerp(c.r, 255f, t),
                (byte)Mathf.Lerp(c.g, 255f, t),
                (byte)Mathf.Lerp(c.b, 255f, t), c.a);
        }
    }

    /// <summary>Small software canvas used to paint pixel art into textures. Coordinates are top-left based.</summary>
    public sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;
        readonly Color32[] pixels;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
        }

        int Index(int x, int y) => (Height - 1 - y) * Width + x;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public void Set(int x, int y, Color32 c)
        {
            if (InBounds(x, y)) pixels[Index(x, y)] = c;
        }

        public Color32 Get(int x, int y) => InBounds(x, y) ? pixels[Index(x, y)] : Pal.Clear;
        public bool Opaque(int x, int y) => Get(x, y).a > 0;

        public void Stamp(string[] map, IReadOnlyDictionary<char, Color32> palette, int ox = 0, int oy = 0)
        {
            for (int y = 0; y < map.Length; y++)
            for (int x = 0; x < map[y].Length; x++)
            {
                char ch = map[y][x];
                if (ch == '.' || ch == ' ') continue;
                if (palette.TryGetValue(ch, out var c)) Set(ox + x, oy + y, c);
            }
        }

        public void Outline(Color32 color, bool diagonals)
        {
            var src = (Color32[])pixels.Clone();
            bool SrcOpaque(int x, int y) => InBounds(x, y) && src[Index(x, y)].a > 0;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (SrcOpaque(x, y)) continue;
                bool edge = SrcOpaque(x - 1, y) || SrcOpaque(x + 1, y) || SrcOpaque(x, y - 1) || SrcOpaque(x, y + 1);
                if (!edge && diagonals)
                    edge = SrcOpaque(x - 1, y - 1) || SrcOpaque(x + 1, y - 1) || SrcOpaque(x - 1, y + 1) || SrcOpaque(x + 1, y + 1);
                if (edge) Set(x, y, color);
            }
        }

        public void DropShadow(Color32 color, int dx, int dy)
        {
            var src = (Color32[])pixels.Clone();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (src[Index(x, y)].a > 0) continue;
                int sx = x - dx, sy = y - dy;
                if (InBounds(sx, sy) && src[Index(sx, sy)].a > 0) Set(x, y, color);
            }
        }

        public PixelCanvas Silhouette(Color32 color)
        {
            var result = new PixelCanvas(Width, Height);
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].a > 0) result.pixels[i] = color;
            return result;
        }

        public Texture2D ToTexture(FilterMode filter = FilterMode.Point)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        public Sprite ToSprite(Vector2 pivot, float ppu = World.PPU, FilterMode filter = FilterMode.Point, Vector4 border = default)
        {
            var tex = ToTexture(filter);
            return Sprite.Create(tex, new Rect(0, 0, Width, Height), pivot, ppu, 0, SpriteMeshType.FullRect, border);
        }

        public static PixelCanvas FromMap(string[] map, IReadOnlyDictionary<char, Color32> palette, int pad = 0)
        {
            int w = 0;
            foreach (var row in map) w = Mathf.Max(w, row.Length);
            var pc = new PixelCanvas(w + pad * 2, map.Length + pad * 2);
            pc.Stamp(map, palette, pad, pad);
            return pc;
        }
    }
}
