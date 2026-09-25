using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Top + bottom orange pipe with a gap. Tap (flap) to fly through the gap.</summary>
    public class PipePair : Obstacle
    {
        // Dog version: the pillar is a femur (narrow shaft, wide knob end), sized from the bone art.
        const float BodyWidth = Art.BoneShaftWidth / World.PPU;
        const float CapWidth = Art.BoneEndWidth / World.PPU;
        const float CapHeight = Art.BoneKnobHeight / World.PPU;

        SpriteRenderer topBody, topCap, bottomBody, bottomCap;
        float center, gap, amplitude, phase;

        public float GapCenter => center + amplitude * Mathf.Sin(phase);
        public float GapTop => GapCenter + gap * 0.5f;
        public float GapBottom => GapCenter - gap * 0.5f;
        public bool Smashed { get; private set; }
        /// <summary>Smallest vertical clearance to the gap edges while the bird was inside the pipe column.</summary>
        public float Closest { get; private set; }
        public override float HalfWidth => CapWidth * 0.5f;

        public void Build(Art art)
        {
            topBody = Part("TopBody", art.PipeBody, -10);
            bottomBody = Part("BottomBody", art.PipeBody, -10);
            topBody.drawMode = bottomBody.drawMode = SpriteDrawMode.Tiled;
            topCap = Part("TopCap", art.PipeCapTop, -9); // own sprite so the light still comes from above
            bottomCap = Part("BottomCap", art.PipeCap, -9);
        }

        public void Setup(float x, float gapCenter, float gapSize, float moveAmplitude)
        {
            X = x;
            Cleared = false;
            center = gapCenter;
            gap = gapSize;
            amplitude = moveAmplitude;
            phase = Random.value * Mathf.PI * 2f;
            Smashed = false;
            Closest = float.MaxValue;
            SetVisible(true);
            Place();
            Layout();
        }

        public override void Tick(float dt, float speed)
        {
            base.Tick(dt, speed);
            if (amplitude > 0f)
            {
                phase += dt * 1.8f;
                Layout();
            }
        }

        void Layout()
        {
            float c = GapCenter, top = c + gap * 0.5f, bottom = c - gap * 0.5f;
            float floor = World.GroundTop - 0.5f, ceiling = World.Top + 0.5f;

            bottomBody.size = new Vector2(BodyWidth, Mathf.Max(0.01f, bottom - floor));
            bottomBody.transform.localPosition = new Vector3(0f, (bottom + floor) * 0.5f, 0f);
            bottomCap.transform.localPosition = new Vector3(0f, bottom, 0f);

            topBody.size = new Vector2(BodyWidth, Mathf.Max(0.01f, ceiling - top));
            topBody.transform.localPosition = new Vector3(0f, (top + ceiling) * 0.5f, 0f);
            topCap.transform.localPosition = new Vector3(0f, top, 0f);
        }

        public void TrackClearance(Vector2 c, float r)
        {
            if (Mathf.Abs(c.x - X) > BodyWidth * 0.5f) return;
            Closest = Mathf.Min(Closest, Mathf.Min(c.y - r - GapBottom, GapTop - (c.y + r)));
        }

        /// <summary>Fever smash: the pipe disappears (FX spawns the debris) and no longer collides.</summary>
        public void Smash()
        {
            Smashed = true;
            Cleared = true;
            SetVisible(false);
        }

        void SetVisible(bool on) => topBody.enabled = topCap.enabled = bottomBody.enabled = bottomCap.enabled = on;

        public override bool Hits(Vector2 c, float r)
        {
            if (Smashed) return false;
            float gc = GapCenter, top = gc + gap * 0.5f, bottom = gc - gap * 0.5f;
            float bx0 = X - BodyWidth * 0.5f, bx1 = X + BodyWidth * 0.5f;
            float cx0 = X - CapWidth * 0.5f, cx1 = X + CapWidth * 0.5f;
            return CircleRect(c, r, bx0, bx1, -100f, bottom)
                   || CircleRect(c, r, cx0, cx1, bottom - CapHeight, bottom)
                   || CircleRect(c, r, bx0, bx1, top, 100f)
                   || CircleRect(c, r, cx0, cx1, top, top + CapHeight);
        }
    }
}
