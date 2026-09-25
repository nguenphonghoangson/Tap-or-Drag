using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Every sprite in the prototype is painted at startup from ASCII pixel maps or small procedural rules,
    /// so the project needs no imported art. Palette: dusk synthwave sky, orange pipes, cyan/pink neon gates.
    /// </summary>
    public sealed partial class Art
    {
        public const float UiPPU = 100f / 3f;

        public static readonly Color32[] GateCore = { Pal.Hex("eaffff"), Pal.Hex("fff0fb"), Pal.Hex("fff0f0") };
        public static readonly Color32[] GateMain = { Pal.Hex("3ff0ff"), Pal.Hex("ff5fd8"), Pal.Hex("ff3b4e") };
        public static readonly Color32[] GateDeep = { Pal.Hex("1f7dff"), Pal.Hex("a43dff"), Pal.Hex("b3163a") };
        public const int RedVariant = 2; // trap gates (do NOT dash)

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
            art.BuildExtras();
            return art;
        }

        // ---------------------------------------------------------------- Dog pilot in a flying saucer (the player character)
        // 24x23 canvas: the puppy sits in an open cockpit (head, floppy ear, collar), a windshield in front, the saucer
        // hull in the skin's colour, and an engine flame underneath. The three "wing" frames are flame sizes
        // (big right after a flap, then flickering), so all the old wing-cycle animation code drives the engine.
        // Palette keys: Y fur, L light fur, D shade, W/E eye, N nose, T tongue, P blush, G glass.

        public const int CharWidth = 24, CharHeight = 23;
        const float CharCenterX = CharWidth * 0.5f, CharCenterY = CharHeight * 0.5f;
        // Hats were authored on the old bird head; this moves them onto the puppy's head.
        const int HatShiftX = 2, HatShiftY = -1;

        static readonly string[] DogHead =
        {
            "...KKKKKK......",
            "..KYYYYYYKK....",
            ".KYLYYYYYYYK...",
            ".KYYYYYYYWWEK..",
            "KYYYYYYYYWEEK..",
            "KYYYYYYYYYYYLKK",
            "KYYYYYYYYYYLLNK",
            "KDYYYYYYYPKKKKK",
            ".KDYYYYYYYKTTK.",
            "..KDDYYYYYYKK..",
        };
        static readonly string[] DogEar = { ".KK..", "KDDK.", "KDDDK", "KDDDK", "KDDK.", ".KDK.", "..K.." };
        static readonly string[] DogTorso = { "KYYYYYYYYYK", "KYYYYYYYYYK", "KYYYYYYYYYK" };
        static readonly string[] Windshield = { "...K", "..KG", ".KGG", "KGGG" };
        static readonly string[][] EngineFlames =
        {
            new[] { "oyWWyo", ".yWWy.", "..yy.." }, // just flapped
            new[] { ".yWWy.", "..yy.." },
            new[] { "..yy.." },
        };

        /// <summary>Saucer hull: metal rim, skin-coloured top, dark underside, running lights and a belly glow.</summary>
        static void DrawHull(PixelCanvas pc, Color32 main, Color32 shade)
        {
            const float cx = 11.5f, cy = 16f, rx = 11.5f, ry = 3.6f;
            Color32 metal = Pal.Hex("f4f8ff"), lights = Pal.Hex("8ff6ff");
            bool Inside(int x, int y)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                return dx * dx + dy * dy <= 1f;
            }
            for (int y = 0; y < pc.Height; y++)
            for (int x = 0; x < pc.Width; x++)
            {
                if (Inside(x, y))
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    Color32 c = dy < -0.78f ? metal : dy < 0.02f ? main : shade;
                    if (y == 15) c = Pal.Ink;                                         // seam
                    if (y == 16 && x % 3 == 1 && Mathf.Abs(dx) < 0.85f) c = lights;  // running lights
                    if (dy > 0.6f && Mathf.Abs(dx) < 0.25f) c = lights;              // belly glow
                    pc.Set(x, y, c);
                }
                else if (Inside(x - 1, y) || Inside(x + 1, y) || Inside(x, y - 1) || Inside(x, y + 1))
                {
                    if (!pc.Opaque(x, y) || y >= 13) pc.Set(x, y, Pal.Ink); // outline, without cutting through the collar
                }
            }
        }

        /// <summary>Per-skin sprite set built from the shared dog-pilot art.</summary>
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
                { 'T', Pal.Hex(def.Beak) }, { 'N', Pal.Hex(def.BeakShade) }, { 'P', Pal.Hex(def.Cheek) },
                { 'G', new Color32(191, 244, 255, 150) },
            };
            var flamePal = new Dictionary<char, Color32> { { 'W', Pal.Hex("fff8d0") }, { 'y', Pal.Gold }, { 'o', Pal.Orange } };
            Color32 hull = Pal.Hex(def.Wing), hullShade = Pal.Hex(def.WingShade);
            string[] closedHead = CloseEyes(DogHead);
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
            for (int f = 0; f < 3; f++)
            for (int e = 0; e < 2; e++)
            {
                var pc = new PixelCanvas(CharWidth, CharHeight);
                pc.Stamp(DogTorso, pal, 8, 10);
                if (hasScarf) PaintScarfBand(pc, scarfMain, scarfDark, scarfLight); // collar on the torso, before the hull covers it
                DrawHull(pc, hull, hullShade);
                pc.Stamp(e == 0 ? DogHead : closedHead, pal, 7, 1);
                pc.Stamp(DogEar, pal, 8, 2);
                pc.Stamp(Windshield, pal, 18, 9);
                pc.Stamp(EngineFlames[f], flamePal, 9, 20);
                skin.Frames[f, e] = pc.ToSprite(Center);
                if (buildSilhouette && e == 0) BirdSilhouette[f] = pc.Silhouette(Pal.White).ToSprite(Center);
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
            skin.HatOffset = new Vector3(shape.X + HatShiftX + pc.Width * 0.5f - CharCenterX, CharCenterY - (shape.Y + HatShiftY + pc.Height), 0f) / World.PPU;
            skin.HatRigid = shape.Rigid;
        }

        // ---------------------------------------------------------------- Scarf

        /// <summary>Collar: two rows between head and body, recolouring fur only (outline stays); stops before the tongue.</summary>
        static void PaintScarfBand(PixelCanvas pc, Color32 main, Color32 dark, Color32 light)
        {
            for (int x = 8; x <= 18; x++)
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

        /// <summary>Closed eyes: the lowest eye pixel in each column becomes a lid line, the rest becomes fur.</summary>
        static string[] CloseEyes(string[] src)
        {
            bool IsEye(char ch) => ch == 'W' || ch == 'E';
            var rows = new char[src.Length][];
            for (int y = 0; y < src.Length; y++) rows[y] = src[y].ToCharArray();
            for (int y = 0; y < src.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
            {
                if (!IsEye(src[y][x])) continue;
                bool eyeBelow = y + 1 < src.Length && x < src[y + 1].Length && IsEye(src[y + 1][x]);
                rows[y][x] = eyeBelow ? 'Y' : 'K';
            }
            var result = new string[src.Length];
            for (int y = 0; y < src.Length; y++) result[y] = new string(rows[y]);
            return result;
        }

        // ---------------------------------------------------------------- Pillars (giant bones in the dog version)

        public static readonly Color32 BoneHighlight = Pal.Hex("fffdf6");
        public static readonly Color32 BoneLight = Pal.Hex("fbf4e3");
        public static readonly Color32 BoneBase = Pal.Hex("f0e5c9");
        public static readonly Color32 BoneShade = Pal.Hex("d9c8a0");
        public static readonly Color32 BoneDeep = Pal.Hex("b9a276");
        public static readonly Color32 BoneWarm = Pal.Hex("9c8560");

        public const int BoneShaftWidth = 18, BoneEndWidth = 32, BoneEndHeight = 22, BoneKnobHeight = 14;
        public Sprite PipeCapTop; // knob end for the top pillar (gap edge at the bottom, still lit from above)

        /// <summary>Cylinder shading across a width (u = 0..1), lit from the left.</summary>
        static Color32 BoneCylinder(float u) =>
            u < 0.08f ? BoneDeep : u < 0.16f ? BoneShade : u < 0.26f ? BoneLight : u < 0.36f ? BoneHighlight : u < 0.46f ? BoneLight
            : u < 0.66f ? BoneBase : u < 0.8f ? BoneShade : u < 0.92f ? BoneDeep : BoneWarm;

        /// <summary>
        /// Pillars drawn as a femur: a narrow shaft with long grain and pores (tiled along the pillar), and at the gap
        /// a flared neck ending in two round condyles with a crease between them. Collision sizes in PipePair match.
        /// </summary>
        void BuildPipe()
        {
            const int sw = BoneShaftWidth;
            var body = new PixelCanvas(sw, 16);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < sw; x++)
            {
                Color32 c = x == 0 || x == sw - 1 ? Pal.Ink : BoneCylinder((x - 0.5f) / (sw - 2));
                if (x > 2 && x < sw - 3)
                {
                    if (Hash(x, y, 5) % 23 == 0) c = BoneShade;                  // pores
                    if ((x == 9 || x == 12) && (y + x) % 7 < 3) c = BoneShade;    // grain
                }
                body.Set(x, y, c);
            }
            PipeBody = body.ToSprite(Center);
            PipeCap = BuildBoneEnd(true).ToSprite(TopCenter);
            PipeCapTop = BuildBoneEnd(false).ToSprite(BottomCenter);
        }

        /// <param name="gapEdgeTop">true: knobs on the top row (bottom pillar); false: knobs on the bottom row (top pillar).</param>
        static PixelCanvas BuildBoneEnd(bool gapEdgeTop)
        {
            const int w = BoneEndWidth, h = BoneEndHeight;
            const float radius = 7.8f;
            var lobes = new[] { new Vector2(8.5f, 8f), new Vector2(23.5f, 8f) };
            float NeckHalf(float y) => y < 10f ? 12f : 12f - (y - 10f) * (3f / 11f); // 24px flaring down to the 18px shaft

            var fill = new bool[w, h];
            for (int row = 0; row < h; row++)
            for (int x = 0; x < w; x++)
            {
                float y = (gapEdgeTop ? row : h - 1 - row) + 0.5f, px = x + 0.5f; // geometry with the gap edge on top
                bool inLobe = Vector2.Distance(new Vector2(px, y), lobes[0]) <= radius || Vector2.Distance(new Vector2(px, y), lobes[1]) <= radius;
                bool inNeck = y >= 8.5f && Mathf.Abs(px - 16f) <= NeckHalf(y);
                fill[x, row] = inLobe || inNeck;
            }

            var pc = new PixelCanvas(w, h);
            for (int row = 0; row < h; row++)
            for (int x = 0; x < w; x++)
            {
                int geomRow = gapEdgeTop ? row : h - 1 - row;
                if (!fill[x, row])
                {
                    bool edge = (x > 0 && fill[x - 1, row]) || (x < w - 1 && fill[x + 1, row]) || (row > 0 && fill[x, row - 1]) || (row < h - 1 && fill[x, row + 1]);
                    if (edge && geomRow != h - 1) pc.Set(x, row, Pal.Ink); // no outline where the end joins the shaft
                    continue;
                }
                float px = x + 0.5f, y = geomRow + 0.5f;
                var lobe = px < 16f ? lobes[0] : lobes[1];
                float dx = (px - lobe.x) / radius, dy = (y - lobe.y) / radius;
                float screenDy = gapEdgeTop ? dy : -dy; // light always comes from the top of the screen
                Color32 c;
                if (dx * dx + dy * dy <= 1f && geomRow < BoneKnobHeight)
                {
                    float light = -dx * 0.7f - screenDy * 0.7f;
                    c = light > 0.6f ? BoneHighlight : light > 0.25f ? BoneLight : light < -0.75f ? BoneWarm
                        : light < -0.45f ? BoneDeep : light < -0.1f ? BoneShade : BoneBase;
                }
                else
                {
                    float half = NeckHalf(y);
                    c = BoneCylinder((px - (16f - half)) / (2f * half));
                }
                if (Mathf.Abs(px - 16f) < 0.6f && geomRow >= 2 && geomRow <= 9) c = geomRow <= 3 ? Pal.Ink : BoneDeep; // crease between condyles
                pc.Set(x, row, c);
            }
            return pc;
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
            GateBeam = new Sprite[GateMain.Length][];
            GateEmitter = new Sprite[GateMain.Length];
            for (int v = 0; v < GateMain.Length; v++)
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
        public static Sprite BuildSky(int rows, string[] palette = null)
        {
            palette = palette ?? SkyHex;
            var cols = new Color32[palette.Length];
            for (int i = 0; i < cols.Length; i++) cols[i] = Pal.Hex(palette[i]);
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
                { 'K', Pal.Ink }, { 'h', BoneHighlight }, { 'O', BoneLight }, { 'o', BoneBase },
                { 'd', BoneShade }, { 'D', BoneDeep },
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
