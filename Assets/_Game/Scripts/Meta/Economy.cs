using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Coin wallet and skin ownership, persisted in PlayerPrefs.</summary>
    public static class Economy
    {
        const string CoinsKey = "TapOrDrag.Coins";
        const string OwnedKey = "TapOrDrag.Owned";

        static int owned;

        public static int Coins { get; private set; }

        /// <summary>
        /// First run after skins became purchasable: skins already unlocked through the old best-score rule are kept.
        /// </summary>
        public static void Load(int bestScore)
        {
            Coins = PlayerPrefs.GetInt(CoinsKey, 0);
            if (PlayerPrefs.HasKey(OwnedKey))
            {
                owned = PlayerPrefs.GetInt(OwnedKey) | 1;
                return;
            }
            owned = 1;
            for (int i = 0; i < SkinDef.All.Length; i++)
                if (bestScore >= SkinDef.All[i].UnlockBest) owned |= 1 << i;
            Save();
        }

        public static bool Owns(int skin) => (owned & (1 << skin)) != 0;

        public static void Add(int amount) => Coins = Mathf.Max(0, Coins + amount);

        public static bool TryBuy(int skin)
        {
            if (Owns(skin)) return true;
            int price = SkinDef.All[skin].Price;
            if (Coins < price) return false;
            Coins -= price;
            owned |= 1 << skin;
            Save();
            return true;
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(CoinsKey, Coins);
            PlayerPrefs.SetInt(OwnedKey, owned);
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(CoinsKey);
            PlayerPrefs.DeleteKey(OwnedKey);
        }
    }
}
