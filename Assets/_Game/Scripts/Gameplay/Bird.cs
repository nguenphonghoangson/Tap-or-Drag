using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Player (a puppy piloting a flying saucer; wing frames = engine flame sizes): flappy physics, dash freeze, and sprite animation (wing cycle, blink, tilt, squash).</summary>
    public class Bird : MonoBehaviour
    {
        static readonly int[] WingCycle = { 0, 1, 2, 1 };

        GameConfig cfg;
        Art art;
        Art.SkinArt skin;
        Color tint = Color.white;
        Vector3 hatAnchor;
        bool hatRigid;
        SpriteRenderer sr, scarf, hat, bubble;
        bool shieldOn, phasing;
        float invulnerable, bubbleTime, feverScale = 1f, feverScaleTarget = 1f, flipScale = 1f;
        float baseY, bobTime, wingPhase, wingBoost, blinkTimer, blinkLeft, tilt, hurtFlash, scarfPhase;
        float hatOffset, hatVelocity, hatLean, hatSpin;
        bool hatFlying;
        Vector2 hatFlyVelocity;
        Vector2 squash = Vector2.one;

        public float Vy { get; set; }
        public bool Dashing { get; private set; }
        public bool Dead { get; private set; }
        public bool Grounded { get; private set; }
        public int WingFrame { get; private set; }
        public bool Invulnerable => invulnerable > 0f;

        // Skill physics scales (ROBO anti-grav). 1 = default.
        public float GravityScale { get; set; } = 1f;
        public float FlapScale { get; set; } = 1f;
        public float FallScale { get; set; } = 1f;

        /// <summary>+1 normal, -1 while a gravity portal has flipped gravity (gravity pulls up, flaps push down).</summary>
        public float GravitySign { get; private set; } = 1f;

        public void SetGravityInverted(bool inverted)
        {
            GravitySign = inverted ? -1f : 1f;
            Vy *= -0.3f; // keep a little momentum, but in the new "up"
        }
        public Vector2 Position => transform.position;
        public Sprite Silhouette => art.BirdSilhouette[WingFrame];

        public void Build(Art sourceArt, GameConfig config)
        {
            art = sourceArt;
            cfg = config;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;

            // Scarf ends sit behind the body, knotted at the back of the neck.
            var tail = new GameObject("ScarfTail");
            tail.transform.SetParent(transform, false);
            tail.transform.localPosition = new Vector3(-0.3f, -0.02f, 0f); // back of the pilot's collar
            scarf = tail.AddComponent<SpriteRenderer>();
            scarf.sortingOrder = 9;

            var hatGo = new GameObject("Hat");
            hatGo.transform.SetParent(transform, false);
            hat = hatGo.AddComponent<SpriteRenderer>();
            hat.sortingOrder = 11;

            var bubbleGo = new GameObject("ShieldBubble");
            bubbleGo.transform.SetParent(transform, false);
            bubble = bubbleGo.AddComponent<SpriteRenderer>();
            bubble.sprite = art.ShieldBubble;
            bubble.sortingOrder = 12;
            bubble.enabled = false;
        }

        public void ResetAt(float y)
        {
            transform.position = new Vector3(World.BirdX, y, 0f);
            baseY = y;
            Vy = 0f;
            Dashing = Dead = Grounded = false;
            tilt = 0f;
            bobTime = 0f;
            hurtFlash = 0f;
            squash = Vector2.one;
            invulnerable = 0f;
            phasing = false;
            feverScale = feverScaleTarget = 1f;
            GravitySign = flipScale = 1f;
            blinkTimer = Random.Range(1.5f, 3.5f);

            hatFlying = false;
            hatOffset = hatVelocity = hatLean = 0f;
            hat.transform.SetParent(transform, false);
            hat.transform.localScale = Vector3.one;
            hat.transform.localRotation = Quaternion.identity;
            hat.transform.localPosition = hatAnchor;
            Animate(0f, 0f);
        }

        /// <summary>Swap sprites/hat/scarf. Locked skins are previewed as a dark silhouette.</summary>
        public void ApplySkin(Art.SkinArt newSkin, bool locked)
        {
            skin = newSkin;
            tint = locked ? new Color(0.14f, 0.1f, 0.22f) : Color.white;
            scarf.enabled = skin.ScarfTail != null;
            hat.enabled = skin.Hat != null;
            hat.sprite = skin.Hat;
            hatAnchor = skin.HatOffset;
            hatRigid = skin.HatRigid;
            if (!hatFlying)
            {
                hat.transform.localPosition = hatAnchor;
                hat.transform.localRotation = Quaternion.identity;
            }
            Animate(0f, 0f);
        }

        /// <summary>Little squash-and-stretch pop, used when the skin changes.</summary>
        public void Pop()
        {
            squash = new Vector2(1.3f, 0.75f);
            hatVelocity -= 3f;
        }

        public void SetShield(bool on) => shieldOn = on;
        public void StartInvulnerable(float seconds) => invulnerable = seconds;
        public void SetPhasing(bool on) => phasing = on;
        public void SetFeverScale(float scale) => feverScaleTarget = scale;

        /// <summary>Push back inside the play field (ground/ceiling while invulnerable).</summary>
        public void Bounce(float y, float vy)
        {
            var p = transform.position;
            p.y = y;
            transform.position = p;
            Vy = vy;
            squash = new Vector2(1.25f, 0.8f);
        }

        public void Flap()
        {
            Vy = cfg.flapVelocity * FlapScale * GravitySign;
            wingBoost = 1f;
            wingPhase = 0f;
            squash = new Vector2(0.78f, 1.25f);
            hatVelocity -= 2.2f; // hat lags behind the jump, squashes down, springs back
        }

        public void BeginDash()
        {
            Dashing = true;
            Vy = 0f;
            squash = new Vector2(1.45f, 0.7f);
        }

        public void EndDash()
        {
            Dashing = false;
            Vy = 1.5f * GravitySign;
            squash = new Vector2(0.9f, 1.1f);
        }

        /// <summary>Vertical homing while a dash is locked onto an enemy.</summary>
        public void HomeTowards(float y, float maxDelta)
        {
            var p = transform.position;
            p.y = Mathf.MoveTowards(p.y, y, maxDelta);
            transform.position = p;
        }

        public void Kill(float knockVy)
        {
            Dead = true;
            Dashing = false;
            GravitySign = 1f; // the dead bird just falls
            Vy = knockVy;
            hurtFlash = 0.3f;
            Grounded = false;

            // The beanie pops off and tumbles on its own.
            hatFlying = true;
            hat.transform.SetParent(transform.parent, true);
            hat.transform.localScale = Vector3.one;
            hatFlyVelocity = new Vector2(Random.Range(-2f, -0.8f), Random.Range(6f, 8f));
            hatSpin = Random.Range(240f, 420f) * (Random.value < 0.5f ? -1f : 1f);
        }

        public void TickIdle(float dt)
        {
            bobTime += dt;
            var p = transform.position;
            p.y = baseY + Mathf.Sin(bobTime * 3.2f) * 0.28f;
            transform.position = p;
            tilt = Mathf.Cos(bobTime * 3.2f) * 6f;
            Animate(dt, 9f);
        }

        public void TickPlay(float dt)
        {
            if (!Dashing)
            {
                float maxFall = cfg.maxFallSpeed * FallScale;
                Vy = Mathf.Clamp(Vy - cfg.gravity * GravityScale * GravitySign * dt, GravitySign > 0f ? -maxFall : -1000f, GravitySign > 0f ? 1000f : maxFall);
                transform.position += new Vector3(0f, Vy * dt, 0f);
            }
            // Nose follows the motion relative to the current gravity; mirrored when upside down.
            float target = Dashing ? 0f : GravitySign * Mathf.Clamp(Vy * GravitySign * 4.2f + 10f, -80f, 28f);
            tilt = Mathf.MoveTowards(tilt, target, (target > tilt ? 720f : 300f) * dt);
            Animate(dt, Dashing ? 0f : 6f + wingBoost * 22f);
        }

        public void TickDead(float dt)
        {
            if (!Grounded)
            {
                Vy = Mathf.Max(Vy - cfg.gravity * dt, -cfg.maxFallSpeed * 1.3f);
                var p = transform.position;
                p.y += Vy * dt;
                float floor = World.GroundTop + 0.35f;
                if (p.y <= floor)
                {
                    p.y = floor;
                    Grounded = true;
                    Vy = 0f;
                    tilt = -90f;
                    squash = new Vector2(1.3f, 0.75f);
                }
                else
                {
                    tilt -= 540f * dt;
                }
                transform.position = p;
            }
            Animate(dt, 0f);
        }

        void Animate(float dt, float wingRate)
        {
            wingBoost = Mathf.MoveTowards(wingBoost, 0f, dt * 2.5f);
            if (Dashing) WingFrame = 0; // full thrust
            else if (Dead) WingFrame = 2;
            else
            {
                wingPhase = (wingPhase + wingRate * dt) % 4f;
                WingFrame = WingCycle[(int)wingPhase & 3];
            }

            if (blinkLeft > 0f) blinkLeft -= dt;
            else
            {
                blinkTimer -= dt;
                if (blinkTimer <= 0f)
                {
                    blinkLeft = 0.12f;
                    blinkTimer = Random.value < 0.2f ? 0.25f : Random.Range(1.8f, 4.5f);
                }
            }
            bool eyesClosed = Dead || blinkLeft > 0f;

            var rest = Dashing ? new Vector2(1.25f, 0.82f) : Vector2.one;
            squash = Vector2.Lerp(squash, rest, 1f - Mathf.Exp(-14f * dt));
            feverScale = Mathf.MoveTowards(feverScale, feverScaleTarget, dt * 3f);
            flipScale = Mathf.MoveTowards(flipScale, GravitySign, dt * 10f); // quick flip through the horizontal axis
            transform.localScale = new Vector3(squash.x * feverScale, squash.y * feverScale * flipScale, 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, tilt);
            sr.sprite = skin.Frames[WingFrame, eyesClosed ? 1 : 0];

            if (hurtFlash > 0f)
            {
                hurtFlash -= dt;
                sr.color = ((int)(hurtFlash * 30f) & 1) == 0 ? new Color(1f, 0.45f, 0.45f) : tint;
            }
            else sr.color = tint;
            if (phasing) sr.color *= new Color(0.6f, 0.45f, 1f, 0.55f);
            if (invulnerable > 0f)
            {
                invulnerable -= dt;
                if (((int)(invulnerable * 16f) & 1) == 0) sr.color *= new Color(1f, 1f, 1f, 0.35f);
            }

            bubbleTime += dt;
            bubble.enabled = shieldOn && !Dead;
            if (bubble.enabled)
            {
                float wobble = 1f + 0.05f * Mathf.Sin(bubbleTime * 6f);
                bubble.transform.localScale = new Vector3(wobble, 2f - wobble, 1f);
                bubble.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(bubbleTime * 3f));
            }

            float scarfRate = Dead ? (Grounded ? 0f : 6f) : Dashing ? 30f : 12f;
            scarfPhase = (scarfPhase + scarfRate * dt) % 4f;
            if (skin.ScarfTail != null) scarf.sprite = skin.ScarfTail[(int)scarfPhase & 3];
            scarf.color = sr.color;

            AnimateHat(dt);
        }

        void AnimateHat(float dt)
        {
            var t = hat.transform;
            if (hatFlying)
            {
                float floor = World.GroundTop + 0.05f;
                var p = t.position;
                if (p.y > floor || hatFlyVelocity.y > 0f)
                {
                    hatFlyVelocity.y -= cfg.gravity * 0.8f * dt;
                    p += (Vector3)(hatFlyVelocity * dt);
                    t.Rotate(0f, 0f, hatSpin * dt);
                    if (p.y <= floor)
                    {
                        p.y = floor;
                        hatFlyVelocity = Vector2.zero;
                        t.rotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
                    }
                    t.position = p;
                }
                return;
            }

            if (hatRigid)
            {
                t.localPosition = hatAnchor;
                t.localRotation = Quaternion.identity;
                hat.color = sr.color;
                return;
            }

            // Damped spring for the bounce; offset snapped to the pixel grid so the hat stays crisp.
            hatVelocity += (-220f * hatOffset - 16f * hatVelocity) * dt;
            hatOffset = Mathf.Clamp(hatOffset + hatVelocity * dt, -0.2f, 0.2f);
            float snapped = Mathf.Round(hatOffset * World.PPU) / World.PPU;
            hatLean = Mathf.Lerp(hatLean, Dashing ? 14f : 0f, 1f - Mathf.Exp(-12f * dt));
            t.localPosition = hatAnchor + new Vector3(0f, snapped, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, hatLean);
            hat.color = sr.color;
        }
    }
}
