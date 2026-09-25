using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Decision gate with two modes:
    ///  - Trap (red): laser beams with a gap. Fly through the gap WITHOUT dashing; dashing into it is fatal.
    ///  - Dash (cyan): full-height barrier, same rule as a DashGate.
    /// A switch gate flickers as a warning and flips to the other mode when it gets close to the bird.
    /// </summary>
    public class TrapGate : Obstacle
    {
        public enum GateMode { Dash, Trap }

        const float BeamHalfWidth = 5f / World.PPU;

        Art art;
        SpriteRenderer fullBeam, topBeam, bottomBeam, glow, topEmitter, bottomEmitter, gapTopEmitter, gapBottomEmitter;
        float gapCenter, gap, anim, fade, flipDistance;
        GateMode shown;

        public GateMode Mode { get; private set; }
        public bool Broken { get; private set; }
        public bool IsSwitch { get; private set; }
        public bool Flipped { get; private set; }
        public System.Action<TrapGate> Flip;
        public int Variant => Mode == GateMode.Trap ? Art.RedVariant : 0;
        public float GapTop => gapCenter + gap * 0.5f;
        public float GapBottom => gapCenter - gap * 0.5f;
        public float GapCenter => gapCenter;
        public override float HalfWidth => BeamHalfWidth;

        public void Build(Art sourceArt)
        {
            art = sourceArt;
            glow = Part("Glow", art.GlowH, -16);
            fullBeam = Part("FullBeam", art.GateBeam[0][0], -8);
            topBeam = Part("TopBeam", art.GateBeam[Art.RedVariant][0], -8);
            bottomBeam = Part("BottomBeam", art.GateBeam[Art.RedVariant][0], -8);
            fullBeam.drawMode = topBeam.drawMode = bottomBeam.drawMode = SpriteDrawMode.Tiled;
            topEmitter = Part("TopEmitter", art.GateEmitter[0], -7);
            bottomEmitter = Part("BottomEmitter", art.GateEmitter[0], -7);
            bottomEmitter.flipY = true;
            gapTopEmitter = Part("GapTopEmitter", art.GateEmitter[Art.RedVariant], -7);
            gapTopEmitter.flipY = true; // body sits on the gap edge, nozzle points up into the beam
            gapBottomEmitter = Part("GapBottomEmitter", art.GateEmitter[Art.RedVariant], -7);
        }

        public void Setup(float x, float center, float gapSize, GateMode start, bool isSwitch, float flipAt)
        {
            X = x;
            Cleared = false;
            Broken = false;
            Mode = start;
            IsSwitch = isSwitch;
            Flipped = false;
            flipDistance = flipAt;
            gapCenter = center;
            gap = gapSize;
            anim = Random.value;
            fade = 1f;
            Layout();
            ShowMode(Mode);
            Place();
        }

        void Layout()
        {
            float top = World.Top, bottom = World.GroundTop, width = BeamHalfWidth * 2f;
            SetSpan(fullBeam, bottom, top, width);
            SetSpan(topBeam, GapTop, top, width);
            SetSpan(bottomBeam, bottom, GapBottom, width);
            glow.transform.localPosition = new Vector3(0f, (top + bottom) * 0.5f, 0f);
            glow.transform.localScale = new Vector3(0.8f, (top - bottom) / glow.sprite.bounds.size.y, 1f);
            topEmitter.transform.localPosition = new Vector3(0f, top, 0f);
            bottomEmitter.transform.localPosition = new Vector3(0f, bottom, 0f);
            gapTopEmitter.transform.localPosition = new Vector3(0f, GapTop, 0f);
            gapBottomEmitter.transform.localPosition = new Vector3(0f, GapBottom, 0f);
        }

        static void SetSpan(SpriteRenderer r, float from, float to, float width)
        {
            r.size = new Vector2(width, Mathf.Max(0.01f, to - from));
            r.transform.localPosition = new Vector3(0f, (from + to) * 0.5f, 0f);
        }

        void ShowMode(GateMode m)
        {
            shown = m;
            bool trap = m == GateMode.Trap;
            fullBeam.enabled = !trap && !Broken;
            topBeam.enabled = bottomBeam.enabled = trap && !Broken;
            gapTopEmitter.enabled = gapBottomEmitter.enabled = trap;
            topEmitter.sprite = bottomEmitter.sprite = art.GateEmitter[trap ? Art.RedVariant : 0];
            topEmitter.color = bottomEmitter.color = gapTopEmitter.color = gapBottomEmitter.color = Color.white;
        }

        static GateMode Other(GateMode m) => m == GateMode.Trap ? GateMode.Dash : GateMode.Trap;

        public override void Tick(float dt, float speed)
        {
            base.Tick(dt, speed);
            anim += dt;

            if (IsSwitch && !Flipped && !Broken)
            {
                float distance = X - World.BirdX;
                if (distance < flipDistance)
                {
                    Mode = Other(Mode);
                    Flipped = true;
                    ShowMode(Mode);
                    Flip?.Invoke(this);
                }
                else if (distance < flipDistance + 1.3f)
                {
                    // Warning flicker between both looks.
                    var look = ((int)(anim * 14f) & 1) == 0 ? Other(Mode) : Mode;
                    if (look != shown) ShowMode(look);
                }
            }

            int variant = shown == GateMode.Trap ? Art.RedVariant : 0;
            Color main = Art.GateMain[variant];
            if (!Broken)
            {
                var frame = art.GateBeam[variant][(int)(anim * 16f) & 3];
                fullBeam.sprite = topBeam.sprite = bottomBeam.sprite = frame;
                float pulse = 0.35f + 0.15f * Mathf.Sin(anim * 9f) + (Random.value < 0.05f ? 0.25f : 0f);
                glow.color = new Color(main.r, main.g, main.b, pulse);
            }
            else
            {
                fade = Mathf.MoveTowards(fade, 0f, dt * 2.5f);
                glow.color = new Color(main.r, main.g, main.b, 0.9f * fade * fade);
                var dim = Color.Lerp(new Color(0.45f, 0.42f, 0.5f), Color.white, fade);
                topEmitter.color = bottomEmitter.color = gapTopEmitter.color = gapBottomEmitter.color = dim;
            }
        }

        public void Break()
        {
            Broken = true;
            Cleared = true;
            fullBeam.enabled = topBeam.enabled = bottomBeam.enabled = false;
            fade = 1f;
        }

        public override bool Hits(Vector2 c, float r)
        {
            if (Broken) return false;
            float x0 = X - BeamHalfWidth, x1 = X + BeamHalfWidth;
            if (Mode == GateMode.Dash) return CircleRect(c, r, x0, x1, World.GroundTop - 1f, World.Top + 1f);
            return CircleRect(c, r, x0, x1, GapTop, World.Top + 1f) || CircleRect(c, r, x0, x1, World.GroundTop - 1f, GapBottom);
        }

        /// <summary>Any contact with the gate column, gap included (used when the bird dashes into a red gate).</summary>
        public bool TouchesColumn(Vector2 c, float r) =>
            !Broken && CircleRect(c, r, X - BeamHalfWidth, X + BeamHalfWidth, World.GroundTop - 1f, World.Top + 1f);
    }
}
