using UnityEngine;
using UnityEngine.UI;

namespace TapOrDrag
{
    /// <summary>UI for the mode selector on the title screen and the DOG BLAST shooter HUD (hearts, power, boss bar).</summary>
    public partial class Hud
    {
        const int MaxHeartIcons = 5;

        PixelText titleTop, titleMid, titleBottom, powerText, bossLabel;
        RectTransform modePrev, modeNext;
        Image shooterPanel, bossBack, bossFill;
        readonly Image[] heartIcons = new Image[MaxHeartIcons];
        bool shooterHud;

        /// <summary>Raised by the mode arrows on the title screen.</summary>
        public event System.Action<int> ModeStepRequested;

        void BuildShooterUi()
        {
            // Mode switch: two halves of the middle title line.
            modePrev = NewButton(readyGroup, "ModePrev", Top, new Vector2(-78f, -171f), new Vector2(150f, 52f), () => ModeStepRequested?.Invoke(-1));
            modeNext = NewButton(readyGroup, "ModeNext", Top, new Vector2(78f, -171f), new Vector2(150f, 52f), () => ModeStepRequested?.Invoke(1));

            shooterPanel = NewImage(readyGroup, "ShooterTutorial", art.Panel, 1f, BottomMid, BottomMid, new Vector2(0f, 180f));
            shooterPanel.type = Image.Type.Sliced;
            shooterPanel.rectTransform.sizeDelta = new Vector2(300f, 136f);
            PixelText.Create(shooterPanel.transform, "Move", "DRAG TO MOVE", Pal.OrangeLight, 3, Top, Top, new Vector2(0f, -22f));
            PixelText.Create(shooterPanel.transform, "Fire", "AUTO FIRE!", Art.PelletColor, 3, Top, Top, new Vector2(0f, -58f));
            PixelText.Create(shooterPanel.transform, "Goal", "STOP THE CAT INVASION", Pal.Lilac, 2, Top, Top, new Vector2(0f, -100f));
            shooterPanel.gameObject.SetActive(false);

            for (int i = 0; i < MaxHeartIcons; i++)
            {
                heartIcons[i] = NewImage(playGroup, "Heart" + i, art.IconHeart, 3f, TopLeft, TopLeft, new Vector2(12f + i * 28f, -60f));
                heartIcons[i].gameObject.SetActive(false);
            }
            powerText = PixelText.Create(playGroup, "Power", "POWER 1", Pal.OrangeLight, 2, TopLeft, TopLeft, new Vector2(12f, -94f));
            powerText.Visible = false;

            bossLabel = PixelText.Create(playGroup, "BossLabel", "CAT MOTHERSHIP", Pal.Rose, 2, Top, Top, new Vector2(0f, -176f));
            bossBack = NewImage(playGroup, "BossBarBack", null, 1f, Top, Top, new Vector2(0f, -198f));
            bossBack.rectTransform.sizeDelta = new Vector2(260f, 14f);
            bossBack.color = new Color(0.08f, 0.04f, 0.14f, 0.85f);
            bossFill = NewImage(bossBack.transform, "Fill", null, 1f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(3f, 0f));
            bossFill.rectTransform.sizeDelta = new Vector2(254f, 8f);
            bossFill.color = Pal.Rose;
            ShowBossBar(false, 0f);
        }

        bool ShooterButtonHit(Vector2 screenPos) => Hit(modePrev, screenPos) || Hit(modeNext, screenPos);

        public void SetMode(bool shooter)
        {
            titleTop.SetColor(Pal.Orange);
            titleTop.Set(shooter ? "DOG" : "TAP");
            titleMid.SetColor(Pal.White);
            titleMid.Set(shooter ? "< SHOOTER >" : "< FLAPPY >");
            titleBottom.SetColor(shooter ? Pal.Rose : Art.GateMain[0]);
            titleBottom.Set(shooter ? "BLAST" : "DRAG");
        }

        public void SetShooterHud(bool on)
        {
            shooterHud = on;
            powerText.Visible = on;
            feverBarBack.enabled = feverBarFill.enabled = !on;
            feverMeterLabel.Visible = !on;
            if (on)
            {
                switchBadge.gameObject.SetActive(false);
                SetHint(false, "", Pal.White);
                SetMeteorWarning(false, 0f, false);
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

        public void SetPower(int level) => powerText.Set(level >= 4 ? "POWER MAX" : "POWER " + level);

        public void ShowBossBar(bool on, float fill01)
        {
            bossBack.enabled = bossFill.enabled = on;
            bossLabel.Visible = on;
            bossFill.rectTransform.sizeDelta = new Vector2(254f * Mathf.Clamp01(fill01), 8f);
        }
    }
}
