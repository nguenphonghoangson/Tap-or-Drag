using UnityEngine;
using UnityEngine.UI;

namespace TapOrDrag
{
    /// <summary>UI text drawn with <see cref="PixelFont"/>. Scale = screen-canvas units per font pixel.</summary>
    public class PixelText : MonoBehaviour
    {
        Image image;
        string text;
        Color32 color;
        int scale;

        public RectTransform Rect => (RectTransform)transform;
        public float Width => Rect.sizeDelta.x;
        public float Height => Rect.sizeDelta.y;

        /// <summary>Multiplies the rendered colour (white text + tint = cheap animated colour without new sprites).</summary>
        public Color Tint
        {
            get => image.color;
            set => image.color = value;
        }

        public bool Visible
        {
            get => image.enabled;
            set => image.enabled = value;
        }

        public static PixelText Create(Transform parent, string objectName, string text, Color32 color, int scale,
            Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<PixelText>();
            t.image = go.GetComponent<Image>();
            t.image.raycastTarget = false;
            var rt = t.Rect;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            t.color = color;
            t.scale = scale;
            t.Set(text);
            return t;
        }

        public void Set(string value)
        {
            if (value == text) return;
            text = value;
            Refresh();
        }

        public void SetScale(int value)
        {
            if (value == scale) return;
            scale = value;
            Refresh();
        }

        public void SetColor(Color32 value)
        {
            if (value.Equals(color)) return;
            color = value;
            Refresh();
        }

        void Refresh()
        {
            var sprite = PixelFont.Get(text, color);
            image.sprite = sprite;
            Rect.sizeDelta = sprite.rect.size * scale;
        }
    }
}
