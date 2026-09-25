using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Coins, meta-UI icons and the biome sets (sky palette + hills + sun/moon/planet + tints).</summary>
    public sealed partial class Art
    {
        public sealed class BiomeArt
        {
            public string Name;
            public string[] Sky;
            public Sprite HillsFar, HillsMid, Sun;
            public Color SunGlow, CloudTint, GroundTint;
            public float StarAlpha;
            public bool Snow; // snowfall layers in the background
            public BiomeHazard Hazard; // obstacle that only appears in this biome
        }

        public Sprite[] Coin;          // pickup frames (a bone in the dog version)
        public Sprite IconCoin, IconCheck;
        public Sprite[][] PortalBeam;  // [0 = flips gravity up, 1 = back down][frame], chevrons show the new "down"
        public static readonly Color32 PortalMain = Pal.Hex("b58cff");
        public static readonly Color32 PortalCore = Pal.Hex("f2e8ff");
        public static readonly Color32 PortalDeep = Pal.Hex("6a3dcc");
        public Sprite[,] SwitchBlock;  // [colour 0 gold / 1 blue, 0 solid / 1 ghost], 16x16 tile
        public Sprite[] SwitchIcon;    // [colour] small HUD badge
        public static readonly Color32[] SwitchMain = { Pal.Hex("ffc233"), Pal.Hex("4d7cff") };
        public static readonly Color32[] SwitchLight = { Pal.Hex("fff0a0"), Pal.Hex("a8c4ff") };
        public static readonly Color32[] SwitchDark = { Pal.Hex("c77a12"), Pal.Hex("2a45b8") };
        public static readonly string[] SwitchNames = { "GOLD", "BLUE" };
        public BiomeArt[] Biomes;      // cycled during a run: dusk -> night -> neon -> space

        void BuildExtras()
        {
            BuildCoins();
            BuildPortal();
            BuildSwitchBlocks();
            BuildHazards();
            BuildShooterArt();
            BuildWeaponArt();
            BuildMetaIcons();
            BuildBiomes();
        }

        // ---------------------------------------------------------------- Bones (the collectible currency)

        /// <summary>
        /// Golden bone pickup (kept in the Coin fields so the economy code is unchanged): a shaft with two round,
        /// sphere-shaded knobs at each end. Gold so it never reads as a bone pillar. Coin.cs wobbles it.
        /// </summary>
        void BuildCoins()
        {
            const int w = 18, h = 10;
            Color32 hi = Pal.Hex("fff6c0"), light = Pal.Hex("ffe066"), baseCol = Pal.Gold, shade = Pal.Hex("e0a624"), deep = Pal.Hex("b97d14");
            var knobs = new[] { new Vector2(3.2f, 3f), new Vector2(3.2f, 7f), new Vector2(14.8f, 3f), new Vector2(14.8f, 7f) };
            const float r = 2.6f;
            var pc = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                int knob = -1;
                for (int i = 0; i < knobs.Length; i++)
                    if (Vector2.Distance(p, knobs[i]) <= r) knob = i;
                bool shaft = p.x > 3.5f && p.x < 14.5f && p.y > 3.4f && p.y < 6.6f;
                if (knob < 0 && !shaft) continue;
                Color32 c;
                if (knob >= 0)
                {
                    float dx = (p.x - knobs[knob].x) / r, dy = (p.y - knobs[knob].y) / r;
                    float l = -dx * 0.7f - dy * 0.7f;
                    c = l > 0.55f ? hi : l > 0.15f ? light : l < -0.55f ? deep : l < -0.15f ? shade : baseCol;
                }
                else c = p.y < 4.4f ? light : p.y > 5.6f ? shade : baseCol;
                pc.Set(x, y, c);
            }
            pc.Outline(Pal.Ink, false);
            Coin = new[] { pc.ToSprite(Center) };
            IconCoin = Coin[0];
        }

        void BuildMetaIcons()
        {
            IconCheck = OutlinedIcon(new[] { "......w", ".....ww", "w...ww.", "ww.ww..", ".www...", "..w...." }, Pal.Hex("7dff9a"));
        }

        // ---------------------------------------------------------------- Gravity portal

        /// <summary>16x32 tiling field with scrolling chevrons. Up-chevrons: gravity is about to point up; down-chevrons: back to normal.</summary>
        void BuildPortal()
        {
            PortalBeam = new Sprite[2][];
            for (int dir = 0; dir < 2; dir++)
            {
                PortalBeam[dir] = new Sprite[4];
                for (int f = 0; f < 4; f++)
                {
                    var pc = new PixelCanvas(16, 32);
                    for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        Color32 c = Pal.Alpha(PortalDeep, 70);
                        if (x == 0 || x == 15) c = Pal.Alpha(PortalMain, (byte)((y + f * 2) % 6 < 4 ? 220 : 90));
                        else if (x == 1 || x == 14) c = Pal.Alpha(PortalMain, 110);
                        pc.Set(x, y, c);
                    }
                    int shift = dir == 0 ? -f * 4 : f * 4; // chevrons travel towards the new "down"
                    for (int copy = 0; copy < 2; copy++)
                    for (int dy = 0; dy < 5; dy++)
                    {
                        int row = dir == 0 ? dy : 4 - dy;
                        int y = ((copy * 16 + 5 + row + shift) % 32 + 32) % 32;
                        int left = 7 - dy, right = 8 + dy;
                        foreach (int x in new[] { left, left + 1, right - 1, right })
                            if (x > 1 && x < 14) pc.Set(x, y, dy == 0 ? Pal.White : PortalCore);
                    }
                    PortalBeam[dir][f] = pc.ToSprite(Center);
                }
            }
        }

        // ---------------------------------------------------------------- Switch blocks

        /// <summary>Solid: bevelled block with a riveted plate. Ghost: dashed outline only, so it reads as "passable".</summary>
        void BuildSwitchBlocks()
        {
            SwitchBlock = new Sprite[2, 2];
            SwitchIcon = new Sprite[2];
            for (int c = 0; c < 2; c++)
            {
                Color32 main = SwitchMain[c], light = SwitchLight[c], dark = SwitchDark[c];
                var solid = new PixelCanvas(16, 16);
                var ghost = new PixelCanvas(16, 16);
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool edge = x == 0 || y == 0 || x == 15 || y == 15;
                    Color32 s;
                    if (edge) s = Pal.Ink;
                    else if (x == 1 || y == 1) s = light;
                    else if (x == 14 || y == 14) s = dark;
                    else if (x >= 5 && x <= 10 && y >= 5 && y <= 10) s = (x == 5 || y == 5) ? dark : (x == 10 || y == 10) ? light : Pal.Shade(main, 1.08f);
                    else s = main;
                    if ((x == 3 || x == 12) && (y == 3 || y == 12)) s = light; // rivets
                    solid.Set(x, y, s);

                    bool dash = edge && ((x + y) & 3) < 2;
                    ghost.Set(x, y, dash ? Pal.Alpha(main, 210) : Pal.Alpha(main, 28));
                }
                SwitchBlock[c, 0] = solid.ToSprite(Center);
                SwitchBlock[c, 1] = ghost.ToSprite(Center);

                var icon = new PixelCanvas(8, 8);
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    icon.Set(x, y, x == 0 || y == 0 || x == 7 || y == 7 ? Pal.Ink : x == 1 || y == 1 ? light : x == 6 || y == 6 ? dark : main);
                SwitchIcon[c] = icon.ToSprite(Center);
            }
        }

        // ---------------------------------------------------------------- Biomes

        void BuildBiomes()
        {
            var moon = MakeMoon();
            var planet = MakePlanet();
            Biomes = new[]
            {
                new BiomeArt
                {
                    Name = "DUSK", Sky = SkyHex, HillsFar = HillsFar, HillsMid = HillsMid, Sun = Sun,
                    SunGlow = new Color(1f, 0.45f, 0.6f, 0.35f), CloudTint = Color.white, GroundTint = Color.white, StarAlpha = 1f,
                },
                new BiomeArt
                {
                    Name = "SNOW PEAKS",
                    Sky = new[] { "1b2b4a", "25406a", "31558a", "3f6aa3", "5283b8", "6f9ecb", "92b9da", "b6d3e8", "d8ebf5" },
                    HillsFar = MakeSnowMountains(160, 72, 20f,
                        new[] { new Vector3(30f, 2f, 0.3f), new Vector3(12f, 5f, 1f), new Vector3(5f, 11f, 2.1f) },
                        Pal.Hex("5a6f9a"), Pal.Hex("465a82"), Pal.Hex("f4f8ff"), Pal.Hex("c8d8f0")),
                    HillsMid = MakeHills(128, 56, 22f, new[] { new Vector3(8f, 2f, 1.4f), new Vector3(4f, 6f, 0.2f) },
                        Pal.Hex("2d4668"), Pal.Hex("e8f2ff"), 13),
                    Sun = MakeWinterSun(),
                    SunGlow = new Color(1f, 0.95f, 0.8f, 0.3f), CloudTint = new Color(1f, 1f, 1f, 0.9f),
                    GroundTint = new Color(0.8f, 0.9f, 1f), StarAlpha = 0.2f, Snow = true, Hazard = BiomeHazard.Icicles,
                },
                new BiomeArt
                {
                    Name = "NIGHT",
                    Sky = new[] { "05060f", "0b1030", "141c4a", "1f2a63", "2b3a7a", "3a4c8f", "4b5fa0", "5a6fb0", "6a7fbd" },
                    HillsFar = MakeHills(160, 72, 38f, new[] { new Vector3(10f, 1f, 0.3f), new Vector3(6f, 3f, 1.1f), new Vector3(3f, 7f, 2f) },
                        Pal.Hex("1c2550"), Pal.Hex("34407a"), 0),
                    HillsMid = MakeHills(128, 56, 24f, new[] { new Vector3(9f, 2f, 2f), new Vector3(4f, 5f, 0.5f) },
                        Pal.Hex("111838"), Pal.Hex("243060"), 7),
                    Sun = moon,
                    SunGlow = new Color(0.7f, 0.8f, 1f, 0.25f), CloudTint = new Color(0.45f, 0.5f, 0.8f, 0.8f),
                    GroundTint = new Color(0.6f, 0.65f, 0.9f), StarAlpha = 1f, Hazard = BiomeHazard.Bats,
                },
                new BiomeArt
                {
                    Name = "NEON CITY",
                    Sky = new[] { "12001f", "2a0038", "470052", "650066", "85007a", "a3108a", "c01f93", "d8329b", "ee4aa3" },
                    HillsFar = MakeSkyline(160, 72, 21, Pal.Hex("2a0a45"), Pal.Hex("5a2a8a"),
                        new[] { Pal.Hex("3ff0ff"), Pal.Hex("ff5fd8"), Pal.Hex("ffd23f") }, 22, 60, 0.35f),
                    HillsMid = MakeSkyline(128, 56, 43, Pal.Hex("170428"), Pal.Hex("3d1560"),
                        new[] { Pal.Hex("ff5fd8"), Pal.Hex("3ff0ff") }, 14, 40, 0.25f),
                    Sun = Sun,
                    SunGlow = new Color(1f, 0.3f, 0.9f, 0.4f), CloudTint = new Color(1f, 0.6f, 1f, 0.5f),
                    GroundTint = new Color(0.85f, 0.6f, 1f), StarAlpha = 0.4f, Hazard = BiomeHazard.Lasers,
                },
                new BiomeArt
                {
                    Name = "SPACE",
                    Sky = new[] { "000000", "05010d", "0b0418", "120622", "190a2e", "1f0d38", "240f40", "2a1248", "301550" },
                    HillsFar = MakeHills(160, 72, 30f,
                        new[] { new Vector3(8f, 2f, 0.4f), new Vector3(5f, 5f, 1.2f), new Vector3(3f, 11f, 2.2f), new Vector3(2f, 17f, 0.7f) },
                        Pal.Hex("3a3450"), Pal.Hex("6a6290"), 0),
                    HillsMid = MakeHills(128, 56, 20f, new[] { new Vector3(6f, 3f, 1f), new Vector3(4f, 7f, 2f), new Vector3(2f, 13f, 0.3f) },
                        Pal.Hex("231d35"), Pal.Hex("3e3656"), 0),
                    Sun = planet,
                    SunGlow = new Color(1f, 0.7f, 0.4f, 0.2f), CloudTint = new Color(1f, 1f, 1f, 0f),
                    GroundTint = new Color(0.55f, 0.5f, 0.75f), StarAlpha = 1f, Hazard = BiomeHazard.Meteors,
                },
            };
        }

        /// <summary>Sharp peaks (1 - |sin|, integer frequencies so it tiles) with snow caps and left-lit / right-shaded slopes.</summary>
        static Sprite MakeSnowMountains(int w, int h, float baseHeight, Vector3[] waves, Color32 rock, Color32 rockShade, Color32 snow, Color32 snowShade)
        {
            var heights = new int[w];
            for (int x = 0; x < w; x++)
            {
                float v = baseHeight;
                foreach (var wave in waves) v += wave.x * (1f - Mathf.Abs(Mathf.Sin(Mathf.PI * wave.y * x / w + wave.z)));
                heights[x] = Mathf.Clamp(Mathf.RoundToInt(v), 1, h);
            }
            var pc = new PixelCanvas(w, h);
            for (int x = 0; x < w; x++)
            {
                bool lit = heights[(x + 1) % w] >= heights[x];
                int cap = 4 + (x * 7 % 5) + heights[x] / 14; // jagged snow line, deeper on tall peaks
                for (int yb = 0; yb < heights[x]; yb++)
                {
                    int depth = heights[x] - 1 - yb;
                    Color32 c = depth < cap ? (lit ? snow : snowShade) : (lit ? rock : rockShade);
                    pc.Set(x, h - 1 - yb, c);
                }
            }
            return pc.ToSprite(BottomCenter);
        }

        static Sprite MakeWinterSun()
        {
            var pc = new PixelCanvas(30, 30);
            Color32 top = Pal.Hex("fffbe8"), bottom = Pal.Hex("ffe2a8");
            for (int y = 0; y < 30; y++)
            for (int x = 0; x < 30; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(15f, 15f));
                if (d <= 12f) pc.Set(x, y, Color32.Lerp(top, bottom, Mathf.Floor(y / 30f * 5f) / 4f));
                else if (d <= 13.5f && (x + y) % 2 == 0) pc.Set(x, y, Pal.Alpha(top, 120)); // dithered halo
            }
            return pc.ToSprite(Center);
        }

        static Sprite MakeMoon()
        {
            var pc = new PixelCanvas(28, 28);
            Color32 baseCol = Pal.Hex("f2ecd0"), shade = Pal.Hex("d8cfae"), crater = Pal.Hex("c9bf9c");
            Vector2[] craters = { new Vector2(10f, 10f), new Vector2(17f, 15f), new Vector2(11f, 18f), new Vector2(18f, 8f) };
            float[] craterR = { 2.2f, 2.8f, 1.6f, 1.3f };
            for (int y = 0; y < 28; y++)
            for (int x = 0; x < 28; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, new Vector2(14f, 14f));
                if (d > 12f) continue;
                Color32 c = p.x + p.y > 32f ? shade : baseCol;
                for (int i = 0; i < craters.Length; i++)
                    if (Vector2.Distance(p, craters[i]) < craterR[i]) c = crater;
                pc.Set(x, y, c);
            }
            return pc.ToSprite(Center);
        }

        static Sprite MakePlanet()
        {
            var pc = new PixelCanvas(48, 32);
            Color32 top = Pal.Hex("ffb347"), bottom = Pal.Hex("e0603a"), ring = Pal.Hex("8fe8ff"), ringShade = Pal.Hex("5fb8d6");
            var c0 = new Vector2(24f, 16f);
            bool RingAt(float dx, float dy) => Mathf.Abs(dx * dx / 400f + dy * dy / 25f - 1f) < 0.2f;
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 48; x++)
            {
                float dx = x + 0.5f - c0.x, dy = y + 0.5f - c0.y;
                bool body = dx * dx + dy * dy <= 100f;
                bool ringHere = RingAt(dx, dy);
                if (ringHere && (dy > 0f || !body)) pc.Set(x, y, dx > 0f ? ringShade : ring); // ring passes in front of the lower half
                else if (body)
                {
                    float t = Mathf.Floor((dy + 10f) / 20f * 5f) / 4f;
                    pc.Set(x, y, Color32.Lerp(top, bottom, Mathf.Clamp01(t)));
                }
            }
            return pc.ToSprite(Center);
        }

        /// <summary>City skyline silhouette with random lit windows and antennas. Buildings run edge to edge so it tiles.</summary>
        static Sprite MakeSkyline(int w, int h, int seed, Color32 fill, Color32 rim, Color32[] windows, int minH, int maxH, float windowChance)
        {
            var rng = new System.Random(seed);
            var pc = new PixelCanvas(w, h);
            int x = 0;
            while (x < w)
            {
                int bw = 6 + rng.Next(9);
                if (w - x - bw < 6) bw = w - x;
                int bh = minH + rng.Next(maxH - minH + 1);
                for (int xx = x; xx < x + bw; xx++)
                for (int yb = 0; yb < bh; yb++)
                    pc.Set(xx, h - 1 - yb, yb == bh - 1 || xx == x ? rim : fill);
                for (int wy = 3; wy < bh - 2; wy += 3)
                for (int wx = x + 2; wx < x + bw - 1; wx += 2)
                    if (rng.NextDouble() < windowChance) pc.Set(wx, h - 1 - wy, windows[rng.Next(windows.Length)]);
                if (rng.NextDouble() < 0.25 && bh + 5 < h)
                {
                    int ax = x + bw / 2, len = 3 + rng.Next(3);
                    for (int i = 0; i < len; i++) pc.Set(ax, h - 1 - bh - i, rim);
                    pc.Set(ax, h - 1 - bh - len, windows[0]);
                }
                x += bw;
            }
            return pc.ToSprite(BottomCenter);
        }
    }
}
