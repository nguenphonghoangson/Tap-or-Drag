using UnityEngine;

namespace TapOrDrag
{
    public enum EnemyStyle { Spiky, Bat } // Bat: smaller, faster wave, NIGHT biome flocks

    /// <summary>
    /// Flying spiky ball that moves towards the bird. The player chooses: dodge it with taps (small reward)
    /// or dash through it to stomp it (bigger reward). Touching it without dashing is fatal.
    /// </summary>
    public class SpikyEnemy : Obstacle
    {
        public const float Radius = 0.42f;
        const float BatRadius = 0.3f;

        Art art;
        SpriteRenderer body;
        float baseY, bob, extraSpeed, phase, anim, spin, bobRate = 3f;
        Sprite[] frames;
        Vector2 knockVelocity, knockOffset;

        public float Y { get; private set; }
        public EnemyStyle Style { get; private set; }
        public float CollisionRadius => Style == EnemyStyle.Bat ? BatRadius : Radius;
        public bool Defeated { get; private set; }
        public Vector2 Position => new Vector2(X, Y) + knockOffset;
        public override float HalfWidth => CollisionRadius;

        public void Build(Art sourceArt)
        {
            art = sourceArt;
            body = Part("Body", art.Spiky[0], 7);
        }

        public void Setup(float x, float y, float towardsBirdSpeed, float bobAmplitude, EnemyStyle style = EnemyStyle.Spiky)
        {
            Style = style;
            frames = style == EnemyStyle.Bat ? art.BatFrames : art.Spiky;
            bobRate = style == EnemyStyle.Bat ? 6f : 3f;
            X = x;
            Cleared = false;
            Defeated = false;
            baseY = Y = y;
            bob = bobAmplitude;
            extraSpeed = towardsBirdSpeed;
            phase = Random.value * Mathf.PI * 2f;
            anim = Random.value;
            spin = 0f;
            knockVelocity = knockOffset = Vector2.zero;
            body.flipY = false;
            body.color = Color.white;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;
            Place();
            UpdateBody();
        }

        public override void Tick(float dt, float speed)
        {
            anim += dt;
            if (!Defeated)
            {
                X -= (speed + extraSpeed) * dt;
                phase += dt * bobRate;
                Y = baseY + bob * Mathf.Sin(phase);
                body.sprite = frames[(int)(anim * (Style == EnemyStyle.Bat ? 14f : 10f)) & 1];
                float pulse = Style == EnemyStyle.Bat ? 1f : 1f + 0.06f * Mathf.Sin(anim * 14f);
                body.transform.localScale = new Vector3(pulse, 2f - pulse, 1f);
            }
            else
            {
                // Mario-style defeat: flipped over, popped up, falls off screen.
                X -= speed * dt;
                knockVelocity.y -= 30f * dt;
                knockOffset += knockVelocity * dt;
                spin += dt * 540f;
                body.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
            }
            Place();
            UpdateBody();
        }

        void UpdateBody() => body.transform.localPosition = new Vector3(knockOffset.x, Y + knockOffset.y, 0f);

        public void Defeat()
        {
            Defeated = true;
            Cleared = true;
            body.flipY = true;
            body.transform.localScale = Vector3.one;
            body.color = new Color(1f, 0.85f, 0.85f);
            knockVelocity = new Vector2(Random.Range(1.5f, 3f), 9f);
        }

        public override bool Hits(Vector2 c, float r)
        {
            if (Defeated) return false;
            float rr = r + CollisionRadius;
            return (c - Position).sqrMagnitude < rr * rr;
        }
    }
}
