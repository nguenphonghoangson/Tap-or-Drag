using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// SNOW PEAKS hazard. Hangs from the ceiling, shakes when the bird gets close, then drops and sticks in the
    /// ground as a spike before the bird arrives, so the safe height flips from "low" to "high".
    /// </summary>
    public class Icicle : Obstacle, IBreakable
    {
        enum Phase { Hanging, Shaking, Falling, Landed, Broken }

        const float HalfW = 3.5f / World.PPU; // a bit narrower than the art: forgiving on the thin tip

        SpriteRenderer sr;
        Phase phase;
        float length, y, vy, shakeTime, dropDistance;

        public System.Action<Icicle> Shattered; // FX/sound hook when it hits the ground
        public bool Broken => phase == Phase.Broken;
        public override float HalfWidth => HalfW;

        public void Build(Art art)
        {
            sr = Part("Ice", art.Icicles[0], -9);
        }

        public void Setup(float x, Sprite sprite, float dropAtDistance)
        {
            X = x;
            Cleared = false;
            phase = Phase.Hanging;
            sr.sprite = sprite;
            sr.enabled = true;
            sr.flipY = false;
            length = sprite.rect.height / World.PPU;
            y = World.Top; // base (top of the sprite) at the ceiling, tip pointing down
            vy = 0f;
            dropDistance = dropAtDistance;
            Place();
            Apply(0f);
        }

        public override void Tick(float dt, float speed)
        {
            base.Tick(dt, speed);
            float jitter = 0f;
            switch (phase)
            {
                case Phase.Hanging:
                    if (X - World.BirdX < dropDistance + 1.2f) { phase = Phase.Shaking; shakeTime = 0f; }
                    break;
                case Phase.Shaking:
                    shakeTime += dt;
                    jitter = Mathf.Sin(shakeTime * 70f) * 0.05f;
                    if (X - World.BirdX < dropDistance) phase = Phase.Falling;
                    break;
                case Phase.Falling:
                    vy -= 70f * dt;
                    y += vy * dt;
                    float landedTop = World.GroundTop + length - 0.35f; // tip buried in the snow
                    if (y <= landedTop)
                    {
                        y = landedTop;
                        phase = Phase.Landed;
                        Shattered?.Invoke(this);
                    }
                    break;
            }
            Apply(jitter);
        }

        void Apply(float jitter) => sr.transform.localPosition = new Vector3(jitter, y, 0f);

        public void Break()
        {
            phase = Phase.Broken;
            Cleared = true;
            sr.enabled = false;
        }

        public Vector2 TipPosition => new Vector2(X, y - length);

        public override bool Hits(Vector2 c, float r) =>
            phase != Phase.Broken && CircleRect(c, r, X - HalfW, X + HalfW, y - length + 0.1f, y);
    }
}
