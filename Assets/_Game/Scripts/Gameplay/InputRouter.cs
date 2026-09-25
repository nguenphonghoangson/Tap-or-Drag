using System;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Tap fires on press (no delay, so flapping feels instant). Swipe fires once per press when the pointer
    /// travels horizontally past a threshold. Keyboard: Space/W/Up = tap, D/Right/Shift = swipe.
    /// </summary>
    public sealed class InputRouter
    {
        public event Action Tap;
        public event Action Swipe;

        readonly GameConfig cfg;
        readonly Func<Vector2, bool> isBlocked;
        bool tracking, swiped;
        int pointerId = -1;
        Vector2 start;

        public InputRouter(GameConfig config, Func<Vector2, bool> blocked)
        {
            cfg = config;
            isBlocked = blocked;
        }

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                Tap?.Invoke();
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) ||
                Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
                Swipe?.Invoke();

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++) HandleTouch(Input.GetTouch(i));
                return;
            }
            if (tracking && pointerId >= 0) tracking = false; // touch ended without an event

            if (Input.GetMouseButtonDown(0)) Begin(Input.mousePosition, -1);
            else if (tracking && pointerId == -1)
            {
                Move(Input.mousePosition);
                if (!Input.GetMouseButton(0)) tracking = false;
            }
        }

        void HandleTouch(Touch t)
        {
            switch (t.phase)
            {
                case TouchPhase.Began:
                    Begin(t.position, t.fingerId);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (tracking && t.fingerId == pointerId) Move(t.position);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (tracking && t.fingerId == pointerId)
                    {
                        Move(t.position);
                        tracking = false;
                    }
                    break;
            }
        }

        void Begin(Vector2 pos, int id)
        {
            if (isBlocked != null && isBlocked(pos)) return;
            if (!tracking)
            {
                tracking = true;
                swiped = false;
                pointerId = id;
                start = pos;
            }
            Tap?.Invoke();
        }

        void Move(Vector2 pos)
        {
            if (swiped) return;
            Vector2 d = pos - start;
            float threshold = Mathf.Max(20f, Mathf.Min(Screen.width, Screen.height) * cfg.swipeThreshold);
            if (Mathf.Abs(d.x) >= threshold && Mathf.Abs(d.x) > Mathf.Abs(d.y) * 1.2f)
            {
                swiped = true;
                Swipe?.Invoke();
            }
        }
    }
}
