using System.Collections.Generic;

namespace TapOrDrag
{
    public enum HatKind { None, Beanie, Cap, Crown, Headband, Antenna, TopHat }

    public enum SkillKind { None, Shield, RoyalCombo, ShadowDash, AntiGrav, Midas }

    /// <summary>Active skill of each ship in DOG BLAST (charged by kills, fired from the HUD button).</summary>
    public enum ShipSkill { BarkBlast, IceShield, Wingmen, TimeWarp, MegaLaser, GoldRush }

    /// <summary>
    /// Dog skin = recolour of the shared puppy pixel map + optional scarf + a hat. Bought with coins (see Economy).
    /// Colours are hex strings; hat colour keys match the characters used in that hat's pixel map (see Art.HatShapes).
    /// </summary>
    public sealed class SkinDef
    {
        public string Name;
        public int UnlockBest;   // legacy: skins reached with this best score before the coin shop are granted for free
        public int Price;        // coins
        public string Body, BodyLight, BodyShade, Beak, BeakShade, Cheek, Wing, WingShade; // dog pilot: Beak = tongue, BeakShade = nose, Wing/WingShade = saucer hull
        public string[] Scarf; // main, dark, light — null for no scarf
        public HatKind Hat;
        public Dictionary<char, string> HatColors;
        public SkillKind Skill;
        public string SkillText;   // shown on the title screen (pixel font: A-Z 0-9 space ! . : + - = > x ?)
        public string SkillColor;
        public ShipSkill ShipSkill;
        public string ShipSkillName, ShipSkillShort, ShipSkillText; // pixel-font safe

        public static readonly SkinDef[] All =
        {
            new SkinDef
            {
                Name = "SHIBA", UnlockBest = 0, Price = 0,
                ShipSkill = ShipSkill.BarkBlast, ShipSkillName = "BARK BLAST", ShipSkillShort = "BARK", ShipSkillText = "SHOCKWAVE CLEARS BULLETS",
                Skill = SkillKind.None, SkillText = "NO SKILL", SkillColor = "c9b6f0",
                Body = "f0a04b", BodyLight = "fff1d6", BodyShade = "c77a32", Beak = "ff6f91", BeakShade = "2a1633",
                Cheek = "ff9eb5", Wing = "3fd6c0", WingShade = "1f8f86",
                Scarf = new[] { "e8384f", "a8233f", "ff8d9b" },
                Hat = HatKind.Beanie,
                HatColors = new Dictionary<char, string>
                {
                    { 'R', "e8384f" }, { 'r', "c42a44" }, { 'H', "ff8d9b" }, { 'S', "fff1e6" }, { 's', "e6d2c8" }, { 'P', "fffaf2" }, { 'p', "e3d6ea" },
                },
            },
            new SkinDef
            {
                Name = "HUSKY", UnlockBest = 25, Price = 100,
                ShipSkill = ShipSkill.IceShield, ShipSkillName = "ICE SHIELD", ShipSkillShort = "SHIELD", ShipSkillText = "6S OF FULL PROTECTION",
                Skill = SkillKind.Shield, SkillText = "SHIELD: BLOCKS 1 HIT", SkillColor = "3ff0ff",
                Body = "9aa8c0", BodyLight = "f4f8ff", BodyShade = "6c7a96", Beak = "ff6f91", BeakShade = "2a1633",
                Cheek = "ff9ac0", Wing = "ff8a2a", WingShade = "c24b1a",
                Scarf = new[] { "ffd23f", "d99a1e", "fff3a8" },
                Hat = HatKind.Cap,
                HatColors = new Dictionary<char, string> { { 'C', "ff5f3a" }, { 'H', "ff9a7a" }, { 'B', "c43d28" } },
            },
            new SkinDef
            {
                Name = "POODLE", UnlockBest = 50, Price = 250,
                ShipSkill = ShipSkill.Wingmen, ShipSkillName = "WINGMEN", ShipSkillShort = "WINGS", ShipSkillText = "2 HELPER DRONES FOR 8S",
                Skill = SkillKind.RoyalCombo, SkillText = "FAST COMBO + SOFT MISS", SkillColor = "ff8fc4",
                Body = "ff9fcf", BodyLight = "ffe0f0", BodyShade = "e070a8", Beak = "ff6f91", BeakShade = "2a1633",
                Cheek = "ff5f8f", Wing = "fff0f7", WingShade = "d9a3c4",
                Scarf = new[] { "3fd6c0", "1f8f86", "a8fff0" },
                Hat = HatKind.Crown,
                HatColors = new Dictionary<char, string> { { 'Y', "ffd23f" }, { 'H', "fff3a8" }, { 'D', "d99a1e" }, { 'R', "3ff0ff" } },
            },
            new SkinDef
            {
                Name = "NINJA", UnlockBest = 100, Price = 500,
                ShipSkill = ShipSkill.TimeWarp, ShipSkillName = "TIME WARP", ShipSkillShort = "WARP", ShipSkillText = "SLOWS ALL CATS FOR 5S",
                Skill = SkillKind.ShadowDash, SkillText = "DASH THROUGH PIPES", SkillColor = "b58cff",
                Body = "5a5478", BodyLight = "8a82b0", BodyShade = "3a3552", Beak = "ff6f91", BeakShade = "2a1633",
                Cheek = "7a5a9a", Wing = "3a3048", WingShade = "221a30",
                Scarf = null,
                Hat = HatKind.Headband,
                HatColors = new Dictionary<char, string> { { 'R', "e8384f" } },
            },
            new SkinDef
            {
                Name = "ROBO", UnlockBest = 200, Price = 900,
                ShipSkill = ShipSkill.MegaLaser, ShipSkillName = "MEGA LASER", ShipSkillShort = "LASER", ShipSkillText = "HUGE BEAM FOR 3S",
                Skill = SkillKind.AntiGrav, SkillText = "LOW GRAVITY", SkillColor = "8fe8ff",
                Body = "b8c4d6", BodyLight = "eef4ff", BodyShade = "7c8aa6", Beak = "3ff0ff", BeakShade = "2a1633",
                Cheek = "3ff0ff", Wing = "dfe8f5", WingShade = "9aa8c0",
                Scarf = new[] { "3ff0ff", "1f7dff", "eaffff" },
                Hat = HatKind.Antenna,
                HatColors = new Dictionary<char, string> { { 'C', "ff4d6d" }, { 'W', "ffffff" }, { 'G', "7c8aa6" } },
            },
            new SkinDef
            {
                Name = "GOLDEN", UnlockBest = 400, Price = 1500,
                ShipSkill = ShipSkill.GoldRush, ShipSkillName = "GOLD RUSH", ShipSkillShort = "GOLD", ShipSkillText = "X2 BONES AND SCORE 8S",
                Skill = SkillKind.Midas, SkillText = "DOUBLE POINTS", SkillColor = "ffd23f",
                Body = "ffcf40", BodyLight = "fffbd0", BodyShade = "c98a18", Beak = "ff6f91", BeakShade = "2a1633",
                Cheek = "ff8fb0", Wing = "9a4dff", WingShade = "5e2bb0",
                Scarf = new[] { "9a4dff", "5e2bb0", "c9a0ff" },
                Hat = HatKind.TopHat,
                HatColors = new Dictionary<char, string> { { 'T', "2e2440" }, { 'H', "54467a" }, { 'B', "9a4dff" } },
            },
        };
    }
}
