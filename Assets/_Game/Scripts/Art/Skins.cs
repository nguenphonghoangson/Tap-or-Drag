using System.Collections.Generic;

namespace TapOrDrag
{
    public enum HatKind { None, Beanie, Cap, Crown, Headband, Antenna, TopHat }

    public enum SkillKind { None, Shield, RoyalCombo, ShadowDash, AntiGrav, Midas }

    /// <summary>
    /// Bird skin = recolour of the shared bird pixel map + optional scarf + a hat. Unlocked by best score.
    /// Colours are hex strings; hat colour keys match the characters used in that hat's pixel map (see Art.HatShapes).
    /// </summary>
    public sealed class SkinDef
    {
        public string Name;
        public int UnlockBest;
        public string Body, BodyLight, BodyShade, Beak, BeakShade, Cheek, Wing, WingShade;
        public string[] Scarf; // main, dark, light — null for no scarf
        public HatKind Hat;
        public Dictionary<char, string> HatColors;
        public SkillKind Skill;
        public string SkillText;   // shown on the title screen (pixel font: A-Z 0-9 space ! . : + - = > x ?)
        public string SkillColor;

        public static readonly SkinDef[] All =
        {
            new SkinDef
            {
                Name = "CHICK", UnlockBest = 0,
                Skill = SkillKind.None, SkillText = "NO SKILL", SkillColor = "c9b6f0",
                Body = "ffd23f", BodyLight = "fff3a8", BodyShade = "f09a1e", Beak = "ff7b2e", BeakShade = "d9481f",
                Cheek = "ff8fb0", Wing = "fffbe8", WingShade = "f5c86b",
                Scarf = new[] { "e8384f", "a8233f", "ff8d9b" },
                Hat = HatKind.Beanie,
                HatColors = new Dictionary<char, string>
                {
                    { 'R', "e8384f" }, { 'r', "c42a44" }, { 'H', "ff8d9b" }, { 'S', "fff1e6" }, { 's', "e6d2c8" }, { 'P', "fffaf2" }, { 'p', "e3d6ea" },
                },
            },
            new SkinDef
            {
                Name = "BLUEJAY", UnlockBest = 25,
                Skill = SkillKind.Shield, SkillText = "SHIELD: BLOCKS 1 HIT", SkillColor = "3ff0ff",
                Body = "4fa8ff", BodyLight = "c4e6ff", BodyShade = "2e6fd1", Beak = "ffb03a", BeakShade = "d97a1e",
                Cheek = "ff9ac0", Wing = "ffffff", WingShade = "b9dcff",
                Scarf = new[] { "ffd23f", "d99a1e", "fff3a8" },
                Hat = HatKind.Cap,
                HatColors = new Dictionary<char, string> { { 'C', "ff5f3a" }, { 'H', "ff9a7a" }, { 'B', "c43d28" } },
            },
            new SkinDef
            {
                Name = "FLAMINGO", UnlockBest = 50,
                Skill = SkillKind.RoyalCombo, SkillText = "FAST COMBO + SOFT MISS", SkillColor = "ff8fc4",
                Body = "ff8fc4", BodyLight = "ffd1e6", BodyShade = "e0609e", Beak = "fff1e6", BeakShade = "2a1633",
                Cheek = "ff5f8f", Wing = "fff0f7", WingShade = "ffb3d6",
                Scarf = new[] { "3fd6c0", "1f8f86", "a8fff0" },
                Hat = HatKind.Crown,
                HatColors = new Dictionary<char, string> { { 'Y', "ffd23f" }, { 'H', "fff3a8" }, { 'D', "d99a1e" }, { 'R', "3ff0ff" } },
            },
            new SkinDef
            {
                Name = "NINJA", UnlockBest = 100,
                Skill = SkillKind.ShadowDash, SkillText = "DASH THROUGH PIPES", SkillColor = "b58cff",
                Body = "5a5478", BodyLight = "8a82b0", BodyShade = "3a3552", Beak = "ffb03a", BeakShade = "d97a1e",
                Cheek = "7a5a9a", Wing = "8a82b0", WingShade = "5a5478",
                Scarf = null,
                Hat = HatKind.Headband,
                HatColors = new Dictionary<char, string> { { 'R', "e8384f" } },
            },
            new SkinDef
            {
                Name = "ROBO", UnlockBest = 200,
                Skill = SkillKind.AntiGrav, SkillText = "LOW GRAVITY", SkillColor = "8fe8ff",
                Body = "b8c4d6", BodyLight = "eef4ff", BodyShade = "7c8aa6", Beak = "ffd23f", BeakShade = "d99a1e",
                Cheek = "3ff0ff", Wing = "dfe8f5", WingShade = "9aa8c0",
                Scarf = new[] { "3ff0ff", "1f7dff", "eaffff" },
                Hat = HatKind.Antenna,
                HatColors = new Dictionary<char, string> { { 'C', "ff4d6d" }, { 'W', "ffffff" }, { 'G', "7c8aa6" } },
            },
            new SkinDef
            {
                Name = "GOLD", UnlockBest = 400,
                Skill = SkillKind.Midas, SkillText = "DOUBLE POINTS", SkillColor = "ffd23f",
                Body = "ffcf40", BodyLight = "fffbd0", BodyShade = "c98a18", Beak = "ff6a2a", BeakShade = "c8401e",
                Cheek = "ff8fb0", Wing = "fff3b0", WingShade = "e8b43a",
                Scarf = new[] { "9a4dff", "5e2bb0", "c9a0ff" },
                Hat = HatKind.TopHat,
                HatColors = new Dictionary<char, string> { { 'T', "2e2440" }, { 'H', "54467a" }, { 'B', "9a4dff" } },
            },
        };
    }
}
