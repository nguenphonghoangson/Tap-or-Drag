using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Full-height neon barrier. Only passable while dashing (swipe); touching it otherwise is fatal.</summary>
    public class DashGate : Obstacle
    {
        const float BeamHalfWidth = 5f / World.PPU;

        Art art;
        SpriteRenderer beam, glow, topEmitter, bottomEmitter;
        Sprite[] frames;
        float anim, fade;

        public int Variant { get; private set; }
        public bool Broken { get; private set; }
        public override float HalfWidth => BeamHalfWidth;

        public void Build(Art sourceArt)
        {
            art = sourceArt;
            glow = Part("Glow", art.GlowH, -16);
            beam = Part("Beam", art.GateBeam[0][0], -8);
            beam.drawMode = SpriteDrawMode.Tiled;
            topEmitter = Part("TopEmitter", art.GateEmitter[0], -7);
            bottomEmitter = Part("BottomEmitter", art.GateEmitter[0], -7);
            bottomEmitter.flipY = true;
        }

        public void Setup(float x, int variant)
        {
            X = x;
            Cleared = false;
            Broken = false;
            Variant = variant;
            frames = art.GateBeam[variant];
            anim = Random.value;
            fade = 1f;

            float height = World.Top - World.GroundTop;
            float mid = (World.Top + World.GroundTop) * 0.5f;
            beam.enabled = true;
            beam.sprite = frames[0];
            beam.size = new Vector2(BeamHalfWidth * 2f, height);
            beam.transform.localPosition = new Vector3(0f, mid, 0f);
            glow.transform.localPosition = new Vector3(0f, mid, 0f);
            glow.transform.localScale = new Vector3(0.8f, height / glow.sprite.bounds.size.y, 1f);

            topEmitter.sprite = bottomEmitter.sprite = art.GateEmitter[variant];
            topEmitter.color = bottomEmitter.color = Color.white;
            topEmitter.transform.localPosition = new Vector3(0f, World.Top, 0f);
            bottomEmitter.transform.localPosition = new Vector3(0f, World.GroundTop, 0f);
            Place();
        }

        public override void Tick(float dt, float speed)
        {
            base.Tick(dt, speed);
            anim += dt;
            Color main = Art.GateMain[Variant];
            if (!Broken)
            {
                beam.sprite = frames[(int)(anim * 16f) & 3];
                float pulse = 0.35f + 0.15f * Mathf.Sin(anim * 9f) + (Random.value < 0.05f ? 0.25f : 0f);
                glow.color = new Color(main.r, main.g, main.b, pulse);
            }
            else
            {
                fade = Mathf.MoveTowards(fade, 0f, dt * 2.5f);
                glow.color = new Color(main.r, main.g, main.b, 0.9f * fade * fade);
                glow.transform.localScale = new Vector3(0.8f + (1f - fade) * 2.5f, glow.transform.localScale.y, 1f);
                var dim = Color.Lerp(new Color(0.45f, 0.42f, 0.5f), Color.white, fade);
                topEmitter.color = bottomEmitter.color = dim;
            }
        }

        public void Break()
        {
            Broken = true;
            Cleared = true;
            beam.enabled = false;
            fade = 1f;
        }

        public override bool Hits(Vector2 c, float r) =>
            !Broken && CircleRect(c, r, X - BeamHalfWidth, X + BeamHalfWidth, World.GroundTop - 1f, World.Top + 1f);
    }
}
