using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>DOG BLAST weapons (capsules and projectiles), extra bosses and a 1px sprite for world-space health bars.</summary>
    public sealed partial class Art
    {
        public Sprite CapsuleHoming, CapsulePlasma, CapsuleWave, CapsuleLightning, CapsuleBoomerang;
        public Sprite Missile, PlasmaOrb, WaveShot;
        public Sprite LaserCatBoss, YarnKingBoss;
        public Sprite Pixel; // white 1x1, tinted and scaled for health bars and laser telegraphs

        public static readonly Color32 HomingColor = Pal.Hex("7dff9a");
        public static readonly Color32 PlasmaColor = Pal.Hex("b58cff");
        public static readonly Color32 WaveColor = Pal.Hex("3ff0ff");
        public static readonly Color32 LightningColor = Pal.Hex("ffe45c");
        public static readonly Color32 BoomerangColor = Pal.Hex("ffb36b");

        void BuildWeaponArt()
        {
            var px = new PixelCanvas(1, 1);
            px.Set(0, 0, Pal.White);
            Pixel = px.ToSprite(Center);


            Missile = PixelCanvas.FromMap(new[] { ".W.", "GWG", "GGG", "GGG", "G.G", ".o." },
                new Dictionary<char, Color32> { { 'W', Pal.White }, { 'G', HomingColor }, { 'o', Pal.Orange } }).ToSprite(Center);

            var orb = new PixelCanvas(9, 9);
            for (int y = 0; y < 9; y++)
            for (int x = 0; x < 9; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(4.5f, 4.5f));
                if (d <= 4.5f) orb.Set(x, y, d < 1.8f ? Pal.White : d < 3.2f ? PlasmaColor : Pal.Alpha(PlasmaColor, 110));
            }
            PlasmaOrb = orb.ToSprite(Center);

            WaveShot = PixelCanvas.FromMap(new[] { "c....c", "cc..cc", ".cWWc." },
                new Dictionary<char, Color32> { { 'c', WaveColor }, { 'W', Pal.White } }).ToSprite(Center);

            LaserCatBoss = BuildLaserCat();
            YarnKingBoss = BuildYarnKing();
        }

        static Sprite BuildLaserCat()
        {
            var map = new[]
            {
                "KK.................KK",
                "KmK...............KmK",
                "KmmK.KKKKKKKKKKK.KmmK",
                "KmmmKmmmmmmmmmmmKmmmK",
                ".KmmmmmmmmmmmmmmmmmK.",
                "KmmKKKKKKKKKKKKKKKmmK",
                "KmmKRRRRRRRRRRRRRKmmK",
                "KmmKRWRRRRRRRRRWRKmmK",
                "KmmKKKKKKKKKKKKKKKmmK",
                "KmmmmmmmmmNmmmmmmmmmK",
                ".KmmmmKKKKKKKKKmmmmK.",
                "CCKmmmmmmmmmmmmmmmKCC",
                "CCCKmmmmmmmmmmmmmKCCC",
                ".CC.KKKKKKKKKKKKK.CC.",
            };
            var pal = new Dictionary<char, Color32>
            {
                { 'K', Pal.Ink }, { 'm', Pal.Hex("9aa8c0") }, { 'R', Pal.Red }, { 'W', Pal.White },
                { 'N', Pal.Hex("5d4a80") }, { 'C', Pal.Hex("5d4a80") },
            };
            return PixelCanvas.FromMap(map, pal).ToSprite(Center);
        }

        /// <summary>Giant ball of yarn with cat ears, a face and a crown.</summary>
        static Sprite BuildYarnKing()
        {
            const int w = 22, h = 24;
            var pc = new PixelCanvas(w, h);
            Color32 yarn = Pal.Hex("ff8fc4"), yarnDark = Pal.Hex("d9609e"), yarnLight = Pal.Hex("ffc4e1");
            var center = new Vector2(11f, 14f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, center);
                if (d > 9.5f) continue;
                // wound strands: diagonal stripes plus a curved band
                bool strand = ((int)(p.x + p.y * 0.6f) % 4 == 0) || Mathf.Abs(d - 6.5f) < 0.5f;
                Color32 c = strand ? yarnDark : (p.x + p.y < 20f ? yarnLight : yarn);
                pc.Set(x, y, c);
            }
            // ears
            string[] ear = { "K...", "KK..", "KpK.", "KppK" };
            var earPal = new Dictionary<char, Color32> { { 'K', Pal.Ink }, { 'p', yarn } };
            pc.Stamp(ear, earPal, 3, 2);
            pc.Stamp(new[] { "...K", "..KK", ".KpK", "KppK" }, earPal, 15, 2);
            // face
            var face = new Dictionary<char, Color32> { { 'E', Pal.Hex("c8ff5a") }, { 'K', Pal.Ink }, { 'N', Pal.Hex("ff6f91") } };
            pc.Stamp(new[] { "EE....EE", "EE....EE", "...N....", "..KKKK.." }, face, 7, 12);
            // crown
            pc.Stamp(new[] { "y..y..y", "yy.y.yy", "yyyyyyy", "yRyyyRy" },
                new Dictionary<char, Color32> { { 'y', Pal.Gold }, { 'R', Pal.Red } }, 7, 1);
            pc.Outline(Pal.Ink, false);
            return pc.ToSprite(Center);
        }
    }
}
