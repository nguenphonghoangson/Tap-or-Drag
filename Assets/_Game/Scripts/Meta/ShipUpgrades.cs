using UnityEngine;

namespace TapOrDrag
{
    public enum UpgradeStat { Firepower, FireRate, Armor, Magnet, SkillCharge }

    /// <summary>Permanent DOG BLAST ship upgrades bought with bones in the Hangar. Levels persist in PlayerPrefs.</summary>
    public static class ShipUpgrades
    {
        public static readonly UpgradeStat[] All =
            { UpgradeStat.Firepower, UpgradeStat.FireRate, UpgradeStat.Armor, UpgradeStat.Magnet, UpgradeStat.SkillCharge };
        public static readonly string[] Names = { "FIREPOWER", "FIRE RATE", "ARMOR", "MAGNET", "SKILL CHARGE" };
        public static readonly string[] Effects = { "+25% DAMAGE", "+8% FIRE SPEED", "+1 START HEART", "+30% PULL RANGE", "-10% CHARGE TIME" };
        static readonly int[] MaxLevels = { 5, 5, 3, 5, 5 };
        static readonly int[] Costs = { 50, 100, 200, 350, 500 };

        static string Key(UpgradeStat stat) => "TapOrDrag.Upgrade." + stat;

        public static int Level(UpgradeStat stat) => Mathf.Clamp(PlayerPrefs.GetInt(Key(stat), 0), 0, MaxLevel(stat));
        public static int MaxLevel(UpgradeStat stat) => MaxLevels[(int)stat];

        /// <summary>Price of the next level, or -1 when maxed. Armor costs double (extra hearts are strong).</summary>
        public static int NextCost(UpgradeStat stat)
        {
            int level = Level(stat);
            if (level >= MaxLevel(stat)) return -1;
            return Costs[level] * (stat == UpgradeStat.Armor ? 2 : 1);
        }

        public static bool TryBuy(UpgradeStat stat)
        {
            int cost = NextCost(stat);
            if (cost < 0 || Economy.Coins < cost) return false;
            Economy.Add(-cost);
            PlayerPrefs.SetInt(Key(stat), Level(stat) + 1);
            Economy.Save();
            return true;
        }

        // Effect helpers used by ShooterGame.
        public static float DamageMultiplier => 1f + 0.25f * Level(UpgradeStat.Firepower);
        public static float FireIntervalMultiplier => 1f - 0.08f * Level(UpgradeStat.FireRate);
        public static int BonusHearts => Level(UpgradeStat.Armor);
        public static float MagnetMultiplier => 1f + 0.3f * Level(UpgradeStat.Magnet);
        public static float SkillChargeMultiplier => 1f - 0.1f * Level(UpgradeStat.SkillCharge);

        public static void ResetAll()
        {
            foreach (var stat in All) PlayerPrefs.DeleteKey(Key(stat));
        }
    }
}
