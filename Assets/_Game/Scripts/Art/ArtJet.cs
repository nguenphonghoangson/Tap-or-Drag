using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// DOG BLAST player ship: a top-down fighter jet (nose up) with the dog in a glass cockpit. One set per skin,
    /// hull in the skin's Wing colours, wing tips and spine stripe in its scarf colour. Rolling frames shorten and
    /// darken the dipping wing.
    /// </summary>
    public sealed partial class Art
    {
        public const int JetWidth = 27, JetHeight = 27;
        const int JetCx = 13;

        public Sprite[][] Jets;   // [skin][0 level, 1 roll left, 2 roll right]
        public Sprite[] JetFlame; // [flicker frame], pivot top-center, placed under the nozzles

        static readonly string[] CockpitDog = { "e.....e", "eefffee", "fEfffEf", "fFFnFFf", ".FFtFF." };

        void BuildJetArt()
        {
            Jets = new Sprite[SkinDef.All.Length][];
            for (int i = 0; i < SkinDef.All.Length; i++)
                Jets[i] = new[] { MakeJet(SkinDef.All[i], 0), MakeJet(SkinDef.All[i], -1), MakeJet(SkinDef.All[i], 1) };

            var flamePal = new Dictionary<char, Color32> { { 'W', Pal.White }, { 'y', Pal.Gold }, { 'o', Pal.Orange }, { 'r', Pal.Red } };
            JetFlame = new[]
            {
                PixelCanvas.FromMap(new[] { ".yWy.", ".oyo.", "..o..", "..r..", "....." }, flamePal).ToSprite(TopCenter),
                PixelCanvas.FromMap(new[] { ".yWy.", ".yWy.", ".oyo.", "..o..", "..r.." }, flamePal).ToSprite(TopCenter),
            };
        }

        /// <summary>Half-width of the fuselage per row (-1 = empty).</summary>
        static int JetHullHalf(int y) =>
            y <= 1 ? 0 : y <= 3 ? 1 : y == 4 ? 2 : y == 5 ? 3 : y == 6 ? 4 : y <= 13 ? 5 : y <= 20 ? 4 : y <= 23 ? 3 : y == 24 ? 2 : -1;

        /// <summary>Half-width of the cockpit glass per row (-1 = none).</summary>
        static int JetGlassHalf(int y) => y == 5 ? 1 : y == 6 ? 2 : y >= 7 && y <= 12 ? 4 : y == 13 ? 2 : -1;

        static Sprite MakeJet(SkinDef skin, int bank)
        {
            var pc = new PixelCanvas(JetWidth, JetHeight);
            Color32 hull = Pal.Hex(skin.Wing), hullDark = Pal.Hex(skin.WingShade), hullLight = Pal.Shade(hull, 1.3f);
            Color32 accent = skin.Scarf != null ? Pal.Hex(skin.Scarf[0]) : Pal.White;
            Color32 gun = Pal.Hex("b8c0d8"), metal = Pal.Hex("5d6378");

            // Wings and tail fins first; the fuselage is painted over their roots.
            foreach (int side in new[] { -1, 1 })
            {
                bool dip = bank == side;
                int length = dip ? 6 : 8;
                Color32 main = dip ? hullDark : hull, lead = dip ? hull : hullLight, trail = dip ? Pal.Shade(hullDark, 0.8f) : hullDark;
                for (int k = 1; k <= length; k++)
                {
                    int x = JetCx + side * (4 + k), top = 13 + Mathf.FloorToInt(k * 0.75f + 0.5f) + (dip ? 1 : 0);
                    const int bottom = 20;
                    for (int y = top; y <= bottom; y++)
                        pc.Set(x, y, k >= length - 1 ? accent : y == top ? lead : y == bottom ? trail : main);
                    if (k == length - 2) // wing cannon
                        for (int y = top - 3; y < top; y++) pc.Set(x, y, y == top - 3 ? metal : gun);
                }
                for (int k = 1; k <= 4; k++)
                {
                    int x = JetCx + side * (3 + k), top = 21 + Mathf.FloorToInt(k * 0.5f + 0.5f);
                    for (int y = top; y <= 24; y++)
                        pc.Set(x, y, y == top ? (dip ? hull : hullLight) : y == 24 ? hullDark : (dip ? hullDark : hull));
                }
            }

            for (int y = 0; y <= 24; y++)
            {
                int w = JetHullHalf(y);
                for (int x = JetCx - w; x <= JetCx + w; x++)
                {
                    bool stripe = x == JetCx && (y >= 14 && y <= 22 || y == 2 || y == 3);
                    pc.Set(x, y, w > 0 && x == JetCx - w ? hullLight : w > 0 && x == JetCx + w ? hullDark : stripe ? accent : hull);
                }
            }
            pc.Set(JetCx, 0, hullLight);
            for (int x = JetCx - 2; x <= JetCx + 2; x++) pc.Set(x, 25, x == JetCx ? gun : metal); // nozzles

            // Cockpit: dark frame, glass with a glint, the dog looking forward.
            Color32 frame = Pal.Hex("3b3552"), glass = Pal.Hex("8fe3ff"), glint = Pal.Hex("e6fbff");
            for (int y = 4; y <= 14; y++)
            {
                int g = Mathf.Max(JetGlassHalf(y), Mathf.Max(JetGlassHalf(y - 1), JetGlassHalf(y + 1)));
                if (g < 0) continue;
                for (int x = JetCx - g - 1; x <= JetCx + g + 1; x++)
                    if (Mathf.Abs(x - JetCx) < JetHullHalf(y)) pc.Set(x, y, frame);
            }
            for (int y = 5; y <= 13; y++)
            {
                int g = JetGlassHalf(y);
                for (int x = JetCx - g; x <= JetCx + g; x++) pc.Set(x, y, glass);
            }
            pc.Set(JetCx - 3, 7, glint);
            pc.Set(JetCx - 3, 8, glint);
            pc.Set(JetCx - 1, 6, glint);
            pc.Stamp(CockpitDog, new Dictionary<char, Color32>
            {
                { 'e', Pal.Hex(skin.BodyShade) }, { 'f', Pal.Hex(skin.Body) }, { 'F', Pal.Hex(skin.BodyLight) },
                { 'E', Pal.Ink }, { 'n', Pal.Hex(skin.BeakShade) }, { 't', Pal.Hex(skin.Beak) },
            }, JetCx - 3, 7);

            pc.Outline(Pal.Ink, false);
            return pc.ToSprite(Center);
        }
    }
}
