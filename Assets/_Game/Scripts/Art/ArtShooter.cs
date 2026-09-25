using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Sprites for the DOG BLAST shooter mode: cat enemies (dogs vs cats), bullets, pickups and hearts.</summary>
    public sealed partial class Art
    {
        public Sprite CatDrone, CatDiver, CatSaucer, CatBoss, CatMama, Kitten, Puppy, PuppyCage;
        public Sprite PlayerPellet, EnemyOrb, PowerCapsule, HeartPickup;
        public Sprite IconHeart, IconHeartEmpty;
        public Sprite IconPaw, IconBomb, IconMagnet, IconRapid, IconWingman, IconShieldItem, IconLucky, IconWarp, IconGold, ShipLaser;
        public static readonly Color32 EnemyBulletColor = Pal.Hex("ff5f8f");
        public static readonly Color32 PelletColor = Pal.Hex("8ff6ff");

        // Front-view cat head: ears with pink insides, glowing eyes, pink nose, little mouth.
        static readonly string[] CatHead =
        {
            "K...........K",
            "KK.........KK",
            "KpK.KKKKK.KpK",
            "KppKcccccKppK",
            ".KcccccccccK.",
            "KccEcccccEccK",
            "KccEcccccEccK",
            "KcccccNcccccK",
            ".KcccKcKcccK.",
            "..KKKKKKKKK..",
        };

        void BuildShooterArt()
        {
            CatDrone = MakeCatDrone(Pal.Hex("8f94b0"), Pal.Hex("c9cde0"));
            CatDiver = MakeCatDrone(Pal.Hex("e0605a"), Pal.Hex("ff9a8f"));
            CatMama = MakeCatDrone(Pal.Hex("c98a4a"), Pal.Hex("e8b27a"));
            Kitten = MakeCatDrone(Pal.Hex("f2c38b"), Pal.Hex("fff1d6"));
            BuildPuppyArt();
            CatSaucer = MakeCatSaucer(Pal.Hex("8f94b0"), Pal.Hex("ff6fb5"), Pal.Hex("c23f86"));
            CatBoss = MakeCatSaucer(Pal.Hex("3b3552"), Pal.Hex("7a3dcc"), Pal.Hex("4a2290"));

            var pellet = new PixelCanvas(3, 7);
            for (int y = 0; y < 7; y++)
            for (int x = 0; x < 3; x++)
                pellet.Set(x, y, x == 1 && y > 0 && y < 6 ? Pal.White : PelletColor);
            pellet.Set(0, 0, Pal.Clear); pellet.Set(2, 0, Pal.Clear); pellet.Set(0, 6, Pal.Clear); pellet.Set(2, 6, Pal.Clear);
            PlayerPellet = pellet.ToSprite(Center);

            var orb = new PixelCanvas(6, 6);
            for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(3f, 3f));
                if (d <= 3f) orb.Set(x, y, d < 1.3f ? Pal.White : EnemyBulletColor);
            }
            orb.Outline(Pal.Ink, false);
            EnemyOrb = orb.ToSprite(Center);

            var capsule = PixelCanvas.FromMap(new[]
            {
                ".KKKKKKKK.",
                "KooooooooK",
                "KoWWWWoooK",
                "KoWooWoooK",
                "KoWWWWoooK",
                "KoWooooooK",
                "KoWooooooK",
                "KddddddddK",
                ".KKKKKKKK.",
            }, new Dictionary<char, Color32> { { 'K', Pal.Ink }, { 'o', Pal.Orange }, { 'd', Pal.Hex("c24b1a") }, { 'W', Pal.White } });
            PowerCapsule = capsule.ToSprite(Center);

            string[] heart = { ".rr.rr.", "rhrrrrr", "rrrrrrr", ".rrrrr.", "..rrr..", "...r..." };
            HeartPickup = OutlinedPalette(heart, new Dictionary<char, Color32> { { 'r', Pal.Red }, { 'h', Pal.Hex("ffb3c1") } });
            IconHeart = HeartPickup;
            IconHeartEmpty = OutlinedPalette(heart, new Dictionary<char, Color32> { { 'r', Pal.Hex("4a3560") }, { 'h', Pal.Hex("4a3560") } });

            // Support items (pickups, buff icons, hangar icons).
            IconBomb = OutlinedPalette(new[] { "....ff.", "...f...", ".kkk...", "kkhkk..", "kkkkk..", "kkkkk..", ".kkk..." },
                new Dictionary<char, Color32> { { 'k', Pal.Hex("3b3552") }, { 'h', Pal.Hex("8a82b0") }, { 'f', Pal.Orange } });
            IconMagnet = OutlinedPalette(new[] { "rr...rr", "rr...rr", "rr...rr", "rr...rr", ".rr.rr.", "..rrr..", "ww...ww" },
                new Dictionary<char, Color32> { { 'r', Pal.Red }, { 'w', Pal.White } });
            IconRapid = OutlinedPalette(new[] { "...yy", "..yy.", ".yyyy", "yyyy.", "..yy.", ".yy..", "yy..." },
                new Dictionary<char, Color32> { { 'y', Pal.Gold } });
            IconWingman = OutlinedPalette(new[] { "..c..", ".ccc.", "ccccc", "c.c.c", "..c.." },
                new Dictionary<char, Color32> { { 'c', PelletColor } });
            IconShieldItem = OutlinedPalette(new[] { ".bbbbb.", "bwbbbbb", "bwbbbbb", "bbbbbbb", ".bbbbb.", "..bbb..", "...b..." },
                new Dictionary<char, Color32> { { 'b', Pal.Hex("3ff0ff") }, { 'w', Pal.White } });
            IconLucky = OutlinedPalette(new[] { "yy...yy", "yyyyyyy", "yy...yy" }, new Dictionary<char, Color32> { { 'y', Pal.Gold } });
            IconWarp = OutlinedPalette(new[] { "ppppp", ".ppp.", "..p..", ".ppp.", "ppppp" }, new Dictionary<char, Color32> { { 'p', PortalMain } });
            IconGold = OutlinedPalette(new[] { ".yyy.", "yyYyy", "yYyyy", "yyyyy", ".yyy." },
                new Dictionary<char, Color32> { { 'y', Pal.Gold }, { 'Y', Pal.White } });

            IconPaw = OutlinedPalette(new[] { ".o.o.o.", ".o.o.o.", ".......", "..ooo..", ".ooooo.", ".ooooo.", "..o.o.." },
                new Dictionary<char, Color32> { { 'o', Pal.Orange } });

            // ROBO mega laser: 16x7 tile, drawn tiled and rotated to point up.
            var laser = new PixelCanvas(16, 7);
            for (int x = 0; x < 16; x++)
            {
                laser.Set(x, 0, Pal.Alpha(PelletColor, 80));
                laser.Set(x, 1, Pal.Alpha(PelletColor, 170));
                for (int y = 2; y <= 4; y++) laser.Set(x, y, (x + y) % 5 == 0 ? PelletColor : Pal.White);
                laser.Set(x, 5, Pal.Alpha(PelletColor, 170));
                laser.Set(x, 6, Pal.Alpha(PelletColor, 80));
            }
            ShipLaser = laser.ToSprite(Center);
        }

        // Rescue pickups: a floppy-eared puppy, and the same puppy behind cage bars (shot open to free it).
        static readonly string[] PuppyFace = { "ee.....ee", "efffffffe", "efEfffEfe", ".fFFnFFf.", "..FFtFF..", "...fff..." };

        void BuildPuppyArt()
        {
            var pal = new Dictionary<char, Color32>
            {
                { 'e', Pal.Hex("8a5a2e") }, { 'f', Pal.Hex("f0c080") }, { 'F', Pal.Hex("fff1d6") },
                { 'E', Pal.Ink }, { 'n', Pal.Ink }, { 't', Pal.Hex("ff6f91") },
            };
            Puppy = OutlinedPalette(PuppyFace, pal);

            const int w = 15, h = 15;
            var cage = new PixelCanvas(w, h);
            cage.Stamp(PuppyFace, pal, 3, 6);
            Color32 bar = Pal.Hex("b8c0d8"), frame = Pal.Hex("5d6378");
            cage.Set(7, 0, frame);
            cage.Set(6, 1, frame);
            cage.Set(8, 1, frame);
            for (int x = 1; x < w - 1; x++)
            {
                cage.Set(x, 2, frame);
                cage.Set(x, h - 2, frame);
            }
            for (int x = 1; x < w - 1; x += 3)
                for (int y = 3; y < h - 2; y++) cage.Set(x, y, bar);
            cage.Outline(Pal.Ink, false);
            PuppyCage = cage.ToSprite(Center);
        }

        static Sprite OutlinedPalette(string[] map, Dictionary<char, Color32> pal)
        {
            var pc = PixelCanvas.FromMap(map, pal, 1);
            pc.Outline(Pal.Ink, true);
            return pc.ToSprite(Center);
        }

        static Dictionary<char, Color32> CatPalette(Color32 fur) => new Dictionary<char, Color32>
        {
            { 'K', Pal.Ink }, { 'c', fur }, { 'p', Pal.Hex("ff9eb5") }, { 'E', Pal.Hex("c8ff5a") }, { 'N', Pal.Hex("ff6f91") },
        };

        static Sprite MakeCatDrone(Color32 fur, Color32 light)
        {
            var pc = new PixelCanvas(13, 13);
            pc.Stamp(CatHead, CatPalette(fur), 0, 0);
            for (int x = 3; x < 10; x++) pc.Set(x, 10, Pal.Hex("5d4a80"));      // little hover pod
            for (int x = 4; x < 9; x++) pc.Set(x, 11, Pal.Hex("8ff6ff"));
            pc.Set(6, 12, Pal.White);
            return pc.ToSprite(Center);
        }

        static Sprite MakeCatSaucer(Color32 fur, Color32 hull, Color32 hullShade)
        {
            const int w = 21, h = 16;
            var pc = new PixelCanvas(w, h);
            pc.Stamp(CatHead, CatPalette(fur), 4, 0);
            const float cx = 10.5f, cy = 11.5f, rx = 10f, ry = 3.4f;
            Color32 lights = Pal.Gold;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy > 1f) continue;
                Color32 c = dy < -0.5f ? Pal.Shade(hull, 1.2f) : dy < 0.15f ? hull : hullShade;
                if (y == 12 && x % 3 == 1 && Mathf.Abs(dx) < 0.85f) c = lights;
                pc.Set(x, y, c);
            }
            pc.Outline(Pal.Ink, false);
            return pc.ToSprite(Center);
        }
    }
}
