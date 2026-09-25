using UnityEngine;
using UnityEngine.UI;

namespace TapOrDrag
{
    /// <summary>Meta UI: coin counter, skin shop button, daily missions and the coins earned on the game-over screen.</summary>
    public partial class Hud
    {
        PixelText coinText, buyText, goCoinsText, switchText, meteorMarker;
        bool meteorOn, meteorLocked;
        Vector3 meteorWorld;
        Image coinIcon, buyIcon, goCoinsIcon, gravityOverlay, switchIcon;
        RectTransform switchBadge;
        float switchPulse;
        bool gravityInverted;
        float metaTime;
        RectTransform buyButton, missionsPanel;
        readonly PixelText[] missionTexts = new PixelText[Missions.Count];
        readonly PixelText[] missionRewards = new PixelText[Missions.Count];
        readonly Image[] missionChecks = new Image[Missions.Count];
        float coinPulse, buyShake;

        public event System.Action BuyRequested;

        void BuildMeta()
        {
            // Coin counter under the best score.
            coinText = PixelText.Create(frame, "Coins", "0", Pal.White, 3, TopRight, TopRight, new Vector2(-12f, -50f));
            coinIcon = NewImage(frame, "CoinIcon", art.IconCoin, 3f, TopRight, TopRight, Vector2.zero);

            // Shop button under the skin name (only for skins not owned yet).
            buyButton = NewButton(skinSelector, "Buy", Mid, new Vector2(0f, -106f), new Vector2(150f, 36f), () => BuyRequested?.Invoke());
            AddPanel(buyButton);
            buyText = PixelText.Create(buyButton, "Price", "BUY 100", Pal.Gold, 3, Mid, Mid, new Vector2(-12f, 0f));
            buyIcon = NewImage(buyButton, "Coin", art.IconCoin, 2f, Mid, Mid, Vector2.zero);
            buyButton.gameObject.SetActive(false);

            // Daily missions (replaces the tutorial panel once the player knows the controls).
            var missions = NewImage(readyGroup, "Missions", art.Panel, 1f, BottomMid, BottomMid, new Vector2(0f, 180f));
            missions.type = Image.Type.Sliced;
            missionsPanel = missions.rectTransform;
            missionsPanel.sizeDelta = new Vector2(300f, 136f);
            PixelText.Create(missionsPanel, "Title", "DAILY MISSIONS", Pal.Gold, 2, Top, Top, new Vector2(0f, -12f));
            for (int i = 0; i < Missions.Count; i++)
            {
                float y = -40f - i * 28f;
                missionChecks[i] = NewImage(missionsPanel, "Check" + i, art.IconCheck, 2f, TopLeft, TopLeft, new Vector2(12f, y - 2f));
                missionTexts[i] = PixelText.Create(missionsPanel, "Mission" + i, "-", Pal.White, 2, TopLeft, TopLeft, new Vector2(34f, y));
                missionRewards[i] = PixelText.Create(missionsPanel, "Reward" + i, "+50", Pal.Gold, 2, TopRight, TopRight, new Vector2(-12f, y));
            }
            missionsPanel.gameObject.SetActive(false);

            // GOLD/BLUE switch state (visible once switch walls appear in the run).
            switchBadge = NewRect("SwitchBadge", playGroup);
            switchBadge.anchorMin = switchBadge.anchorMax = switchBadge.pivot = TopLeft;
            switchBadge.anchoredPosition = new Vector2(12f, -62f);
            switchIcon = NewImage(switchBadge, "Icon", art.SwitchIcon[0], 3f, TopLeft, TopLeft, Vector2.zero);
            switchText = PixelText.Create(switchBadge, "State", "GOLD", Art.SwitchMain[0], 2, TopLeft, TopLeft, new Vector2(30f, -2f));
            switchBadge.gameObject.SetActive(false);

            // Meteor warning at the right edge of the play field (positioned in TickMeta from a world height).
            meteorMarker = PixelText.Create(playGroup, "MeteorWarning", "!", Pal.Gold, 6, Mid, new Vector2(1f, 0.5f), Vector2.zero);
            meteorMarker.Visible = false;

            // Flipped-gravity tint (under the HUD, over the world).
            gravityOverlay = NewImage(transform, "GravityOverlay", null, 1f, Mid, Mid, Vector2.zero);
            Stretch(gravityOverlay.rectTransform);
            gravityOverlay.transform.SetSiblingIndex(0);
            gravityOverlay.enabled = false;

            // Game over: coins earned this run.
            goCoinsText = PixelText.Create(gameOverGroup, "RunCoins", "+0", Pal.Gold, 4, Top, Top, new Vector2(14f, -532f));
            goCoinsIcon = NewImage(gameOverGroup, "RunCoinIcon", art.IconCoin, 3f, Top, Top, Vector2.zero);
        }

        void AddPanel(RectTransform button)
        {
            var bg = NewImage(button, "Panel", art.Panel, 1f, Mid, Mid, Vector2.zero);
            bg.type = Image.Type.Sliced;
            Stretch(bg.rectTransform);
        }

        bool MetaButtonHit(Vector2 screenPos) => Hit(buyButton, screenPos);

        // ---------------------------------------------------------------- coins & shop

        float coinY = -50f; // -12 in the shooter HUD (best score hidden there)

        public void SetCoins(int total, bool pulse)
        {
            coinText.Set(total.ToString());
            coinIcon.rectTransform.anchoredPosition = new Vector2(-12f - coinText.Width - 4f, coinY - (coinText.Height - coinIcon.rectTransform.sizeDelta.y) * 0.5f);
            if (pulse) coinPulse = 1f;
        }

        void SetBuyButton(bool show, int price, bool affordable)
        {
            buyButton.gameObject.SetActive(show);
            if (!show) return;
            buyText.SetColor(affordable ? Pal.Gold : Pal.Lilac);
            buyText.Set("BUY " + price);
            buyIcon.rectTransform.anchoredPosition = new Vector2(buyText.Rect.anchoredPosition.x + buyText.Width * 0.5f + 14f, 0f);
        }

        /// <summary>Not enough coins: shake the price.</summary>
        public void BuyFailed() => buyShake = 0.4f;

        // ---------------------------------------------------------------- missions

        public void SetReadyPanel(bool showMissions, bool shooterMode = false)
        {
            tutorialPanel.gameObject.SetActive(!shooterMode && !showMissions);
            missionsPanel.gameObject.SetActive(!shooterMode && showMissions);
            shooterPanel.gameObject.SetActive(shooterMode);
            upgradesButton.gameObject.SetActive(shooterMode);
            itemsButton.gameObject.SetActive(shooterMode);
        }

        public void SetMissionRow(int slot, string text, int reward, bool done)
        {
            missionTexts[slot].SetColor(done ? Pal.Hex("7dff9a") : Pal.White);
            missionTexts[slot].Set(text);
            missionRewards[slot].SetColor(done ? Pal.Lilac : Pal.Gold);
            missionRewards[slot].Set(done ? "DONE" : "+" + reward);
            missionChecks[slot].enabled = done;
        }

        public void ShowSwitchState(bool show, int state)
        {
            switchBadge.gameObject.SetActive(show);
            if (!show) return;
            switchIcon.sprite = art.SwitchIcon[state];
            switchText.SetColor(Art.SwitchMain[state]);
            switchText.Set(Art.SwitchNames[state]);
            switchPulse = 1f;
        }

        /// <summary>Warning marker at the right edge. Tracking: yellow, slow blink. Locked: red, fast blink.</summary>
        public void SetMeteorWarning(bool on, float worldY, bool locked)
        {
            meteorOn = on;
            meteorLocked = locked;
            meteorWorld = new Vector3(World.ViewWidth * 0.5f - 0.3f, worldY, 0f);
            meteorMarker.SetColor(locked ? Pal.Red : Pal.Gold);
            if (!on) meteorMarker.Visible = false;
        }

        public void SetGravityInverted(bool inverted)
        {
            gravityInverted = inverted;
            gravityOverlay.enabled = inverted;
        }

        // ---------------------------------------------------------------- game over

        void ShowRunCoins(int amount)
        {
            goCoinsText.Set("+" + amount);
            goCoinsIcon.rectTransform.anchoredPosition = new Vector2(goCoinsText.Rect.anchoredPosition.x - goCoinsText.Width * 0.5f - 18f, -532f - 6f);
        }

        void TickMeta(float dt)
        {
            metaTime += dt;
            if (meteorOn && cam != null)
            {
                Vector2 screen = cam.WorldToScreenPoint(meteorWorld);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(playGroup, screen, null, out var local))
                    meteorMarker.Rect.anchoredPosition = local;
                meteorMarker.Visible = ((int)(metaTime * (meteorLocked ? 14f : 6f)) & 1) == 0;
            }
            switchPulse = Mathf.MoveTowards(switchPulse, 0f, dt * 4f);
            switchBadge.localScale = Vector3.one * (1f + 0.35f * switchPulse);
            if (gravityInverted)
            {
                Color c = Art.PortalMain;
                gravityOverlay.color = new Color(c.r, c.g, c.b, 0.1f + 0.03f * Mathf.Sin(metaTime * 4f));
            }

            coinPulse = Mathf.MoveTowards(coinPulse, 0f, dt * 3f);
            coinText.Rect.localScale = Vector3.one * (1f + 0.3f * coinPulse);

            if (buyShake > 0f)
            {
                buyShake -= dt;
                buyText.Rect.anchoredPosition = new Vector2(-12f + Random.Range(-5f, 5f), 0f);
                buyText.Tint = new Color(1f, 0.5f, 0.5f);
            }
            else
            {
                buyText.Rect.anchoredPosition = new Vector2(-12f, 0f);
                buyText.Tint = Color.white;
            }
        }
    }
}
