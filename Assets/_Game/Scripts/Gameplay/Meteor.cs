using UnityEngine;

namespace TapOrDrag
{
    /// <summary>SPACE hazard: a rock that streaks across the screen at a locked height after a warning marker.</summary>
    public class Meteor : Obstacle, IBreakable
    {
        const float Radius = 0.42f;

        SpriteRenderer sr;
        float extraSpeed, spin;
        bool broken;

        public float Y { get; private set; }
        public bool Broken => broken;
        public Vector2 Position => new Vector2(X, Y);
        public override float HalfWidth => Radius;

        public void Build(Art art)
        {
            sr = Part("Rock", art.Meteor, 7);
        }

        public void Setup(float x, float y, float speedOverScroll)
        {
            X = x;
            Y = y;
            Cleared = false;
            broken = false;
            extraSpeed = speedOverScroll;
            spin = 0f;
            sr.enabled = true;
            sr.transform.localPosition = new Vector3(0f, Y, 0f);
            Place();
        }

        public override void Tick(float dt, float scroll)
        {
            X -= (scroll + extraSpeed) * dt;
            spin += dt * 300f;
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
            Place();
        }

        public void Break()
        {
            broken = true;
            Cleared = true;
            sr.enabled = false;
        }

        public override bool Hits(Vector2 c, float r)
        {
            if (broken) return false;
            float rr = r + Radius;
            return (c - Position).sqrMagnitude < rr * rr;
        }
    }
}
