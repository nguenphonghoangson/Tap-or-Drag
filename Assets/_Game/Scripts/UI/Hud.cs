using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TapOrDrag
{
    /// <summary>Builds and animates all UI: score, combo, best, mute, title screen, game-over panel, screen flash.</summary>
    public partial class Hud : MonoBehaviour
    {
        static readonly Vector2 Top = new Vector2(0.5f, 1f);
        static readonly Vector2 BottomMid = new Vector2(0.5f, 0f);
        static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        static readonly Vector2 TopRight = new Vector2(1f, 1f);
        static readonly Vector2 Mid = new Vector2(0.5f, 0.5f);

        const float ComboY = -136f;

        Art art;
        AudioManager audioManager;
        RectTransform frame, playGroup, readyGroup, gameOverGroup, titleGroup, goPanel, muteButton, hapticButton;
        RectTransform skinSelector, skinLeft, skinRight;
        PixelText score, combo, toast, best, swipeHint, tapToStart, tapToRetry, goTitle, goScore, goBest, goNewBest;
        Image tutorialPanel;
        string hintText;
        PixelText skinName, skinSkill, skinInfo;
        PixelText[] skinLabels;
        Camera cam;
        Vector3 selectorWorld;
        Image crown, muteIcon, hapticIcon, flash, feverOverlay, feverBarBack, feverBarFill;
        PixelText feverText, feverMeterLabel;
        bool feverOn, meterRecharging;
        float feverProgress, meterFill;
        Color flashColor = Color.white;
        float flashAlpha, time, scorePunch, comboPunch, bestPulse, comboBreak, toastTime, goTime;
        bool hintOn, newBestShown;
        string lastScore;

        /// <summary>Raised by the skin arrows on the title screen (-1 / +1).</summary>
        public event System.Action<int> SkinStepRequested;

        public void Build(Art sourceArt, AudioManager audio, Camera worldCamera)
        {
            art = sourceArt;
            audioManager = audio;
            cam = worldCamera;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(405f, 820f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            frame = NewRect("Frame", transform);
            Stretch(frame);

            // Top bar (always visible)
            best = PixelText.Create(frame, "Best", "BEST 0", Pal.Gold, 3, TopRight, TopRight, new Vector2(-12f, -14f));
            crown = NewImage(frame, "Crown", art.IconCrown, 3f, TopRight, TopRight, Vector2.zero);
            muteButton = NewButton(frame, "Mute", TopLeft, new Vector2(4f, -4f), new Vector2(64f, 52f), OnMuteClicked);
            muteButton.pivot = TopLeft;
            muteIcon = NewImage(muteButton, "Icon", art.IconSoundOn, 3f, Mid, Mid, Vector2.zero);
            hapticButton = NewButton(frame, "Haptics", TopLeft, new Vector2(68f, -4f), new Vector2(64f, 52f), OnHapticsClicked);
            hapticButton.pivot = TopLeft;
            hapticIcon = NewImage(hapticButton, "Icon", art.IconHapticOn, 3f, Mid, Mid, Vector2.zero);

            // In-run HUD
            playGroup = NewRect("Play", frame);
            Stretch(playGroup);
            score = PixelText.Create(playGroup, "Score", "0", Pal.White, 7, Top, Top, new Vector2(0f, -54f));
            combo = PixelText.Create(playGroup, "Combo", "x2", Pal.Orange, 4, Top, Top, new Vector2(0f, ComboY));
            toast = PixelText.Create(playGroup, "Toast", "NEW BEST!", Pal.Gold, 3, Top, Top, new Vector2(0f, -262f));
            feverText = PixelText.Create(playGroup, "Fever", "FEVER!", Pal.White, 5, Top, Top, new Vector2(0f, -178f));
            feverBarBack = NewImage(playGroup, "FeverBarBack", null, 1f, Top, Top, new Vector2(0f, -236f));
            feverBarBack.rectTransform.sizeDelta = new Vector2(186f, 12f);
            feverBarBack.color = new Color(0.08f, 0.04f, 0.14f, 0.75f);
            feverBarFill = NewImage(feverBarBack.transform, "Fill", null, 1f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(3f, 0f));
            feverBarFill.rectTransform.sizeDelta = new Vector2(180f, 6f);
            feverMeterLabel = PixelText.Create(playGroup, "FeverMeter", "FEVER AT x5", Pal.Lilac, 2, Top, Top, new Vector2(0f, -214f));
            swipeHint = PixelText.Create(playGroup, "SwipeHint", "SWIPE >>", Art.GateMain[0], 4, BottomMid, BottomMid, new Vector2(0f, 150f));
            combo.Visible = toast.Visible = swipeHint.Visible = false;

            // Title screen
            readyGroup = NewRect("Ready", frame);
            Stretch(readyGroup);
            titleGroup = NewRect("Title", readyGroup);
            Stretch(titleGroup);
            titleTop = PixelText.Create(titleGroup, "Tap", "TAP", Pal.Orange, 8, Top, Top, new Vector2(0f, -70f));
            titleMid = PixelText.Create(titleGroup, "Or", "OR", Pal.White, 3, Top, Top, new Vector2(0f, -156f));
            titleBottom = PixelText.Create(titleGroup, "Drag", "DRAG", Art.GateMain[0], 8, Top, Top, new Vector2(0f, -190f));

            // Skin picker: follows the idle bird (positioned in LateUpdate).
            skinSelector = NewRect("SkinSelector", readyGroup);
            skinSelector.anchorMin = skinSelector.anchorMax = Mid;
            skinSelector.sizeDelta = Vector2.zero;
            skinLeft = NewButton(skinSelector, "Prev", Mid, new Vector2(-74f, 0f), new Vector2(56f, 64f), () => SkinStepRequested?.Invoke(-1));
            NewImage(skinLeft, "Icon", art.IconArrowLeft, 4f, Mid, Mid, Vector2.zero);
            skinRight = NewButton(skinSelector, "Next", Mid, new Vector2(74f, 0f), new Vector2(56f, 64f), () => SkinStepRequested?.Invoke(1));
            NewImage(skinRight, "Icon", art.IconArrowRight, 4f, Mid, Mid, Vector2.zero);
            skinName = PixelText.Create(skinSelector, "SkinName", "CHICK", Pal.White, 3, Mid, Top, new Vector2(0f, -38f));
            skinSkill = PixelText.Create(skinSelector, "SkinSkill", "NO SKILL", Pal.Lilac, 2, Mid, Top, new Vector2(0f, -66f));
            skinInfo = PixelText.Create(skinSelector, "SkinInfo", "1/6", Pal.Lilac, 2, Mid, Top, new Vector2(0f, -88f));
            skinLabels = new[] { skinName, skinSkill, skinInfo };

            var tutorial = NewImage(readyGroup, "Tutorial", art.Panel, 1f, BottomMid, BottomMid, new Vector2(0f, 180f));
            tutorialPanel = tutorial;
            tutorial.type = Image.Type.Sliced;
            tutorial.rectTransform.sizeDelta = new Vector2(300f, 136f);
            NewImage(tutorial.transform, "PipeIcon", art.IconPipe, 3f, TopLeft, TopLeft, new Vector2(26f, -16f));
            PixelText.Create(tutorial.transform, "TapLine", "TAP = FLAP", Pal.OrangeLight, 3, TopLeft, TopLeft, new Vector2(84f, -24f));
            NewImage(tutorial.transform, "GateIcon", art.IconGate, 3f, TopLeft, TopLeft, new Vector2(26f, -72f));
            PixelText.Create(tutorial.transform, "SwipeLine", "SWIPE = DASH", Art.GateMain[0], 3, TopLeft, TopLeft, new Vector2(84f, -84f));
            tapToStart = PixelText.Create(readyGroup, "TapToStart", "TAP TO START", Pal.White, 3, BottomMid, BottomMid, new Vector2(0f, 150f));

            // Game over
            gameOverGroup = NewRect("GameOver", frame);
            Stretch(gameOverGroup);
            goTitle = PixelText.Create(gameOverGroup, "Title", "GAME OVER", Pal.Rose, 6, Top, Top, new Vector2(0f, -150f));
            var panel = NewImage(gameOverGroup, "Panel", art.Panel, 1f, Top, Mid, new Vector2(0f, -360f));
            panel.type = Image.Type.Sliced;
            goPanel = panel.rectTransform;
            goPanel.sizeDelta = new Vector2(270f, 220f);
            PixelText.Create(goPanel, "ScoreLabel", "SCORE", Pal.Lilac, 2, Top, Top, new Vector2(0f, -20f));
            goScore = PixelText.Create(goPanel, "Score", "0", Pal.White, 6, Top, Top, new Vector2(0f, -46f));
            PixelText.Create(goPanel, "BestLabel", "BEST", Pal.Lilac, 2, Top, Top, new Vector2(0f, -128f));
            goBest = PixelText.Create(goPanel, "Best", "0", Pal.Gold, 4, Top, Top, new Vector2(0f, -152f));
            goNewBest = PixelText.Create(gameOverGroup, "NewBest", "NEW BEST!", Pal.Gold, 4, Top, Top, new Vector2(0f, -492f));
            tapToRetry = PixelText.Create(gameOverGroup, "TapToRetry", "TAP TO RETRY", Pal.White, 3, BottomMid, BottomMid, new Vector2(0f, 150f));

            feverOverlay = NewImage(transform, "FeverOverlay", null, 1f, Mid, Mid, Vector2.zero);
            Stretch(feverOverlay.rectTransform);
            feverOverlay.enabled = false;
            feverOverlay.transform.SetSiblingIndex(0); // under the HUD, over the world

            flash = NewImage(transform, "Flash", null, 1f, Mid, Mid, Vector2.zero);
            Stretch(flash.rectTransform);
            flash.color = new Color(1f, 1f, 1f, 0f);

            BuildMeta();
            BuildShooterUi();
            RefreshMute();
            RefreshHaptics();
            FitFrame();
        }

        public bool IsOverButton(Vector2 screenPos) =>
            Hit(muteButton, screenPos) || Hit(hapticButton, screenPos) || Hit(skinLeft, screenPos) || Hit(skinRight, screenPos) || MetaButtonHit(screenPos) || ShooterButtonHit(screenPos);

        static bool Hit(RectTransform rt, Vector2 screenPos) =>
            rt != null && rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);

        void OnMuteClicked()
        {
            audioManager.ToggleMute();
            if (!audioManager.Muted) audioManager.Click();
            RefreshMute();
        }

        void RefreshMute() => muteIcon.sprite = audioManager.Muted ? art.IconSoundOff : art.IconSoundOn;

        void OnHapticsClicked()
        {
            Haptics.Enabled = !Haptics.Enabled;
            if (Haptics.Enabled) Haptics.Play(HapticType.Medium); // confirm with a buzz
            audioManager.Click();
            RefreshHaptics();
        }

        void RefreshHaptics() => hapticIcon.sprite = Haptics.Enabled ? art.IconHapticOn : art.IconHapticOff;

        // ---------------------------------------------------------------- state

        public void ShowReady()
        {
            SetFever(false);
            readyGroup.gameObject.SetActive(true);
            playGroup.gameObject.SetActive(false);
            gameOverGroup.gameObject.SetActive(false);
            hintOn = false;
        }

        public void ShowPlaying()
        {
            readyGroup.gameObject.SetActive(false);
            playGroup.gameObject.SetActive(true);
            gameOverGroup.gameObject.SetActive(false);
        }

        public void ShowGameOver(int finalScore, int bestScore, bool isNewBest, int runCoins)
        {
            SetFever(false);
            ShowRunCoins(runCoins);
            playGroup.gameObject.SetActive(false);
            gameOverGroup.gameObject.SetActive(true);
            goScore.Set(finalScore.ToString());
            goBest.Set(bestScore.ToString());
            newBestShown = isNewBest;
            goTime = 0f;
            hintOn = false;
        }

        public void SetScore(int value, int multiplier, bool comboUp)
        {
            string s = value.ToString();
            if (s != lastScore)
            {
                score.Set(s);
                if (value > 0) scorePunch = 1f;
                lastScore = s;
            }
            if (multiplier > 1)
            {
                comboBreak = 0f;
                combo.Visible = true;
                combo.SetColor(ComboColor(multiplier));
                combo.Set("x" + multiplier);
                if (comboUp) comboPunch = 1f;
            }
            else if (comboBreak <= 0f) combo.Visible = false;
        }

        public void ComboBreak()
        {
            if (!combo.Visible) return;
            combo.SetColor(Pal.Red);
            combo.Set("x1");
            comboBreak = 0.55f;
        }

        public void SetBest(int value, bool pulse)
        {
            best.Set("BEST " + value);
            crown.rectTransform.anchoredPosition = new Vector2(-12f - best.Width - 4f, -14f - (best.Height - crown.rectTransform.sizeDelta.y) * 0.5f);
            if (pulse) bestPulse = 1f;
        }

        public void Toast(string text, Color32 color)
        {
            toast.SetColor(color);
            toast.Set(text);
            toastTime = 1.6f;
        }

        public void SetHint(bool on, string text, Color32 color)
        {
            if (on && (!hintOn || text != hintText))
            {
                swipeHint.SetColor(color);
                swipeHint.Set(text);
                hintText = text;
            }
            hintOn = on;
        }

        public void SetFever(bool on)
        {
            feverOn = on;
            feverText.Visible = feverOverlay.enabled = on;
            feverMeterLabel.Visible = !on;
            if (on) comboPunch = 1f;
        }

        public void SetFeverProgress(float remaining01) => feverProgress = Mathf.Clamp01(remaining01);

        /// <summary>Outside Fever: recharge (dim) or combo progress towards the trigger (warm), with a short label.</summary>
        public void SetFeverMeter(float fill01, bool recharging, string label)
        {
            meterFill = Mathf.Clamp01(fill01);
            meterRecharging = recharging;
            feverMeterLabel.SetColor(recharging ? Pal.Lilac : Pal.OrangeLight);
            feverMeterLabel.Set(label);
        }

        public void Flash(Color color, float alpha = 0.85f)
        {
            flashColor = color;
            flashAlpha = alpha;
        }

        static Color32 ComboColor(int m) =>
            m <= 2 ? Pal.Orange : m == 3 ? Pal.Gold : m == 4 ? Pal.Rose : m <= 6 ? Art.GateMain[1] : Art.GateMain[0];

        // ---------------------------------------------------------------- animation

        public void Tick(float dt)
        {
            time += dt;

            scorePunch = Mathf.MoveTowards(scorePunch, 0f, dt * 5f);
            score.Rect.localScale = Vector3.one * (1f + 0.25f * scorePunch);

            comboPunch = Mathf.MoveTowards(comboPunch, 0f, dt * 3f);
            combo.Rect.localScale = Vector3.one * (1f + 0.7f * comboPunch * comboPunch);
            if (comboBreak > 0f)
            {
                comboBreak -= dt;
                combo.Rect.anchoredPosition = new Vector2(Random.Range(-6f, 6f), ComboY);
                if (comboBreak <= 0f) combo.Visible = false;
            }
            else combo.Rect.anchoredPosition = new Vector2(0f, ComboY);

            if (toastTime > 0f)
            {
                toastTime -= dt;
                toast.Visible = toastTime > 0f && (toastTime > 0.6f || ((int)(toastTime * 12f) & 1) == 0);
                toast.Rect.localScale = Vector3.one * (1f + Mathf.Max(0f, toastTime - 1.4f) * 3f);
            }
            else toast.Visible = false;

            swipeHint.Visible = hintOn;
            swipeHint.Rect.anchoredPosition = new Vector2(Mathf.Sin(time * 14f) * 8f, 150f);

            bestPulse = Mathf.MoveTowards(bestPulse, 0f, dt * 2.5f);
            best.Rect.localScale = Vector3.one * (1f + 0.3f * bestPulse);

            titleGroup.anchoredPosition = new Vector2(0f, Mathf.Sin(time * 2.4f) * 6f);
            tapToStart.Visible = ((int)(time * 2.2f) & 1) == 0;

            goTime += dt;
            float k = Mathf.Clamp01(goTime / 0.35f);
            goPanel.localScale = Vector3.one * Mathf.Max(0.01f, EaseOutBack(k));
            goTitle.Rect.anchoredPosition = new Vector2(0f, -150f + (1f - k) * (1f - k) * 80f);
            goNewBest.Visible = newBestShown && goTime > 0.3f && ((int)(time * 5f) & 1) == 0;
            tapToRetry.Visible = goTime > 0.45f && ((int)(time * 2.2f) & 1) == 0;

            if (feverOn)
            {
                var rainbow = Color.HSVToRGB(time * 1.4f % 1f, 0.65f, 1f);
                feverText.Tint = rainbow;
                feverText.Rect.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(time * 16f));
                feverBarFill.color = rainbow;
                feverBarFill.rectTransform.sizeDelta = new Vector2(180f * feverProgress, 6f);
                var overlay = Color.HSVToRGB((time * 0.6f + 0.5f) % 1f, 0.8f, 1f);
                overlay.a = 0.12f + 0.04f * Mathf.Sin(time * 8f);
                feverOverlay.color = overlay;
            }
            else
            {
                // Charge meter: dim while recharging, orange -> gold as the combo approaches the trigger, pulsing near full.
                Color c = meterRecharging ? new Color(0.62f, 0.55f, 0.8f, 0.7f) : Color.Lerp(Pal.Orange, Pal.Gold, meterFill);
                if (!meterRecharging && meterFill >= 0.75f) c = Color.Lerp(c, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(time * 14f)));
                feverBarFill.color = c;
                feverBarFill.rectTransform.sizeDelta = new Vector2(180f * meterFill, 6f);
            }

            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 3f);
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
            flash.enabled = flashAlpha > 0f;

            TickMeta(dt);
            TickShooterUi(dt);
            TickHangar(dt);
            TickCoreChoice(dt);
        }

        void LateUpdate()
        {
            FitFrame();
            if (readyGroup.gameObject.activeInHierarchy && cam != null)
            {
                Vector2 screen = cam.WorldToScreenPoint(selectorWorld);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(readyGroup, screen, null, out var local))
                    skinSelector.anchoredPosition = local;
                // Labels are centred under the bird but nudged inwards so long skill lines stay on screen.
                float frameHalf = readyGroup.rect.width * 0.5f - 8f;
                foreach (var label in skinLabels)
                {
                    float half = label.Width * 0.5f, sx = skinSelector.anchoredPosition.x;
                    float x = Mathf.Clamp(0f, -frameHalf + half - sx, frameHalf - half - sx);
                    label.Rect.anchoredPosition = new Vector2(x, label.Rect.anchoredPosition.y);
                }
            }
        }

        /// <summary>World point (the idle bird) the skin arrows and name are laid out around.</summary>
        public void SetSelectorAnchor(Vector3 world) => selectorWorld = world;

        public void SetSkin(string skinLabel, bool owned, int price, bool affordable, int index, int count, string skillText, Color32 skillColor)
        {
            skinSkill.SetColor(skillColor);
            skinSkill.Set(skillText);
            skinName.SetColor(owned ? Pal.White : Pal.Lilac);
            skinName.Set(skinLabel);
            skinInfo.Visible = owned;
            skinInfo.Set((index + 1) + "/" + count);
            SetBuyButton(!owned, price, affordable);
        }

        /// <summary>
        /// Keep the UI frame on the 405x820 play field even when the screen is wider (e.g. landscape Game view),
        /// and inside the device safe area (notch / Dynamic Island / home indicator).
        /// </summary>
        void FitFrame()
        {
            float half = World.ViewWidth * 0.5f / (World.CamHalfWidth * 2f);
            Rect safe = Screen.safeArea;
            float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            frame.anchorMin = new Vector2(Mathf.Max(0.5f - half, safe.xMin / w), Mathf.Clamp01(safe.yMin / h));
            frame.anchorMax = new Vector2(Mathf.Min(0.5f + half, safe.xMax / w), Mathf.Clamp01(safe.yMax / h));
            frame.offsetMin = frame.offsetMax = Vector2.zero;
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        // ---------------------------------------------------------------- helpers

        static RectTransform NewRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform NewButton(Transform parent, string objectName, Vector2 anchor, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewRect(objectName, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var hitArea = rt.gameObject.AddComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image NewImage(Transform parent, string objectName, Sprite sprite, float scale, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            var rt = NewRect(objectName, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            if (sprite != null) rt.sizeDelta = sprite.rect.size * scale;
            return img;
        }
    }
}
