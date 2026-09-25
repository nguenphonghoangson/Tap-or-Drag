using UnityEngine;

namespace TapOrDrag
{
    /// <summary>NEON CITY hazard: a horizontal laser bar between two drones, sweeping up and down. Time the pass.</summary>
    public class LaserSweeper : Obstacle, IBreakable
    {
        const float HalfSpan = 0.8f;      // half the bar length
        const float BeamHalfThickness = 0.12f;
        const float DroneRadius = 0.28f;

        Art art;
        SpriteRenderer beam, left, right, glow;
        float center, amplitude, phase, speed, anim;
        bool broken;

        public float Y { get; private set; }
        public bool Broken => broken;
        public override float HalfWidth => HalfSpan + DroneRadius;

        public void Build(Art sourceArt)
        {
            art = sourceArt;
            glow = Part("Glow", art.GlowH, -12);
            beam = Part("Beam", art.LaserBeam[0], -8);
            beam.drawMode = SpriteDrawMode.Tiled;
            left = Part("DroneL", art.LaserEmitter, -7);
            right = Part("DroneR", art.LaserEmitter, -7);
        }

        public void Setup(float x, float sweepCenter, float sweepAmplitude, float period)
        {
            X = x;
            Cleared = false;
            broken = false;
            center = sweepCenter;
            amplitude = sweepAmplitude;
            speed = Mathf.PI * 2f / Mathf.Max(0.3f, period);
            phase = Random.value * Mathf.PI * 2f;
            beam.enabled = glow.enabled = true;
            left.color = right.color = Color.white;
            beam.size = new Vector2(HalfSpan * 2f, 5f / World.PPU);
            glow.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            glow.transform.localScale = new Vector3(0.35f, HalfSpan * 2f / glow.sprite.bounds.size.y, 1f);
            Place();
            Layout();
        }

        public override void Tick(float dt, float scroll)
        {
            base.Tick(dt, scroll);
            anim += dt;
            if (!broken) phase += speed * dt;
            beam.sprite = art.LaserBeam[(int)(anim * 20f) & 1];
            Color c = Art.LaserColor;
            glow.color = new Color(c.r, c.g, c.b, 0.35f + 0.1f * Mathf.Sin(anim * 25f));
            Layout();
        }

        void Layout()
        {
            Y = center + amplitude * Mathf.Sin(phase);
            beam.transform.localPosition = glow.transform.localPosition = new Vector3(0f, Y, 0f);
            left.transform.localPosition = new Vector3(-HalfSpan, Y, 0f);
            right.transform.localPosition = new Vector3(HalfSpan, Y, 0f);
        }

        public void Break()
        {
            broken = true;
            Cleared = true;
            beam.enabled = glow.enabled = false;
            left.color = right.color = new Color(0.5f, 0.45f, 0.55f);
        }

        public override bool Hits(Vector2 c, float r)
        {
            if (broken) return false;
            if (CircleRect(c, r, X - HalfSpan, X + HalfSpan, Y - BeamHalfThickness, Y + BeamHalfThickness)) return true;
            float rr = r + DroneRadius;
            return (c - new Vector2(X - HalfSpan, Y)).sqrMagnitude < rr * rr || (c - new Vector2(X + HalfSpan, Y)).sqrMagnitude < rr * rr;
        }
    }
}
