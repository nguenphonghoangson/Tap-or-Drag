using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Every sprite in the prototype is painted at startup from ASCII pixel maps or small procedural rules,
    /// so the project needs no imported art. Palette: dusk synthwave sky, orange pipes, cyan/pink neon gates.
    /// </summary>
    public sealed class Art
    {
        public const float UiPPU = 100f / 3f;

        public static readonly Color32[] GateCore = { Pal.Hex("eaffff"), Pal.Hex("fff0fb") };
        public static readonly Color32[] GateMain = { Pal.Hex("3ff0ff"), Pal.Hex("ff5fd8") };
        public static readonly Color32[] GateDeep = { Pal.Hex("1f7dff"), Pal.Hex("a43dff") };

        public SkinArt[] Skins;           // parallel to SkinDef.All
        public Sprite[] BirdSilhouette;   // [wing frame], shared by all skins
        public Sprite PipeBody, PipeCap;
        public Sprite[][] GateBeam;       // [variant][frame]
        public Sprite[] GateEmitter;      // [variant]
        public Sprite[] Spiky;            // [wing frame 0 up / 1 down], faces left
        public static readonly Color32 SpikyColor = Pal.Hex("86e04a");
        public Sprite GlowH, GlowRadial, ShieldBubble;
        public Texture2D ParticleTexture;
        public Sprite Sun, StarSmall, StarBig;
        public Sprite[] Clouds;
        public Sprite HillsFar, HillsMid, Ground;
        public Sprite IconPipe, IconGate, IconSoundOn, IconSoundOff, IconCrown, IconArrowLeft, IconArrowRight, Panel;

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

        public static Art Build()
        {
            var art = new Art();
            art.BuildSkins();
            art.BuildPipe();
            art.BuildGate();
            art.BuildSpiky();
            art.BuildFx();
            art.BuildScenery();
            art.BuildUi();
            return art;
        }

        // ---------------------------------------------------------------- Bird

        static readonly string[] BirdBody =
        {
            "......KKKKKK......",
            "....KKYYYYYYKK....",
            "...KYLLYYYYWWWK...",
            "..KYLYYYYYYWWEEK..",
            "..KYLYYYYYYWWEEK..",
            ".KYYYYYYYYYYWWWK..",
            ".KYYYYYYYYYYYKKBK.",
            ".KYYYYYYYYYYKBBBBK",
            ".KDYYYYYYYYPPKbbbK",
            ".KDDYYYYYYYYYYKKK.",
            "..KDDYYYYYYYYYDK..",
            "...KDDDYYYYYDDK...",
            "....KKDDDDDDKK....",
            "......KKKKKK......",
        };

        static readonly string[] WingUp =
        {
            "KK.......",
            "KwK......",
            "KwwKK....",
            "KwwwwKK..",
            ".KwwwwwKK",
            ".KggwwwwK",
            "..KKKKKK.",
        };

        static readonly string[] WingMid =
        {
            "KKKKKK...",
            "KwwwwwKK.",
            "KwwwwwwwK",
            ".KgggwwwK",
            "..KKKKKK.",
        };

        static readonly string[] WingDown =
        {
            "..KKKKKK.",
            ".KwwwwwwK",
            "KwwwwwwK.",
            "KggwwKK..",
            "KgKK.....",
            "KK.......",
        };

        /// <summary>Per-skin sprite set built from the shared bird map.</summary>
        public sealed class SkinArt
        {
            public SkinDef Def;
            public Sprite[,] Frames;       // [wing frame 0..2, eyes 0 open / 1 closed]
            public Sprite[] ScarfTail;     // [flutter frame 0..3], null when the skin has no scarf
            public Sprite Hat;             // pivot bottom-center, null for no hat
            public Vector3 HatOffset;      // local position on the bird
            public bool HatRigid;          // tied to the head (no bounce / lean), e.g. a headband
        }

        void BuildSkins()
        {
            Skins = new SkinArt[SkinDef.All.Length];
            for (int i = 0; i < Skins.Length; i++) Skins[i] = BuildSkin(SkinDef.All[i], i == 0);
        }

        SkinArt BuildSkin(SkinDef def, bool buildSilhouette)
        {
            var pal = new Dictionary<char, Color32>
            {
                { 'K', Pal.Ink }, { 'W', Pal.White }, { 'E', Pal.Ink },
                { 'Y', Pal.Hex(def.Body) }, { 'L', Pal.Hex(def.BodyLight) }, { 'D', Pal.Hex(def.BodyShade) },
                { 'B', Pal.Hex(def.Beak) }, { 'b', Pal.Hex(def.BeakShade) }, { 'P', Pal.Hex(def.Cheek) },
                { 'w', Pal.Hex(def.Wing) }, { 'g', Pal.Hex(def.WingShade) },
            };
            string[][] wings = { WingUp, WingMid, WingDown };
            Vector2Int[] wingPos = { new Vector2Int(1, 3), new Vector2Int(1, 8), new Vector2Int(1, 9) };
            string[] closed = CloseEyes(BirdBody);
            bool hasScarf = def.Scarf != null;
            Color32 scarfMain = default, scarfDark = default, scarfLight = default;
            if (hasScarf)
            {
                scarfMain = Pal.Hex(def.Scarf[0]);
                scarfDark = Pal.Hex(def.Scarf[1]);
                scarfLight = Pal.Hex(def.Scarf[2]);
            }

            var skin = new SkinArt { Def = def, Frames = new Sprite[3, 2] };
            if (buildSilhouette) BirdSilhouette = new Sprite[3];
            for (int w = 0; w < 3; w++)
            for (int e = 0; e < 2; e++)
            {
                var pc = new PixelCanvas(20, 17);
                pc.Stamp(e == 0 ? BirdBody : closed, pal, 2, 2);
                if (hasScarf) PaintScarfBand(pc, scarfMain, scarfDark, scarfLight);
                pc.Stamp(wings[w], pal, wingPos[w].x, wingPos[w].y);
                skin.Frames[w, e] = pc.ToSprite(Center);
                if (buildSilhouette && e == 0) BirdSilhouette[w] = pc.Silhouette(Pal.White).ToSprite(Center);
            }

            if (hasScarf) skin.ScarfTail = BuildScarfTail(scarfMain, scarfDark, scarfLight);
            BuildHat(def, skin);
            return skin;
        }

        // ---------------------------------------------------------------- Hats

        sealed class HatShape
        {
            public string[] Map;
            public int X, Y;        // top-left of the (padded) sprite in bird-canvas pixels (bird canvas is 20x17)
            public bool Outline;    // auto ink outline (map has no K)
            public bool Rigid;
        }

        static readonly Dictionary<HatKind, HatShape> HatShapes = new Dictionary<HatKind, HatShape>
        {
            {
                HatKind.Beanie, new HatShape
                {
                    X = 2, Y = -4,
                    Map = new[]
                    {
                        "....KKK.....",
                        "...KPPpK....",
                        "...KPppK....",
                        "..KKKKKKK...",
                        ".KHHHHHHHK..",
                        "KRrRrRrRrRK.",
                        "KRrRrRrRrRrK",
                        "KSsSsSsSsSsK",
                        ".KKKKKKKKKK.",
                    },
                }
            },
            {
                HatKind.Cap, new HatShape // worn backwards, bill over the back of the head
                {
                    X = -1, Y = -1,
                    Map = new[]
                    {
                        "......KKKKK....",
                        "....KKCCCCCKK..",
                        "...KCHHCCCCCCK.",
                        "...KCCCCCCCCCCK",
                        "KKKKCCCCCCCCCCK",
                        "KBBBKKKKKKKKKK.",
                        ".KKKK..........",
                    },
                }
            },
            {
                HatKind.Crown, new HatShape
                {
                    X = 5, Y = -3,
                    Map = new[]
                    {
                        ".K...K...K.",
                        "KYK.KYK.KYK",
                        "KYYKYYYKYYK",
                        "KYHYYRYYHYK",
                        "KYYYYYYYYYK",
                        "KDDDDDDDDDK",
                        ".KKKKKKKKK.",
                    },
                }
            },
            {
                HatKind.Headband, new HatShape // wraps the forehead; the lower edge doubles as a stern brow
                {
                    X = 0, Y = 3, Rigid = true,
                    Map = new[]
                    {
                        "....KKRRRRRRRRRRK",
                        "..KKRKKKKKKKKKKKK",
                        ".KRRK............",
                        "KRRK.............",
                        "KKK..............",
                    },
                }
            },
            {
                HatKind.Antenna, new HatShape
                {
                    X = 7, Y = -6, Outline = true,
                    Map = new[] { ".CC.", "CWCC", "CCCC", ".CC.", "..G.", "..G.", ".GGG" },
                }
            },
            {
                HatKind.TopHat, new HatShape
                {
                    X = 4, Y = -4, Outline = true,
                    Map = new[] { "..TTTTTT..", "..THTTTT..", "..THTTTT..", "..TTTTTT..", "..BBBBBB..", "TTTTTTTTTT" },
                }
            },
        };

        static void BuildHat(SkinDef def, SkinArt skin)
        {
            if (def.Hat == HatKind.None || !HatShapes.TryGetValue(def.Hat, out var shape)) return;
            var pal = new Dictionary<char, Color32> { { 'K', Pal.Ink } };
            foreach (var kv in def.HatColors) pal[kv.Key] = Pal.Hex(kv.Value);

            var pc = PixelCanvas.FromMap(shape.Map, pal, shape.Outline ? 1 : 0);
            if (shape.Outline) pc.Outline(Pal.Ink, true);
            skin.Hat = pc.ToSprite(BottomCenter);
            // Bottom-center of the hat sprite, relative to the bird sprite's center (10, 8.5).
            skin.HatOffset = new Vector3(shape.X + pc.Width * 0.5f - 10f, 8.5f - (shape.Y + pc.Height), 0f) / World.PPU;
            skin.HatRigid = shape.Rigid;
        }

        // ---------------------------------------------------------------- Scarf

        /// <summary>Knit scarf around the neck: two rows just under the beak, recolouring body pixels only (outline stays).</summary>
        static void PaintScarfBand(PixelCanvas pc, Color32 main, Color32 dark, Color32 light)
        {
            for (int x = 0; x < pc.Width; x++)
            {
                RecolorBody(pc, x, 11, x % 4 == 0 ? light : main);
                RecolorBody(pc, x, 12, dark);
            }
        }

        static void RecolorBody(PixelCanvas pc, int x, int y, Color32 c)
        {
            var cur = pc.Get(x, y);
            bool isInk = cur.r == Pal.Ink.r && cur.g == Pal.Ink.g && cur.b == Pal.Ink.b;
            if (cur.a == 0 || isInk) return;
            pc.Set(x, y, c);
        }

        /// <summary>
        /// Scarf end trailing behind the bird: a 3px knit ribbon (light top, dark underside) whose wave grows towards
        /// a forked tip. 4 phases make the flutter loop.
        /// </summary>
        static Sprite[] BuildScarfTail(Color32 main, Color32 dark, Color32 light)
        {
            const int len = 13, w = len + 2, h = 11;
            Color32 stripeTop = Pal.Shade(light, 1.4f), stripeMid = light, stripeLow = Pal.Shade(main, 0.92f);
            var frames = new Sprite[4];
            for (int f = 0; f < 4; f++)
            {
                var pc = new PixelCanvas(w, h);
                int prevY = int.MinValue;
                for (int i = 0; i < len; i++)
                {
                    int x = w - 2 - i;
                    float amp = 0.3f + 1.5f * i / (len - 1);
                    int y = Mathf.RoundToInt(h * 0.5f - 1.5f + amp * Mathf.Sin(i * 0.5f - f * Mathf.PI * 0.5f));
                    if (prevY != int.MinValue)
                    {
                        for (int fy = prevY + 1; fy < y; fy++) pc.Set(x, fy, main);
                        for (int fy = y + 3; fy < prevY + 2; fy++) pc.Set(x, fy, dark);
                    }
                    bool stripe = i % 4 == 2;
                    bool fork = i >= len - 2;
                    pc.Set(x, y, stripe ? stripeTop : light);
                    if (!fork) pc.Set(x, y + 1, stripe ? stripeMid : main);
                    pc.Set(x, y + 2, stripe ? stripeLow : dark);
                    prevY = y;
                }
                pc.Outline(Pal.Ink, false);
                frames[f] = pc.ToSprite(new Vector2((w - 1f) / w, 0.5f));
            }
            return frames;
        }

        static string[] CloseEyes(string[] src)
        {
            var result = (string[])src.Clone();
            for (int y = 2; y <= 5; y++)
            {
                var row = result[y].ToCharArray();
                for (int x = 0; x < row.Length; x++)
                    if (row[x] == 'W' || row[x] == 'E') row[x] = y == 4 ? 'K' : 'Y';
                result[y] = new string(row);
            }
            return result;
        }

        // ---------------------------------------------------------------- Pipes

        static Color32[] PipeColumns()
        {
            string[] hex =
            {
                "2a1633", "b8471a", "e8661f", "ffb35c", "ffe0a6", "ffb35c", "ff9a3a", "ff8a2a",
                "ff8a2a", "ff8a2a", "ff8a2a", "ff8a2a", "ff8a2a", "ff8a2a", "f57a22", "f57a22",
                "f57a22", "e8661f", "e8661f", "d4561b", "c24b1a", "a83d17", "8f3314", "2a1633",
            };
            var cols = new Color32[hex.Length];
            for (int i = 0; i < hex.Length; i++) cols[i] = Pal.Hex(hex[i]);
            return cols;
        }

        void BuildPipe()
        {
            var cols = PipeColumns();

            // Body: 24x16 tile with a segment band and rivets, drawn tiled along the pipe length.
            var body = new PixelCanvas(24, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 24; x++)
            {
                var c = cols[x];
                if (x > 0 && x < 23)
                {
                    if (y == 0) c = Pal.Shade(c, 0.78f);
                    else if (y == 1) c = Pal.Shade(c, 1.18f);
                }
                body.Set(x, y, c);
            }
            body.Set(5, 7, Pal.Hex("ffe0a6"));
            body.Set(5, 8, Pal.Hex("c24b1a"));
            body.Set(19, 7, Pal.Hex("ff9a3a"));
            body.Set(19, 8, Pal.Hex("8f3314"));
            PipeBody = body.ToSprite(Center);

            // Cap: 28x12, top edge is the gap edge, bottom row is a soft shadow onto the body.
            var cap = new PixelCanvas(28, 12);
            for (int x = 1; x < 27; x++)
            {
                cap.Set(x, 0, Pal.Ink);
                cap.Set(x, 10, Pal.Ink);
            }
            for (int y = 1; y < 10; y++)
            {
                cap.Set(0, y, Pal.Ink);
                cap.Set(27, y, Pal.Ink);
                for (int x = 1; x < 27; x++)
                {
                    var c = Pal.Shade(cols[Mathf.Clamp(Mathf.RoundToInt(x * 23f / 27f), 1, 22)], 1.06f);
                    if (y == 1) c = Pal.Shade(c, 1.3f);
                    else if (y == 2) c = Pal.Shade(c, 1.1f);
                    else if (y == 9) c = Pal.Shade(c, 0.78f);
                    cap.Set(x, y, c);
                }
            }
            for (int x = 3; x < 25; x++) cap.Set(x, 11, new Color32(18, 10, 31, 110));
            PipeCap = cap.ToSprite(TopCenter);
        }

        // ---------------------------------------------------------------- Dash gates

        static readonly string[] Emitter =
        {
            "...KKKKKKKKKKKK...",
            "..KllllllllllllK..",
            ".KlmmmmmmmmmmmmdK.",
            "KlmmCmmCmmCmmCmmdK",
            "KlmmCmmCmmCmmCmmdK",
            "KddddddddddddddddK",
            ".KKKKKKKKKKKKKKKK.",
            "....KccccccccK....",
            ".....KWWWWWWK.....",
            "......KKKKKK......",
        };

        void BuildGate()
        {
            GateBeam = new Sprite[2][];
            GateEmitter = new Sprite[2];
            for (int v = 0; v < 2; v++)
            {
                Color32 core = GateCore[v], main = GateMain[v], deep = GateDeep[v];
                GateBeam[v] = new Sprite[4];
                for (int f = 0; f < 4; f++)
                {
                    var pc = new PixelCanvas(10, 32);
                    for (int y = 0; y < 32; y++)
                    {
                        float cx = 4.5f
                                   + 2.1f * Mathf.Sin(2f * Mathf.PI * (y / 32f + f / 4f))
                                   + 0.9f * Mathf.Sin(2f * Mathf.PI * (3f * y / 32f - f / 4f));
                        for (int x = 0; x < 10; x++)
                        {
                            float d = Mathf.Abs(x + 0.5f - cx);
                            Color32 c;
                            if (x == 0 || x == 9) c = Pal.Alpha(main, (byte)((y + f * 2) % 8 < 5 ? 170 : 70));
                            else c = Pal.Alpha(deep, 38);
                            if (d < 2.6f) c = Pal.Alpha(deep, 210);
                            if (d < 1.7f) c = main;
                            if (d < 0.8f) c = core;
                            if (Hash(x, y, f + v * 7) % 37 == 0) c = core;
                            pc.Set(x, y, c);
                        }
                    }
                    GateBeam[v][f] = pc.ToSprite(Center);
                }

                var pal = new Dictionary<char, Color32>
                {
                    { 'K', Pal.Ink }, { 'l', Pal.Hex("9a86bd") }, { 'm', Pal.Hex("5d4a80") }, { 'd', Pal.Hex("382a55") },
                    { 'C', main }, { 'c', deep }, { 'W', core },
                };
                GateEmitter[v] = PixelCanvas.FromMap(Emitter, pal).ToSprite(TopCenter);
            }
        }

        // ---------------------------------------------------------------- Spiky enemy

        static readonly string[] SpikyWing =
        {
            "..........K",
            "........KKK",
            "......KKwwK",
            "....KKwwmwK",
            "..KKwwmwwwK",
            ".KwwmwwwmK.",
            "KwwwwmwwK..",
            ".KKKKKKK...",
        };

        static readonly string[] SpikyFace =
        {
            "K......K.",
            ".KK..KK..",
            ".EW..EW..",
            ".EW..EW..",
            ".........",
            "KTKTKTK..",
            "KKKKKKK..",
        };

        /// <summary>Angry spiky ball with bat wings: shaded circle, 10 tapered spikes, face stamped on the left (it flies towards the bird).</summary>
        void BuildSpiky()
        {
            Color32 light = Pal.Hex("d4ff8a"), main = SpikyColor, dark = Pal.Hex("3f9a3c");
            Color32 spike = Pal.Hex("fff1c4"), spikeShade = Pal.Hex("c9b27a");
            var wingPal = new Dictionary<char, Color32> { { 'K', Pal.Ink }, { 'w', Pal.Hex("2f7a45") }, { 'm', Pal.Hex("5fbf6a") } };
            var facePal = new Dictionary<char, Color32> { { 'K', Pal.Ink }, { 'W', Pal.White }, { 'E', Pal.Ink }, { 'T', Pal.White } };
            var wingDown = (string[])SpikyWing.Clone();
            System.Array.Reverse(wingDown);

            const int size = 24;
            const float c = 11.5f, r = 6.3f;
            Spiky = new Sprite[2];
            for (int f = 0; f < 2; f++)
            {
                var pc = new PixelCanvas(size, size);
                pc.Stamp(f == 0 ? SpikyWing : wingDown, wingPal, 13, f == 0 ? 0 : 12);

                for (int k = 0; k < 10; k++)
                {
                    float a = (k * 36f + 18f) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var perp = new Vector2(-dir.y, dir.x);
                    for (float t = r - 1f; t <= r + 2.5f; t += 0.25f)
                    {
                        float hw = Mathf.Max(0f, (r + 2.5f - t) * 0.55f);
                        for (float s = -hw; s <= hw; s += 0.25f)
                        {
                            var p = new Vector2(c, c) + dir * t + perp * s;
                            pc.Set(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), s > 0f ? spikeShade : spike);
                        }
                    }
                }

                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float ox = x + 0.5f - c, oy = y + 0.5f - c, d = Mathf.Sqrt(ox * ox + oy * oy);
                    if (d > r) continue;
                    float l = (ox * -0.7f + oy * -0.7f) / Mathf.Max(d, 0.001f) * (d / r);
                    pc.Set(x, y, l > 0.45f ? light : l < -0.35f ? dark : main);
                }

                pc.Stamp(SpikyFace, facePal, 6, 8);
                pc.Outline(Pal.Ink, true);
                Spiky[f] = pc.ToSprite(Center);
            }
        }

        static int Hash(int x, int y, int z)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ z * 83492791;
                h ^= h >> 13;
                h *= 0x5bd1e995;
                return (h ^ (h >> 15)) & 0x7fffffff;
            }
        }

        // ---------------------------------------------------------------- FX

        void BuildFx()
        {
            var glow = new PixelCanvas(32, 2);
            for (int x = 0; x < 32; x++)
            {
                float a = Mathf.Exp(-Mathf.Pow((x - 15.5f) / 7f, 2f));
                for (int y = 0; y < 2; y++) glow.Set(x, y, new Color32(255, 255, 255, (byte)(a * 255f)));
            }
            GlowH = glow.ToSprite(Center, World.PPU, FilterMode.Bilinear);

            var radial = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f);
                radial.Set(x, y, new Color32(255, 255, 255, (byte)(Mathf.Pow(1f - d, 2f) * 255f)));
            }
            GlowRadial = radial.ToSprite(Center, World.PPU, FilterMode.Bilinear);

            // BLUEJAY shield: pixel ring with a faint fill and a specular arc at the top-left.
            var bubble = new PixelCanvas(30, 30);
            Color32 rim = Pal.Hex("8ff6ff"), fill = new Color32(63, 240, 255, 40), shine = Pal.White;
            for (int y = 0; y < 30; y++)
            for (int x = 0; x < 30; x++)
            {
                float dx = x + 0.5f - 15f, dy = y + 0.5f - 15f, d = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (d > 14f) continue;
                if (d > 12.8f) bubble.Set(x, y, Pal.Alpha(rim, 210));
                else if (d > 9.5f && d < 10.8f && angle < -100f && angle > -160f) bubble.Set(x, y, Pal.Alpha(shine, 200));
                else bubble.Set(x, y, fill);
            }
            ShieldBubble = bubble.ToSprite(Center);

            var particle = new PixelCanvas(4, 4);
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                particle.Set(x, y, Pal.White);
            ParticleTexture = particle.ToTexture();
        }

        // ---------------------------------------------------------------- Scenery

        static readonly string[] SkyHex = { "140b2e", "231446", "3a1d5e", "5a2672", "822f7c", "b03d7e", "d9577a", "f07d6e", "ffa86b" };
        static readonly int[,] Bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };

        /// <summary>Vertical dithered gradient, 4px wide so it tiles horizontally. Pivot at the top.</summary>
        public static Sprite BuildSky(int rows)
        {
            var cols = new Color32[SkyHex.Length];
            for (int i = 0; i < cols.Length; i++) cols[i] = Pal.Hex(SkyHex[i]);
            var pc = new PixelCanvas(4, rows);
            for (int y = 0; y < rows; y++)
            {
                float t = Mathf.Pow(y / (float)Mathf.Max(1, rows - 1), 1.25f) * (cols.Length - 1);
                int a = Mathf.Min(Mathf.FloorToInt(t), cols.Length - 1);
                float f = t - a;
                for (int x = 0; x < 4; x++)
                {
                    float threshold = (Bayer[y & 3, x] + 0.5f) / 16f;
                    pc.Set(x, y, f > threshold && a + 1 < cols.Length ? cols[a + 1] : cols[a]);
                }
            }
            return pc.ToSprite(TopCenter);
        }

        void BuildScenery()
        {
            // Synthwave sun with horizontal cuts in its lower half.
            var sun = new PixelCanvas(44, 44);
            Color32 sunTop = Pal.Hex("fff27a"), sunBottom = Pal.Hex("ff4f8b");
            for (int y = 0; y < 44; y++)
            for (int x = 0; x < 44; x++)
            {
                float dx = x + 0.5f - 22f, dy = y + 0.5f - 22f;
                if (dx * dx + dy * dy > 22f * 22f) continue;
                if (y > 22)
                {
                    int k = y - 22;
                    if (k % 6 < 1 + k / 7) continue;
                }
                float t = Mathf.Floor(y / 44f * 8f) / 7f;
                sun.Set(x, y, Color32.Lerp(sunTop, sunBottom, t));
            }
            Sun = sun.ToSprite(Center);

            var small = new PixelCanvas(1, 1);
            small.Set(0, 0, Pal.Hex("fff6d8"));
            StarSmall = small.ToSprite(Center);
            var big = new PixelCanvas(3, 3);
            big.Set(1, 1, Pal.White);
            big.Set(0, 1, Pal.Hex("fff6d8a0"));
            big.Set(2, 1, Pal.Hex("fff6d8a0"));
            big.Set(1, 0, Pal.Hex("fff6d8a0"));
            big.Set(1, 2, Pal.Hex("fff6d8a0"));
            StarBig = big.ToSprite(Center);

            Clouds = new[] { MakeCloud(11, 44, 16), MakeCloud(23, 34, 13), MakeCloud(37, 52, 18) };

            HillsFar = MakeHills(160, 72, 38f, new[] { new Vector3(10f, 1f, 0.3f), new Vector3(6f, 3f, 1.1f), new Vector3(3f, 7f, 2f) },
                Pal.Hex("4a2a6e"), Pal.Hex("7a4a9a"), 0);
            HillsMid = MakeHills(128, 56, 24f, new[] { new Vector3(9f, 2f, 2f), new Vector3(4f, 5f, 0.5f) },
                Pal.Hex("2c1848"), Pal.Hex("4a2c6e"), 7);
            Ground = MakeGround();
        }

        static Sprite MakeCloud(int seed, int w, int h)
        {
            var rng = new System.Random(seed);
            var pc = new PixelCanvas(w, h);
            Color32 body = Pal.Hex("f1a6c9"), shade = Pal.Hex("b76aa8"), rim = Pal.Hex("ffd6e6");
            const int n = 5;
            for (int i = 0; i < n; i++)
            {
                float mid = 1f - Mathf.Abs(i - (n - 1) * 0.5f) / ((n - 1) * 0.5f);
                float r = h * (0.32f + 0.25f * mid) + (float)rng.NextDouble() * 1.5f;
                float cx = Mathf.Lerp(r, w - r, i / (float)(n - 1)) + (float)(rng.NextDouble() - 0.5) * 3f;
                float cy = h - 1 - r * 0.75f;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= r * r) pc.Set(x, y, y > h * 0.66f ? shade : body);
                }
            }
            var result = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!pc.Opaque(x, y)) continue;
                var c = pc.Get(x, y);
                if (!pc.Opaque(x, y - 1)) c = rim;
                result.Set(x, y, Pal.Alpha(c, 230));
            }
            return result.ToSprite(Center);
        }

        /// <summary>Seamless hill silhouette: integer sine frequencies make the texture tile horizontally.</summary>
        static Sprite MakeHills(int w, int h, float baseHeight, Vector3[] waves, Color32 fill, Color32 rim, int treeSeed)
        {
            var pc = new PixelCanvas(w, h);
            var heights = new int[w];
            for (int x = 0; x < w; x++)
            {
                float v = baseHeight;
                foreach (var wave in waves) v += wave.x * Mathf.Sin(2f * Mathf.PI * wave.y * x / w + wave.z);
                heights[x] = Mathf.Clamp(Mathf.RoundToInt(v), 1, h);
                for (int yb = 0; yb < heights[x]; yb++)
                    pc.Set(x, h - 1 - yb, yb == heights[x] - 1 ? rim : fill);
            }

            if (treeSeed > 0)
            {
                var rng = new System.Random(treeSeed);
                for (int tx = 8 + rng.Next(6); tx < w - 8; tx += 12 + rng.Next(10))
                {
                    int th = 8 + rng.Next(7);
                    int baseY = h - heights[tx];
                    for (int i = 0; i < th; i++)
                    {
                        int hw = Mathf.RoundToInt(i * 0.32f) + (i % 4 == 3 ? 1 : 0);
                        int y = baseY - th + i;
                        for (int dx = -hw; dx <= hw; dx++) pc.Set(tx + dx, y, dx == -hw ? rim : fill);
                    }
                    pc.Set(tx, baseY - th - 1, rim);
                }
            }
            return pc.ToSprite(BottomCenter);
        }

        static Sprite MakeGround()
        {
            var pc = new PixelCanvas(32, 36);
            int[] grassDepth = { 0, 1, 1, 0, 1, 2, 1, 0 };
            Color32 outline = Pal.Hex("1b0f2a"), hi = Pal.Hex("9dffc9"), grass = Pal.Hex("48e0a4"), grassDark = Pal.Hex("2fb487");
            Color32 lip = Pal.Hex("3b2757"), road = Pal.Hex("2e1d47"), roadDeep = Pal.Hex("251739"), speck = Pal.Hex("3d2a5c");
            Color32 stripeHi = Pal.Hex("ffd48a"), stripe = Pal.Hex("ffb347"), stripeLo = Pal.Hex("d98a2b");
            for (int x = 0; x < 32; x++)
            {
                int grassBottom = 5 + grassDepth[x % 8];
                for (int y = 0; y < 36; y++)
                {
                    Color32 c;
                    if (y == 0) c = outline;
                    else if (y == 1) c = hi;
                    else if (y <= 3) c = (x % 4 == 1 && y == 3) ? grassDark : grass;
                    else if (y <= grassBottom) c = grassDark;
                    else if (y == grassBottom + 1) c = outline;
                    else if (y <= 9) c = lip;
                    else
                    {
                        c = y >= 30 ? roadDeep : road;
                        if (Hash(x, y, 3) % 23 == 0) c = speck;
                        if (x >= 4 && x <= 17)
                        {
                            if (y == 19) c = stripeHi;
                            else if (y == 20) c = stripe;
                            else if (y == 21) c = stripeLo;
                        }
                    }
                    pc.Set(x, y, c);
                }
            }
            return pc.ToSprite(BottomCenter);
        }

        // ---------------------------------------------------------------- UI

        static readonly string[] SoundOnMap =
        {
            "....w.......",
            "...ww...w...",
            "wwwww.w..w..",
            "wwwww..w.w..",
            "wwwww..w.w..",
            "wwwww.w..w..",
            "...ww...w...",
            "....w.......",
        };

        static readonly string[] SoundOffMap =
        {
            "....w.......",
            "...ww.......",
            "wwwww.w...w.",
            "wwwww..w.w..",
            "wwwww...w...",
            "wwwww..w.w..",
            "...ww.w...w.",
            "....w.......",
        };

        static readonly string[] CrownMap =
        {
            "w...w...w",
            "ww.www.ww",
            "wwwwwwwww",
            "wwwwwwwww",
            "wwwwwwwww",
            ".........",
            "wwwwwwwww",
        };

        static readonly string[] PipeIconMap =
        {
            "KKKKKKKKKKKK",
            "KhOooooooddK",
            "KhOooooooddK",
            "KKKKKKKKKKKK",
            ".KhOoooodDK.",
            ".KhOoooodDK.",
            ".KhOoooodDK.",
            ".KhOoooodDK.",
            ".KhOoooodDK.",
            ".KhOoooodDK.",
            ".KhOoooodDK.",
            ".KhOoooodDK.",
            ".KhOoooodDK.",
        };

        static readonly string[] GateIconMap =
        {
            "KKKKKKKKKKKK",
            "KmmmmmmmmmmK",
            "KKKKcCCcKKKK",
            "....cCWc....",
            "....cWCc....",
            "...cCWc.....",
            "...cWCc.....",
            "....cCWc....",
            "....cWCc....",
            ".....cCWc...",
            ".....cWCc...",
            "....cCWc....",
            "KKKKcCCcKKKK",
            "KmmmmmmmmmmK",
            "KKKKKKKKKKKK",
        };

        static readonly string[] PanelMap =
        {
            "..KKKKKKKK..",
            ".KLLLLLLLLK.",
            "KLFFFFFFFFDK",
            "KLFFFFFFFFDK",
            "KLFFFFFFFFDK",
            "KLFFFFFFFFDK",
            "KLFFFFFFFFDK",
            "KLFFFFFFFFDK",
            "KLFFFFFFFFDK",
            "KLFFFFFFFFDK",
            ".KDDDDDDDDK.",
            "..KKKKKKKK..",
        };

        void BuildUi()
        {
            IconSoundOn = OutlinedIcon(SoundOnMap, Pal.White);
            IconSoundOff = OutlinedIcon(SoundOffMap, Pal.Hex("b9a8d6"));
            IconCrown = OutlinedIcon(CrownMap, Pal.Gold);
            var arrow = new[] { "...w", "..ww", ".www", "wwww", ".www", "..ww", "...w" };
            IconArrowLeft = OutlinedIcon(arrow, Pal.White);
            var mirrored = new string[arrow.Length];
            for (int i = 0; i < arrow.Length; i++)
            {
                var chars = arrow[i].ToCharArray();
                System.Array.Reverse(chars);
                mirrored[i] = new string(chars);
            }
            IconArrowRight = OutlinedIcon(mirrored, Pal.White);

            var pipePal = new Dictionary<char, Color32>
            {
                { 'K', Pal.Ink }, { 'h', Pal.Hex("ffe0a6") }, { 'O', Pal.Hex("ffb35c") }, { 'o', Pal.Hex("ff8a2a") },
                { 'd', Pal.Hex("e8661f") }, { 'D', Pal.Hex("c24b1a") },
            };
            IconPipe = PixelCanvas.FromMap(PipeIconMap, pipePal).ToSprite(Center);

            var gatePal = new Dictionary<char, Color32>
            {
                { 'K', Pal.Ink }, { 'm', Pal.Hex("5d4a80") }, { 'c', GateDeep[0] }, { 'C', GateMain[0] }, { 'W', GateCore[0] },
            };
            IconGate = PixelCanvas.FromMap(GateIconMap, gatePal).ToSprite(Center);

            var panelPal = new Dictionary<char, Color32>
            {
                { 'K', Pal.Ink }, { 'L', Pal.Hex("6b4a9a") }, { 'F', Pal.Hex("2e1f4fee") }, { 'D', Pal.Hex("1c1233") },
            };
            Panel = PixelCanvas.FromMap(PanelMap, panelPal).ToSprite(Center, UiPPU, FilterMode.Point, new Vector4(4, 4, 4, 4));
        }

        static Sprite OutlinedIcon(string[] map, Color32 color)
        {
            var pc = PixelCanvas.FromMap(map, new Dictionary<char, Color32> { { 'w', color } }, 1);
            pc.Outline(Pal.Ink, true);
            return pc.ToSprite(Center);
        }
    }
}
