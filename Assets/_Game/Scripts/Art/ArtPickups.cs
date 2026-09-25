using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// DOG BLAST pickups, drawn big and readable: support items are round badges (own colour + symbol), weapons are
    /// rounded glass capsules with a picture of the shot. Also the boomerang projectile. Replaces the letter capsules.
    /// </summary>
    public sealed partial class Art
    {
        public Sprite BadgeMagnet, BadgeShield, BadgeWingman, BadgeRapid, BadgeBomb, BadgeHeart, BadgePower;
        public Sprite BoomerangShot;

        static readonly string[] BoomerangMap = { ".....ww.", "....wwww", "...bbdw.", "..bbd...", ".bbd....", "..bbd...", "...bbdw.", "....wwww", ".....ww." };

        void BuildPickupArt()
        {
            Color32 white = Pal.White;
            BadgeMagnet = Badge("4a3aa8", new[] { "ww.....ww", "ww.....ww", "rr.....rr", "rr.....rr", "rrr...rrr", ".rrr.rrr.", "..rrrrr..", "...rrr..." },
                new Dictionary<char, Color32> { { 'w', Pal.Hex("e6ecff") }, { 'r', Pal.Red } });
            BadgeShield = Badge("1f8f86", new[] { ".sssssss.", "sSSSsSSSs", "sSSSsSSSs", "sssssssss", "sSSSsSSSs", ".sSSsSSs.", "..sSsSs..", "...sss..." },
                new Dictionary<char, Color32> { { 's', white }, { 'S', Pal.Hex("8fe3ff") } });
            BadgeWingman = Badge("7a3dcc", new[] { "....w....", "...wcw...", "w..wcw..w", "wwwwwwwww", ".wwwwwww.", "...www...", "..w.w.w..", "....o...." },
                new Dictionary<char, Color32> { { 'w', white }, { 'c', Pal.Hex("8fe3ff") }, { 'o', Pal.Gold } });
            BadgeRapid = Badge("d9433a", new[] { "..y...y..", ".yWy.yWy.", ".yWy.yWy.", ".yWy.yWy.", ".yyy.yyy.", ".ooo.ooo.", "..o...o.." },
                new Dictionary<char, Color32> { { 'y', Pal.Gold }, { 'W', white }, { 'o', Pal.Orange } });
            BadgeBomb = Badge("e0a82a", new[] { ".....yf..", "....y..f.", "...k.....", ".kkkkk...", "kkhkkkk..", "khkkkkk..", "kkkkkkk..", ".kkkkk..." },
                new Dictionary<char, Color32> { { 'k', Pal.Hex("2b2540") }, { 'h', Pal.Hex("8a82b0") }, { 'y', Pal.Gold }, { 'f', Pal.Red } });
            BadgeHeart = Badge("c23f86", new[] { ".rr.rr.", "rwrrrrr", "rrrrrrr", ".rrrrr.", "..rrr..", "...r..." },
                new Dictionary<char, Color32> { { 'r', Pal.Red }, { 'w', white } });
            BadgePower = Badge("ff8a2a", new[] { "WWWWW.", "WW..WW", "WW..WW", "WWWWW.", "WW....", "WW....", "WW...." },
                new Dictionary<char, Color32> { { 'W', white } });

            CapsuleHoming = WeaponCapsule(HomingColor, new[] { "...w...", "..wgw..", "..ggg..", "..ggg..", ".ggggg.", "g.ggg.g", "...o...", "..oyo.." },
                new Dictionary<char, Color32> { { 'w', white }, { 'g', HomingColor }, { 'o', Pal.Orange }, { 'y', Pal.Gold } });
            CapsulePlasma = WeaponCapsule(PlasmaColor, new[] { "..ppp..", ".pPPPp.", "pPWWPPp", "pPWWPPp", "pPPPPPp", ".pPPPp.", "..ppp.." },
                new Dictionary<char, Color32> { { 'p', Pal.Hex("7a4fd6") }, { 'P', PlasmaColor }, { 'W', white } });
            CapsuleWave = WeaponCapsule(WaveColor, new[] { ".ccc...ccc..", "c...c.c...c.", ".....c.....c", "............", ".ccc...ccc..", "c...c.c...c.", ".....c.....c" },
                new Dictionary<char, Color32> { { 'c', WaveColor } });
            CapsuleLightning = WeaponCapsule(LightningColor, new[] { "...yyy", "..yyy.", ".yyy..", "yyyyyy", "..yyy.", ".yyy..", ".yy...", "y....." },
                new Dictionary<char, Color32> { { 'y', LightningColor } });
            var bonePal = new Dictionary<char, Color32> { { 'w', white }, { 'b', Pal.Hex("fff1d6") }, { 'd', Pal.Hex("d9b98a") } };
            CapsuleBoomerang = WeaponCapsule(BoomerangColor, BoomerangMap, bonePal);

            var shot = PixelCanvas.FromMap(BoomerangMap, bonePal, 1);
            shot.Outline(Pal.Ink, false);
            BoomerangShot = shot.ToSprite(Center);
        }

        static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        static void StampCentered(PixelCanvas pc, string[] map, Dictionary<char, Color32> pal)
        {
            int w = 0;
            foreach (var row in map) w = Mathf.Max(w, row.Length);
            pc.Stamp(map, pal, (pc.Width - w) / 2, (pc.Height - map.Length) / 2);
        }

        /// <summary>17x17 round badge: shaded disc, light upper rim, glint, symbol in the middle.</summary>
        static Sprite Badge(string mainHex, string[] symbol, Dictionary<char, Color32> pal)
        {
            const int size = 17;
            Color32 main = Pal.Hex(mainHex), black = new Color32(0, 0, 0, 255), white = Pal.White;
            Color32 dark = Mix(main, black, 0.45f), light = Mix(main, white, 0.45f);
            var pc = new PixelCanvas(size, size);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8.5f, 8.5f));
                if (d > 7.5f) continue;
                pc.Set(x, y, d > 6.5f ? (y < 8 ? light : dark) : Mix(main, black, 0.2f + 0.25f * (y / 16f)));
            }
            pc.Set(4, 4, white);
            pc.Set(5, 3, white);
            pc.Set(3, 5, light);
            StampCentered(pc, symbol, pal);
            pc.Outline(Pal.Ink, false);
            return pc.ToSprite(Center);
        }

        /// <summary>19x15 rounded capsule: rim in the weapon colour, dark glass window, glint, picture of the shot.</summary>
        static Sprite WeaponCapsule(Color32 main, string[] symbol, Dictionary<char, Color32> pal)
        {
            const int w = 19, h = 15;
            Color32 black = new Color32(0, 0, 0, 255);
            Color32 glass = Mix(main, black, 0.72f), rimDark = Mix(main, black, 0.35f);
            var pc = new PixelCanvas(w, h);
            for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                float cx = Mathf.Min(x - 3, 0) + Mathf.Max(x - 15, 0), cy = Mathf.Min(y - 3, 0) + Mathf.Max(y - 11, 0);
                if (Mathf.Sqrt(cx * cx + cy * cy) > 2.6f) continue;
                float ix = Mathf.Min(x - 4, 0) + Mathf.Max(x - 14, 0), iy = Mathf.Min(y - 4, 0) + Mathf.Max(y - 10, 0);
                bool rim = !(x > 2 && x < 16 && y > 2 && y < 12) || Mathf.Sqrt(ix * ix + iy * iy) > 1.6f;
                pc.Set(x, y, rim ? (y < 7 ? main : rimDark) : Mix(glass, main, 0.12f * (1f - y / 14f)));
            }
            pc.Set(3, 3, Pal.White);
            pc.Set(4, 3, Pal.White);
            pc.Set(3, 4, Pal.White);
            StampCentered(pc, symbol, pal);
            pc.Outline(Pal.Ink, false);
            return pc.ToSprite(Center);
        }
    }
}
