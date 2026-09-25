using UnityEngine;
using UnityEngine.UI;

namespace TapOrDrag
{
    /// <summary>
    /// DOG BLAST hangar overlay, opened from the shooter title panel. Two tabs: permanent ship upgrades and
    /// consumable items (bombs, shield charm, lucky bone). Reads ShipUpgrades / Inventory directly; purchases are
    /// raised as events so GameManager owns the economy side effects and sounds.
    /// </summary>
    public partial class Hud
    {
        const int HangarRows = 5;
        const float RowHeight = 84f, RowSpacing = 92f, RowTop = -186f;

        sealed class HangarRow
        {
            public RectTransform Root, Buy, Toggle;
            public Image Icon;
            public PixelText Name, Line2, Line3, BuyText, ToggleText;
            public float Shake;
        }

        RectTransform hangarRoot, tabUpgrades, tabItems, hangarBack;
        PixelText hangarCoins, tabUpgradesText, tabItemsText;
        readonly HangarRow[] hangarRows = new HangarRow[HangarRows];
        int hangarTab; // 0 upgrades, 1 items

        public bool HangarOpen { get; private set; }
        public event System.Action HangarOpenRequested, HangarCloseRequested;
        public event System.Action<UpgradeStat> UpgradeBuyRequested;
        public event System.Action<ItemKind> ItemBuyRequested, ItemToggleRequested;

        void BuildHangar()
        {
            hangarRoot = NewRect("Hangar", frame);
            Stretch(hangarRoot);
            var dim = NewImage(hangarRoot, "Dim", null, 1f, Mid, Mid, Vector2.zero);
            Stretch(dim.rectTransform);
            dim.color = new Color(0.05f, 0.03f, 0.1f, 0.94f);
            dim.raycastTarget = true; // swallow clicks meant for the title screen underneath

            PixelText.Create(hangarRoot, "Title", "HANGAR", Pal.Orange, 5, Top, Top, new Vector2(0f, -36f));
            hangarCoins = PixelText.Create(hangarRoot, "Coins", "0 BONES", Pal.Gold, 3, Top, Top, new Vector2(0f, -88f));

            tabUpgrades = NewButton(hangarRoot, "TabUpgrades", Top, new Vector2(-92f, -140f), new Vector2(176f, 40f), () => SetHangarTab(0));
            AddPanel(tabUpgrades);
            tabUpgradesText = PixelText.Create(tabUpgrades, "Label", "UPGRADES", Pal.White, 2, Mid, Mid, Vector2.zero);
            tabItems = NewButton(hangarRoot, "TabItems", Top, new Vector2(92f, -140f), new Vector2(176f, 40f), () => SetHangarTab(1));
            AddPanel(tabItems);
            tabItemsText = PixelText.Create(tabItems, "Label", "ITEMS", Pal.White, 2, Mid, Mid, Vector2.zero);

            for (int i = 0; i < HangarRows; i++)
            {
                int index = i;
                var row = new HangarRow();
                var panel = NewImage(hangarRoot, "Row" + i, art.Panel, 1f, Top, Top, new Vector2(0f, RowTop - i * RowSpacing));
                panel.type = Image.Type.Sliced;
                panel.rectTransform.sizeDelta = new Vector2(376f, RowHeight);
                row.Root = panel.rectTransform;
                row.Icon = NewImage(row.Root, "Icon", art.IconBomb, 3f, TopLeft, Mid, new Vector2(24f, -26f));
                row.Name = PixelText.Create(row.Root, "Name", "-", Pal.White, 2, TopLeft, TopLeft, new Vector2(46f, -14f));
                row.Line2 = PixelText.Create(row.Root, "Line2", "-", Pal.Lilac, 2, TopLeft, TopLeft, new Vector2(46f, -36f));
                row.Line3 = PixelText.Create(row.Root, "Line3", "-", Pal.OrangeLight, 2, TopLeft, TopLeft, new Vector2(46f, -58f));
                row.Buy = NewButton(row.Root, "Buy", new Vector2(1f, 0.5f), new Vector2(-50f, 12f), new Vector2(84f, 40f), () => OnHangarBuy(index));
                AddPanel(row.Buy);
                row.BuyText = PixelText.Create(row.Buy, "Price", "50", Pal.Gold, 2, Mid, Mid, Vector2.zero);
                row.Toggle = NewButton(row.Root, "Toggle", new Vector2(1f, 0.5f), new Vector2(-50f, -24f), new Vector2(84f, 26f), () => OnHangarToggle(index));
                row.ToggleText = PixelText.Create(row.Toggle, "State", "ON", Pal.White, 2, Mid, Mid, Vector2.zero);
                hangarRows[i] = row;
            }

            hangarBack = NewButton(hangarRoot, "Back", BottomMid, new Vector2(0f, 56f), new Vector2(200f, 52f), () => HangarCloseRequested?.Invoke());
            AddPanel(hangarBack);
            PixelText.Create(hangarBack, "Label", "BACK", Pal.White, 3, Mid, Mid, Vector2.zero);

            hangarRoot.gameObject.SetActive(false);
        }

        /// <summary>While open, every screen point counts as a button so taps never start a run.</summary>
        bool HangarButtonHit(Vector2 screenPos) => HangarOpen;

        public void ShowHangar(bool open, int coins)
        {
            HangarOpen = open;
            hangarRoot.gameObject.SetActive(open);
            hangarRoot.SetAsLastSibling();
            if (open) RefreshHangar(coins);
        }

        void SetHangarTab(int tab)
        {
            if (hangarTab == tab) return;
            hangarTab = tab;
            audioManager.Click();
            RefreshHangar(Economy.Coins);
        }

        public void RefreshHangar(int coins)
        {
            hangarCoins.Set(coins + " BONES");
            tabUpgradesText.SetColor(hangarTab == 0 ? Pal.Gold : Pal.Lilac);
            tabItemsText.SetColor(hangarTab == 1 ? Pal.Gold : Pal.Lilac);

            for (int i = 0; i < HangarRows; i++)
            {
                var row = hangarRows[i];
                if (hangarTab == 0)
                {
                    var stat = ShipUpgrades.All[i];
                    int level = ShipUpgrades.Level(stat), max = ShipUpgrades.MaxLevel(stat), cost = ShipUpgrades.NextCost(stat);
                    row.Root.gameObject.SetActive(true);
                    row.Icon.sprite = UpgradeIcon(stat);
                    row.Name.Set(ShipUpgrades.Names[i]);
                    row.Line2.Set(ShipUpgrades.Effects[i]);
                    row.Line3.Set("LV " + level + "/" + max);
                    SetBuyLabel(row, cost < 0 ? "MAX" : cost.ToString(), cost >= 0 && coins >= cost);
                    row.Toggle.gameObject.SetActive(false);
                }
                else if (i < Inventory.All.Length)
                {
                    var item = Inventory.All[i];
                    int price = Inventory.Price(item);
                    row.Root.gameObject.SetActive(true);
                    row.Icon.sprite = ItemIcon(item);
                    row.Name.Set(Inventory.Names[i]);
                    row.Line2.Set(Inventory.Descriptions[i]);
                    row.Line3.Set("OWNED " + Inventory.Count(item));
                    SetBuyLabel(row, price.ToString(), coins >= price);
                    bool passive = Inventory.IsPassive(item);
                    row.Toggle.gameObject.SetActive(passive);
                    if (passive)
                    {
                        bool on = Inventory.IsEnabled(item);
                        row.ToggleText.Set(on ? "ON" : "OFF");
                        row.ToggleText.SetColor(on ? Pal.Hex("7dff9a") : Pal.Lilac);
                    }
                }
                else row.Root.gameObject.SetActive(false);
                row.Icon.rectTransform.sizeDelta = row.Icon.sprite.rect.size * 3f;
            }
        }

        static void SetBuyLabel(HangarRow row, string text, bool affordable)
        {
            row.BuyText.Set(text);
            row.BuyText.SetColor(text == "MAX" ? Pal.Lilac : affordable ? Pal.Gold : Pal.Red);
        }

        /// <summary>Shake the price on a row the player could not afford.</summary>
        public void HangarBuyFailed(int row)
        {
            if (row >= 0 && row < HangarRows) hangarRows[row].Shake = 0.35f;
        }

        void OnHangarBuy(int row)
        {
            if (hangarTab == 0) UpgradeBuyRequested?.Invoke(ShipUpgrades.All[row]);
            else ItemBuyRequested?.Invoke(Inventory.All[row]);
        }

        void OnHangarToggle(int row)
        {
            if (hangarTab == 1) ItemToggleRequested?.Invoke(Inventory.All[row]);
        }

        Sprite UpgradeIcon(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.Firepower: return art.PowerCapsule;
                case UpgradeStat.FireRate: return art.IconRapid;
                case UpgradeStat.Armor: return art.IconHeart;
                case UpgradeStat.Magnet: return art.IconMagnet;
                default: return art.IconGold;
            }
        }

        Sprite ItemIcon(ItemKind item) =>
            item == ItemKind.Bomb ? art.IconBomb : item == ItemKind.ShieldCharm ? art.IconShieldItem : art.IconLucky;

        void TickHangar(float dt)
        {
            if (!HangarOpen) return;
            foreach (var row in hangarRows)
            {
                if (row.Shake <= 0f)
                {
                    row.BuyText.Rect.anchoredPosition = Vector2.zero;
                    continue;
                }
                row.Shake -= dt;
                row.BuyText.Rect.anchoredPosition = new Vector2(Random.Range(-5f, 5f), 0f);
            }
        }
    }
}
