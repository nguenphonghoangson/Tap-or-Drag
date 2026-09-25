using UnityEngine;

namespace TapOrDrag
{
    public enum ItemKind { Bomb, ShieldCharm, LuckyBone }

    /// <summary>
    /// Consumables bought with bones in the Hangar. Bombs are used from the in-run button; passive items (shield charm,
    /// lucky bone) are consumed automatically at the start of a DOG BLAST run while their toggle is ON.
    /// </summary>
    public static class Inventory
    {
        public static readonly ItemKind[] All = { ItemKind.Bomb, ItemKind.ShieldCharm, ItemKind.LuckyBone };
        public static readonly string[] Names = { "BOMB", "SHIELD CHARM", "LUCKY BONE" };
        public static readonly string[] Descriptions = { "CLEARS THE SCREEN", "START SHIELDED", "X2 BONES FOR 1 RUN" };
        static readonly int[] Prices = { 40, 60, 80 };
        static readonly bool[] Passives = { false, true, true };

        static string CountKey(ItemKind item) => "TapOrDrag.Item." + item;
        static string ToggleKey(ItemKind item) => "TapOrDrag.ItemOn." + item;

        public static int Count(ItemKind item) => Mathf.Max(0, PlayerPrefs.GetInt(CountKey(item), 0));
        public static int Price(ItemKind item) => Prices[(int)item];
        public static bool IsPassive(ItemKind item) => Passives[(int)item];
        public static bool IsEnabled(ItemKind item) => PlayerPrefs.GetInt(ToggleKey(item), 1) == 1;

        public static void SetEnabled(ItemKind item, bool on)
        {
            PlayerPrefs.SetInt(ToggleKey(item), on ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void Add(ItemKind item, int amount)
        {
            PlayerPrefs.SetInt(CountKey(item), Count(item) + amount);
            PlayerPrefs.Save();
        }

        public static bool TryUse(ItemKind item)
        {
            if (Count(item) <= 0) return false;
            Add(item, -1);
            return true;
        }

        /// <summary>Consumes a passive item for this run if it is owned and switched on.</summary>
        public static bool ConsumePassive(ItemKind item) => IsPassive(item) && IsEnabled(item) && TryUse(item);

        public static bool TryBuy(ItemKind item)
        {
            if (Economy.Coins < Price(item)) return false;
            Economy.Add(-Price(item));
            Economy.Save();
            Add(item, 1);
            return true;
        }

        public static void ResetAll()
        {
            foreach (var item in All)
            {
                PlayerPrefs.DeleteKey(CountKey(item));
                PlayerPrefs.DeleteKey(ToggleKey(item));
            }
        }
    }
}
