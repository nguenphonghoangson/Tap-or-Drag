using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Play-field layout. Design resolution is 405x820 portrait; at that size 1 art pixel = 3 screen pixels.
    /// The camera always keeps the ground at the bottom edge; taller screens get extra sky, wider screens extra side view.
    /// </summary>
    public static class World
    {
        public const float PPU = 16f;
        public const float ViewHeight = 17f;
        public const float ViewWidth = ViewHeight * 405f / 820f;
        public const float Bottom = -ViewHeight * 0.5f;
        public const float GroundHeight = 36f / PPU;
        public const float GroundTop = Bottom + GroundHeight;
        public const float BirdX = -ViewWidth * 0.2f;
        public const float PlayHeight = 14.75f;

        public static float Top { get; private set; } = ViewHeight * 0.5f;
        public static float CamHalfWidth { get; private set; } = ViewWidth * 0.5f;
        public static float CamHalfHeight { get; private set; } = ViewHeight * 0.5f;
        public static float CamCenterY => Bottom + CamHalfHeight;
        public static float CoverWidth => CamHalfWidth * 2f + 2f;
        public static float SpawnX => CamHalfWidth + 2f;
        public static float PlayTop => Mathf.Min(Top, GroundTop + PlayHeight);

        public static void Fit(Camera cam)
        {
            float aspect = Mathf.Max(0.1f, cam.aspect);
            float half = Mathf.Max(ViewHeight * 0.5f, ViewWidth * 0.5f / aspect);
            cam.orthographic = true;
            cam.orthographicSize = half;
            CamHalfHeight = half;
            CamHalfWidth = half * aspect;
            Top = Bottom + half * 2f;
        }
    }
}
