using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Full-height, non-solid portal. Flying through it flips gravity (or restores it). Chevrons in the field point
    /// towards the new "down". Kept outside the obstacle list: it is neither a hazard nor a scoring clear.
    /// </summary>
    public class GravityPortal : MonoBehaviour
    {
        const float HalfWidth = 8f / World.PPU;

        Art art;
        SpriteRenderer field, glow;
        Sprite[] frames;
        float anim, flash;

        public float X { get; private set; }
        public bool Inverts { get; private set; }  // true: gravity will point up after crossing
        public bool Crossed { get; private set; }

        public void Build(Art sourceArt)
        {
            art = sourceArt;
            glow = NewPart("Glow", art.GlowH, -15);
            field = NewPart("Field", art.PortalBeam[0][0], -8);
            field.drawMode = SpriteDrawMode.Tiled;
        }

        SpriteRenderer NewPart(string partName, Sprite sprite, int order)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        public void Setup(float x, bool inverts)
        {
            X = x;
            Inverts = inverts;
            Crossed = false;
            anim = Random.value;
            flash = 0f;
            frames = art.PortalBeam[inverts ? 0 : 1];

            float top = World.Top + 0.5f, bottom = World.GroundTop, mid = (top + bottom) * 0.5f;
            field.sprite = frames[0];
            field.size = new Vector2(HalfWidth * 2f, top - bottom);
            field.transform.localPosition = new Vector3(0f, mid, 0f);
            glow.transform.localPosition = new Vector3(0f, mid, 0f);
            glow.transform.localScale = new Vector3(1.4f, (top - bottom) / glow.sprite.bounds.size.y, 1f);
            Place();
        }

        public void Tick(float dt, float speed)
        {
            X -= speed * dt;
            anim += dt;
            flash = Mathf.MoveTowards(flash, 0f, dt * 2f);
            field.sprite = frames[(int)(anim * 10f) & 3];
            Color c = Art.PortalMain;
            float pulse = 0.35f + 0.15f * Mathf.Sin(anim * 6f) + flash * 0.6f;
            glow.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(pulse));
            glow.transform.localScale = new Vector3(1.4f + flash * 2f, glow.transform.localScale.y, 1f);
            field.color = new Color(1f, 1f, 1f, Crossed ? 0.5f : 1f);
            Place();
        }

        /// <summary>True the first frame the bird's centre passes the portal.</summary>
        public bool CheckCross(float birdX)
        {
            if (Crossed || birdX < X) return false;
            Crossed = true;
            flash = 1f;
            return true;
        }

        void Place() => transform.localPosition = new Vector3(X, 0f, 0f);
    }
}
