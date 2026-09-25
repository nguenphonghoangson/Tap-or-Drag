using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Obstacles that Fever can smash and the BLUEJAY shield can neutralise.</summary>
    public interface IBreakable
    {
        void Break();
    }

    public abstract class Obstacle : MonoBehaviour
    {
        public float X { get; protected set; }
        public bool Cleared { get; set; }
        public abstract float HalfWidth { get; }
        public abstract bool Hits(Vector2 center, float radius);

        public virtual void Tick(float dt, float speed)
        {
            X -= speed * dt;
            Place();
        }

        protected void Place() => transform.localPosition = new Vector3(X, 0f, 0f);

        protected SpriteRenderer Part(string partName, Sprite sprite, int order)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        protected static bool CircleRect(Vector2 c, float r, float xMin, float xMax, float yMin, float yMax)
        {
            float dx = c.x - Mathf.Clamp(c.x, xMin, xMax);
            float dy = c.y - Mathf.Clamp(c.y, yMin, yMax);
            return dx * dx + dy * dy < r * r;
        }
    }
}
