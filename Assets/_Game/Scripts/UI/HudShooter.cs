using UnityEngine;
using UnityEngine.UI;

namespace TapOrDrag
{
    /// <summary>
    /// UI for the mode selector on the title screen and the DOG BLAST shooter HUD: hearts, weapon, buff icons,
    /// skill and bomb buttons, and the boss health bar with its phase marker and WARNING banner.
    /// </summary>
    public partial class Hud
    {
        const int MaxHeartIcons = 8, HeartsPerRow = 4;
        const float BossBarWidth = 144f, BossBarY = -60f, BossLabelY = -78f; // bar right under the score, name below it
        static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        PixelText titleTop, titleMid, titleBottom, shooterPanelLine, waveText, weaponText, bossLabel, skillLabel, skillReadyText, bombCount, warningText, warningName;
        RectTransform modePrev, modeNext, upgradesButton, itemsButton, skillButton, bombButton;
        Image shooterPanel, bossBack, bossFill, bossPhaseTick, skillFill, bombIcon;
        readonly Image[] heartIcons = new Image[MaxHeartIcons];
        Image[] buffIcons;
        Sprite[] buffSprites;
        Color32 bossColor = Pal.Rose;
        bool shooterHud, skillReady;
        float bossFrac, bossPunch, warningTime, skillPunch;

        /// <summary>Raised by the mode arrows on the title screen.</summary>
        public event System.Action<int> ModeStepRequested;
        public event System.Action SkillPressed, BombPressed;

        void BuildShooterUi()
        {
            // Mode switch: two halves of the middle title line.
            modePrev = NewButton(readyGroup, "ModePrev", Top, new Vector2(-78f, -171f), new Vector2(150f, 52f), () => ModeStepRequested?.Invoke(-1));
            modeNext = NewButton(readyGroup, "ModeNext", Top, new Vector2(78f, -171f), new Vector2(150f, 52f), () => ModeStepRequested?.Invoke(1));

            // Shooter hint: two text lines, no panel behind them.
            shooterPanel = NewImage(readyGroup, "ShooterTutorial", null, 1f, BottomMid, BottomMid, new Vector2(0f, 180f));
            shooterPanel.color = Color.clear;
            shooterPanel.rectTransform.sizeDelta = new Vector2(300f, 136f);
            PixelText.Create(shooterPanel.transform, "Move", "DRAG TO MOVE", Pal.OrangeLight, 3, Top, Top, new Vector2(0f, -40f));
            shooterPanelLine = PixelText.Create(shooterPanel.transform, "Fire", "AUTO FIRE - STOP THE CATS", Art.PelletColor, 2, Top, Top, new Vector2(0f, -74f));
            shooterPanel.gameObject.SetActive(false);

            // Hangar entry points: small icon buttons on the left/right edges, each opens straight on its page.
            upgradesButton = SmallIconButton("Upgrades", new Vector2(0f, 0.5f), new Vector2(46f, 40f), art.PowerCapsule, "UPGRADE", 0);
            itemsButton = SmallIconButton("Items", new Vector2(1f, 0.5f), new Vector2(-46f, 40f), art.IconBomb, "ITEMS", 1);

            // Hearts: two rows of four so armor upgrades still fit left of the score.
            for (int i = 0; i < MaxHeartIcons; i++)
            {
                heartIcons[i] = NewImage(playGroup, "Heart" + i, art.IconHeart, 3f, TopLeft, TopLeft,
                    new Vector2(12f + i % HeartsPerRow * 28f, -12f - i / HeartsPerRow * 26f));
                heartIcons[i].gameObject.SetActive(false);
            }
            weaponText = PixelText.Create(playGroup, "Weapon", "BLASTER LV1", Pal.OrangeLight, 2, TopRight, TopRight, new Vector2(-12f, -40f));
            weaponText.Visible = false;
            waveText = PixelText.Create(playGroup, "Wave", "WAVE 1", Pal.Lilac, 2, TopRight, TopRight, new Vector2(-12f, -60f));
            waveText.Visible = false;

            // Active buffs, right-aligned under the bone counter. Order matches ShooterGame.Buff* bits.
            buffSprites = new[] { art.IconMagnet, art.IconRapid, art.IconWingman, art.IconShieldItem, art.IconGold, art.IconWarp, art.IconShieldItem };
            buffIcons = new Image[buffSprites.Length];
            for (int i = 0; i < buffIcons.Length; i++)
            {
                buffIcons[i] = NewImage(playGroup, "Buff" + i, buffSprites[i], 3f, TopRight, TopRight, Vector2.zero);
                buffIcons[i].enabled = false;
            }

            // Boss bar: name, frame, fill, and a tick at 50% where phase 2 starts.
            bossLabel = PixelText.Create(playGroup, "BossLabel", "CAT MOTHERSHIP", Pal.Rose, 2, Top, Top, new Vector2(0f, BossLabelY));
            bossBack = NewImage(playGroup, "BossBarBack", null, 1f, Top, Top, new Vector2(0f, BossBarY));
            bossBack.rectTransform.sizeDelta = new Vector2(BossBarWidth + 6f, 14f);
            bossBack.color = new Color(0.08f, 0.04f, 0.14f, 0.85f);
            bossFill = NewImage(bossBack.transform, "Fill", null, 1f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(3f, 0f));
            bossFill.rectTransform.sizeDelta = new Vector2(BossBarWidth, 8f);
            bossPhaseTick = NewImage(bossBack.transform, "PhaseTick", null, 1f, Mid, Mid, Vector2.zero);
            bossPhaseTick.rectTransform.sizeDelta = new Vector2(2f, 14f);
            bossPhaseTick.color = Pal.White;
            ShowBossBar(false, 0f);

            warningText = PixelText.Create(playGroup, "Warning", "WARNING", Pal.Red, 6, Mid, Mid, new Vector2(0f, 60f));
            warningName = PixelText.Create(playGroup, "WarningName", "", Pal.White, 3, Mid, Mid, new Vector2(0f, 10f));
            warningText.Visible = warningName.Visible = false;

            // Skill button (bottom-right): fills from the bottom as kills charge it, pulses when ready.
            skillButton = NewButton(playGroup, "Skill", BottomRight, new Vector2(-58f, 62f), new Vector2(92f, 92f), () => SkillPressed?.Invoke());
            AddPanel(skillButton);
            skillFill = NewImage(skillButton, "Charge", null, 1f, BottomMid, BottomMid, new Vector2(0f, 6f));
            skillFill.color = new Color(Art.PelletColor.r / 255f, Art.PelletColor.g / 255f, Art.PelletColor.b / 255f, 0.45f);
            skillLabel = PixelText.Create(skillButton, "Label", "SKILL", Pal.White, 2, Mid, Mid, new Vector2(0f, 10f));
            skillReadyText = PixelText.Create(skillButton, "Ready", "0%", Pal.Lilac, 2, Mid, Mid, new Vector2(0f, -16f));

            // Bomb button (bottom-left) with the count of run pickups + hangar bombs.
            bombButton = NewButton(playGroup, "Bomb", BottomLeft, new Vector2(52f, 56f), new Vector2(80f, 80f), () => BombPressed?.Invoke());
            AddPanel(bombButton);
            bombIcon = NewImage(bombButton, "Icon", art.IconBomb, 5f, Mid, Mid, new Vector2(0f, 8f));
            bombCount = PixelText.Create(bombButton, "Count", "X0", Pal.White, 2, BottomMid, BottomMid, new Vector2(0f, 8f));
            skillButton.gameObject.SetActive(false);
            bombButton.gameObject.SetActive(false);

            BuildHangar();
            BuildCoreChoice();
        }

        RectTransform SmallIconButton(string objectName, Vector2 anchor, Vector2 position, Sprite icon, string label, int tab)
        {
            var button = NewButton(readyGroup, objectName, anchor, position, new Vector2(52f, 52f), () => HangarOpenRequested?.Invoke(tab));
            AddPanel(button);
            NewImage(button, "Icon", icon, 3f, Mid, Mid, Vector2.zero);
            PixelText.Create(button, "Label", label, Pal.Gold, 2, Mid, Mid, new Vector2(0f, -40f));
            button.gameObject.SetActive(false);
            return button;
        }

        bool ShooterButtonHit(Vector2 screenPos) =>
            Hit(modePrev, screenPos) || Hit(modeNext, screenPos) || Hit(upgradesButton, screenPos) || Hit(itemsButton, screenPos)
            || Hit(skillButton, screenPos) || Hit(bombButton, screenPos) || HangarButtonHit(screenPos) || CoreChoiceOpen;

        /// <summary>Title lines per mode: 0 flappy, 1 DOG BLAST, 2 CORE RUN.</summary>
        public void SetMode(int mode)
        {
            string[] top = { "TAP", "DOG", "CORE" }, mid = { "< FLAPPY >", "< SHOOTER >", "< ROGUELIKE >" }, bottom = { "DRAG", "BLAST", "RUN" };
            Color32[] bottomColor = { Art.GateMain[0], Pal.Rose, Art.PlasmaColor };
            mode = Mathf.Clamp(mode, 0, 2);
            titleTop.SetColor(Pal.Orange);
            titleTop.Set(top[mode]);
            titleMid.SetColor(Pal.White);
            titleMid.Set(mid[mode]);
            titleBottom.SetColor(bottomColor[mode]);
            titleBottom.Set(bottom[mode]);
            shooterPanelLine.Set(mode == 2 ? "PICK A CORE EVERY WAVE" : "AUTO FIRE - STOP THE CATS");
            shooterPanelLine.SetColor(mode == 2 ? Art.PlasmaColor : Art.PelletColor);
        }

        public void SetWave(int wave)
        {
            waveText.Visible = shooterHud && wave > 0;
            waveText.Set("WAVE " + wave);
        }

        public void SetShooterHud(bool on)
        {
            shooterHud = on;
            weaponText.Visible = on;
            feverBarBack.enabled = feverBarFill.enabled = !on;
            feverMeterLabel.Visible = !on;
            skillButton.gameObject.SetActive(on);
            bombButton.gameObject.SetActive(on);

            // Side layout: keep the top-centre clear for the score and boss bar. Settings buttons and the best
            // score (title-screen info) step aside during a shooter run; bones move up into the freed corner.
            muteButton.gameObject.SetActive(!on);
            hapticButton.gameObject.SetActive(!on);
            best.Visible = !on;
            crown.enabled = !on;
            score.SetScale(on ? 5 : 7);
            score.Rect.anchoredPosition = new Vector2(0f, on ? -12f : -54f);
            comboY = on ? ShooterComboY : ComboY;
            coinY = on ? -12f : -50f;
            coinText.Rect.anchoredPosition = new Vector2(-12f, coinY);
            SetCoins(Economy.Coins, false);

            if (on)
            {
                switchBadge.gameObject.SetActive(false);
                SetHint(false, "", Pal.White);
                SetMeteorWarning(false, 0f, false);
            }
            else
            {
                warningTime = 0f;
                warningText.Visible = warningName.Visible = false;
                SetBuffIcons(0);
                waveText.Visible = false;
                HideCoreChoice();
            }
            for (int i = 0; i < MaxHeartIcons; i++) heartIcons[i].gameObject.SetActive(false);
        }

        public void SetHearts(int current, int max)
        {
            for (int i = 0; i < MaxHeartIcons; i++)
            {
                heartIcons[i].gameObject.SetActive(shooterHud && i < max);
                heartIcons[i].sprite = i < current ? art.IconHeart : art.IconHeartEmpty;
            }
        }

        public void SetWeapon(string weaponName, int level)
        {
            weaponText.Set(weaponName + (level >= 4 ? " MAX" : " LV" + level));
            weaponText.SetColor(weaponName == "HOMING" ? Art.HomingColor : weaponName == "PLASMA" ? Art.PlasmaColor
                : weaponName == "WAVE" ? Art.WaveColor : Pal.OrangeLight);
        }

        /// <summary>Bit mask of ShooterGame.Buff* values; icons stack down the right edge under the weapon and wave lines.</summary>
        public void SetBuffIcons(int mask)
        {
            float y = -84f;
            for (int i = 0; i < buffIcons.Length; i++)
            {
                bool on = (mask & (1 << i)) != 0;
                buffIcons[i].enabled = on;
                if (!on) continue;
                buffIcons[i].rectTransform.anchoredPosition = new Vector2(-12f, y);
                y -= buffIcons[i].rectTransform.sizeDelta.y + 6f;
            }
        }

        public void SetSkillCharge(float charge01, bool ready, string label)
        {
            if (ready && !skillReady) skillPunch = 1f;
            skillReady = ready;
            skillLabel.Set(label);
            skillReadyText.Set(ready ? "READY" : Mathf.FloorToInt(charge01 * 100f) + "%");
            skillReadyText.SetColor(ready ? Pal.Gold : Pal.Lilac);
            skillFill.rectTransform.sizeDelta = new Vector2(80f, 80f * Mathf.Clamp01(charge01));
        }

        public void SetBombs(int count)
        {
            bombCount.Set("X" + count);
            bombIcon.color = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        public void SetBossInfo(string bossName, Color32 color)
        {
            bossColor = color;
            bossLabel.Set(bossName);
            bossLabel.SetColor(color);
        }

        public void BossWarning(string bossName)
        {
            warningTime = 2.2f;
            warningName.Set(bossName + " APPROACHING");
        }

        public void ShowBossBar(bool on, float fill01)
        {
            bossBack.enabled = bossFill.enabled = bossPhaseTick.enabled = on;
            bossLabel.Visible = on;
            float frac = Mathf.Clamp01(fill01);
            if (on && frac < bossFrac) bossPunch = 1f;
            bossFrac = frac;
            bossFill.rectTransform.sizeDelta = new Vector2(BossBarWidth * frac, 8f);
            bossPhaseTick.enabled = on && frac > 0.5f; // marker disappears once phase 2 has started
        }

        void TickShooterUi(float dt)
        {
            if (!shooterHud) return;

            // Boss bar: boss colour in phase 1, flashing red in phase 2, white blink on each hit.
            if (bossFill.enabled)
            {
                bossPunch = Mathf.MoveTowards(bossPunch, 0f, dt * 8f);
                Color baseColor = bossFrac > 0.5f ? (Color)bossColor : (((int)(time * 6f) & 1) == 0 ? (Color)Pal.Red : new Color(0.75f, 0.1f, 0.2f));
                bossFill.color = Color.Lerp(baseColor, Color.white, bossPunch * 0.7f);
                bossBack.rectTransform.anchoredPosition = new Vector2(bossPunch > 0.5f ? Random.Range(-2f, 2f) : 0f, BossBarY);
            }

            if (warningTime > 0f)
            {
                warningTime -= dt;
                bool blink = ((int)(warningTime * 6f) & 1) == 0;
                warningText.Visible = warningTime > 0f && blink;
                warningName.Visible = warningTime > 0f;
                warningText.Rect.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(time * 20f));
            }

            skillPunch = Mathf.MoveTowards(skillPunch, 0f, dt * 3f);
            float pulse = skillReady ? 1f + 0.06f * Mathf.Sin(time * 10f) : 1f;
            skillButton.localScale = Vector3.one * (pulse + 0.3f * skillPunch);
            if (skillReady) skillFill.color = Color.Lerp(new Color(1f, 0.85f, 0.3f, 0.45f), new Color(1f, 1f, 1f, 0.6f), 0.5f + 0.5f * Mathf.Sin(time * 10f));
            else skillFill.color = new Color(Art.PelletColor.r / 255f, Art.PelletColor.g / 255f, Art.PelletColor.b / 255f, 0.45f);
        }
    }
}
