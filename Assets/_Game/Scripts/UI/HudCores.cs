using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TapOrDrag
{
    /// <summary>CORE RUN: the "choose a core" overlay shown between waves (three cards, tap one or press 1-3).</summary>
    public partial class Hud
    {
        const int CoreCards = 3;
        const float CardSpacing = 118f, CardTop = 96f;

        sealed class CoreCard
        {
            public RectTransform Root;
            public Image Icon;
            public PixelText Name, Description, Level;
        }

        RectTransform coreRoot;
        PixelText coreTitle, coreSubtitle;
        readonly CoreCard[] coreCards = new CoreCard[CoreCards];
        float coreTime;

        public bool CoreChoiceOpen { get; private set; }
        public event System.Action<int> CoreChosen;

        void BuildCoreChoice()
        {
            coreRoot = NewRect("CoreChoice", frame);
            Stretch(coreRoot);
            var dim = NewImage(coreRoot, "Dim", null, 1f, Mid, Mid, Vector2.zero);
            Stretch(dim.rectTransform);
            dim.rectTransform.offsetMin = new Vector2(-1000f, -1000f);
            dim.rectTransform.offsetMax = new Vector2(1000f, 1000f);
            dim.color = new Color(0.05f, 0.03f, 0.1f, 0.78f);
            dim.raycastTarget = true;

            coreTitle = PixelText.Create(coreRoot, "Title", "CHOOSE A CORE", Pal.Gold, 4, Mid, Mid, new Vector2(0f, 230f));
            coreSubtitle = PixelText.Create(coreRoot, "Subtitle", "WAVE 1 CLEAR", Pal.Lilac, 2, Mid, Mid, new Vector2(0f, 192f));

            for (int i = 0; i < CoreCards; i++)
            {
                int index = i;
                var card = new CoreCard();
                card.Root = NewButton(coreRoot, "Card" + i, Mid, new Vector2(0f, CardTop - i * CardSpacing), new Vector2(344f, 104f), () => CoreChosen?.Invoke(index));
                AddPanel(card.Root);
                card.Icon = NewImage(card.Root, "Icon", art.PowerCapsule, 4f, new Vector2(0f, 0.5f), Mid, new Vector2(40f, 0f));
                card.Name = PixelText.Create(card.Root, "Name", "-", Pal.White, 3, TopLeft, TopLeft, new Vector2(80f, -16f));
                card.Description = PixelText.Create(card.Root, "Description", "-", Pal.Lilac, 2, TopLeft, TopLeft, new Vector2(80f, -50f));
                card.Level = PixelText.Create(card.Root, "Level", "NEW", Pal.Gold, 2, TopLeft, TopLeft, new Vector2(80f, -74f));
                PixelText.Create(card.Root, "Key", (i + 1).ToString(), Pal.Lilac, 2, TopRight, TopRight, new Vector2(-12f, -12f));
                coreCards[i] = card;
            }
            coreRoot.gameObject.SetActive(false);
        }

        public void ShowCoreChoice(int wave, IReadOnlyList<CoreKind> cores, int[] stacks)
        {
            CoreChoiceOpen = true;
            coreTime = 0f;
            coreRoot.gameObject.SetActive(true);
            coreRoot.SetAsLastSibling();
            coreSubtitle.Set("WAVE " + wave + " CLEAR");
            for (int i = 0; i < CoreCards; i++)
            {
                var card = coreCards[i];
                bool on = i < cores.Count;
                card.Root.gameObject.SetActive(on);
                if (!on) continue;
                var core = cores[i];
                int have = stacks[(int)core], max = CoreDefs.MaxStacks[(int)core];
                card.Icon.sprite = CoreDefs.Icon(art, core);
                card.Icon.rectTransform.sizeDelta = card.Icon.sprite.rect.size * 4f;
                card.Name.Set(CoreDefs.Names[(int)core]);
                card.Name.SetColor(CoreDefs.Colors[(int)core]);
                card.Description.Set(CoreDefs.Descriptions[(int)core]);
                card.Level.Set(have == 0 ? "NEW" : max >= 99 ? "AGAIN" : "LV " + have + " > " + (have + 1));
                card.Level.SetColor(have == 0 ? Pal.Gold : Pal.OrangeLight);
                card.Root.localScale = Vector3.zero;
            }
        }

        public void HideCoreChoice()
        {
            if (coreRoot == null) return;
            CoreChoiceOpen = false;
            coreRoot.gameObject.SetActive(false);
        }

        void TickCoreChoice(float dt)
        {
            if (!CoreChoiceOpen) return;
            coreTime += dt;
            coreTitle.Rect.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(time * 6f));
            for (int i = 0; i < CoreCards; i++)
            {
                float k = Mathf.Clamp01((coreTime - 0.08f * i) / 0.3f); // cards pop in one after another
                coreCards[i].Root.localScale = Vector3.one * (k <= 0f ? 0.01f : EaseOutBack(k));
            }
        }
    }
}
