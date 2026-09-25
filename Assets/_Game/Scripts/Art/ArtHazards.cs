using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    public enum BiomeHazard { None, Icicles, Bats, Lasers, Meteors }

    /// <summary>Sprites for the biome-specific hazards: icicles (snow), bats (night), laser sweepers (neon), meteors (space).</summary>
    public sealed partial class Art
    {
        public Sprite[] Icicles;        // three lengths, pivot at the base (top)
        public Sprite[] BatFrames;      // wings up / down
        public Sprite[] LaserBeam;      // 16x5 tiling frames
        public Sprite LaserEmitter;
        public Sprite Meteor;

        public static readonly Color32 LaserColor = Pal.Hex("ff3fd8");
        public static readonly Color32 IceColor = Pal.Hex("bfe9ff");
        public static readonly Color32 MeteorHot = Pal.Hex("ff8a2a");

        void BuildHazards()
        {
            BuildIcicles();
            BuildBats();
            BuildLaser();
            BuildMeteor();
        }

        void BuildIcicles()
        {
            Color32 highlight = Pal.White, baseCol = IceColor, shade = Pal.Hex("7fc3e6"), snow = Pal.Hex("f4f8ff");
            int[] lengths = { 32, 44, 56 };
            Icicles = new Sprite[lengths.Length];
            for (int i = 0; i < lengths.Length; i++)
            {
                int h = lengths[i];
                const int w = 9;
                var pc = new PixelCanvas(w, h);
                for (int y = 0; y < h; y++)
                {
                    float hw = 4.5f * Mathf.Pow(1f - y / (h - 1f), 0.9f);
                    for (int x = 0; x < w; x++)
                    {
                        float dx = x + 0.5f - 4.5f;
                        if (Mathf.Abs(dx) > hw) continue;
                        float u = hw > 0.01f ? dx / hw : 0f;
                        Color32 c = y < 2 ? snow : u < -0.35f ? highlight : u > 0.45f ? shade : baseCol;
                        if (y > 3 && y % 9 == 0 && Mathf.Abs(u) < 0.8f) c = shade; // growth rings
                        pc.Set(x, y, c);
                    }
                }
                pc.Outline(Pal.Ink, false);
                Icicles[i] = pc.ToSprite(TopCenter);
            }
        }

        static readonly string[] BatUp =
        {
            "KK..........KK",
            "KwK........KwK",
            "KwwK.KKKK.KwwK",
            ".KwwKbbbbKwwK.",
            "..KwKbRbRKwK..",
            "...KKbbbbKK...",
            ".....KbbK.....",
            "......KK......",
        };

        static readonly string[] BatDown =
        {
            "..............",
            "....KKKKKK....",
            "...KKbbbbKK...",
            ".KKwKbRbRKwKK.",
            "KwwwKbbbbKwwwK",
            "KwwK.KbbK.KwwK",
            "KwK...KK...KwK",
            "KK..........KK",
        };

        void BuildBats()
        {
            var pal = new Dictionary<char, Color32>
            {
                { 'K', Pal.Ink }, { 'b', Pal.Hex("5a4480") }, { 'w', Pal.Hex("9a7cd0") }, { 'R', Pal.Red },
            };
            BatFrames = new[] { PixelCanvas.FromMap(BatUp, pal).ToSprite(Center), PixelCanvas.FromMap(BatDown, pal).ToSprite(Center) };
        }

        void BuildLaser()
        {
            Color32 core = Pal.White, main = LaserColor;
            LaserBeam = new Sprite[2];
            for (int f = 0; f < 2; f++)
            {
                var pc = new PixelCanvas(16, 5);
                for (int x = 0; x < 16; x++)
                {
                    pc.Set(x, 0, Pal.Alpha(main, (byte)(f == 0 ? 70 : 110)));
                    pc.Set(x, 1, main);
                    pc.Set(x, 2, (x + f * 3) % 7 == 0 ? main : core);
                    pc.Set(x, 3, main);
                    pc.Set(x, 4, Pal.Alpha(main, (byte)(f == 0 ? 110 : 70)));
                }
                LaserBeam[f] = pc.ToSprite(Center);
            }

            var map = new[]
            {
                "..KKKKKK..",
                ".KllllllK.",
                "KlmmmmmmdK",
                "KmmCCCCmdK",
                "KmmCWWCmdK",
                "KdmmmmmmdK",
                ".KddddddK.",
                "..KKKKKK..",
            };
            var pal = new Dictionary<char, Color32>
            {
                { 'K', Pal.Ink }, { 'l', Pal.Hex("9a86bd") }, { 'm', Pal.Hex("5d4a80") }, { 'd', Pal.Hex("382a55") },
                { 'C', main }, { 'W', core },
            };
            LaserEmitter = PixelCanvas.FromMap(map, pal).ToSprite(Center);
        }

        void BuildMeteor()
        {
            const int w = 16, h = 14;
            Color32 light = Pal.Hex("b08a70"), baseCol = Pal.Hex("8a6a5a"), dark = Pal.Hex("6a4f45"), crater = Pal.Hex("574038");
            var pc = new PixelCanvas(w, h);
            var craters = new[] { new Vector2(9f, 5f), new Vector2(6f, 9f), new Vector2(11.5f, 9.5f) };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float lump = 6.3f + 0.7f * Mathf.Sin(Mathf.Atan2(p.y - 7f, p.x - 8f) * 5f); // lumpy outline
                float d = Vector2.Distance(p, new Vector2(8f, 7f));
                if (d > lump) continue;
                float l = -(p.x - 8f) / lump * 0.5f - (p.y - 7f) / lump * 0.6f;
                Color32 c = l > 0.3f ? light : l < -0.3f ? dark : baseCol;
                foreach (var cr in craters)
                    if (Vector2.Distance(p, cr) < 1.4f) c = crater;
                if (p.x < 8f && d > lump - 1.3f) c = MeteorHot; // glowing leading edge (it flies left)
                pc.Set(x, y, c);
            }
            pc.Outline(Pal.Ink, false);
            Meteor = pc.ToSprite(Center);
        }
    }
}
