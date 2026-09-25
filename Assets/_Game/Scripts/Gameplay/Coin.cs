using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Spinning pickup that scrolls with the world. Collected by GameManager when the bird overlaps it.</summary>
    public class Coin : MonoBehaviour
    {
        Art art;
        SpriteRenderer sr;
        float anim;

        public Vector2 Position;

        public void Build(Art sourceArt)
        {
            art = sourceArt;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 6;
        }

        public void Setup(Vector2 position)
        {
            Position = position;
            anim = Random.value;
            Apply();
        }

        public void Tick(float dt, float speed)
        {
            Position.x -= speed * dt;
            anim += dt;
            sr.sprite = art.Coin[(int)(anim * 10f) & 3];
            Apply();
        }

        /// <summary>Fever magnet.</summary>
        public void PullTowards(Vector2 target, float maxDelta) => Position = Vector2.MoveTowards(Position, target, maxDelta);

        void Apply() => transform.localPosition = new Vector3(Position.x, Position.y + Mathf.Sin(anim * 4f) * 0.06f, 0f);
    }
}
