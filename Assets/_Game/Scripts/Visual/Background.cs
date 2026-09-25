using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Parallax scenery: dithered sky, twinkling stars, sun, drifting clouds, two hill layers and the scrolling road.</summary>
    public class Background : MonoBehaviour
    {
        sealed class Layer
        {
            public SpriteRenderer Renderer;
            public float TileWidth, Factor, Offset, Y;
        }

        sealed class Cloud
        {
            public SpriteRenderer Renderer;
            public float Factor;
        }

        sealed class Star
        {
            public SpriteRenderer Renderer;
            public float Phase, Rate;
        }

        const float StarFieldHalfWidth = 20f;

        readonly List<Layer> layers = new List<Layer>();
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly List<Star> stars = new List<Star>();
        Art art;
        SpriteRenderer sky;
        float skyTop = float.NaN, time;

        public void Build(Art sourceArt)
        {
            art = sourceArt;
            sky = NewRenderer("Sky", -100);
            sky.drawMode = SpriteDrawMode.Tiled;

            for (int i = 0; i < 80; i++)
            {
                var r = NewRenderer("Star", -96);
                r.sprite = Random.value < 0.2f ? art.StarBig : art.StarSmall;
                r.transform.localPosition = new Vector3(
                    Random.Range(-StarFieldHalfWidth, StarFieldHalfWidth),
                    Random.Range(World.GroundTop + 7.5f, World.GroundTop + 22f), 0f);
                stars.Add(new Star { Renderer = r, Phase = Random.value * 10f, Rate = Random.Range(1.5f, 4f) });
            }

            var sunPos = new Vector3(1.3f, World.GroundTop + 4.6f, 0f);
            var sunGlow = NewRenderer("SunGlow", -92);
            sunGlow.sprite = art.GlowRadial;
            sunGlow.color = new Color(1f, 0.45f, 0.6f, 0.35f);
            sunGlow.transform.localPosition = sunPos;
            sunGlow.transform.localScale = Vector3.one * 4.5f;
            var sun = NewRenderer("Sun", -90);
            sun.sprite = art.Sun;
            sun.transform.localPosition = sunPos;

            for (int i = 0; i < 6; i++)
            {
                var r = NewRenderer("Cloud", -80);
                clouds.Add(new Cloud { Renderer = r, Factor = Random.Range(0.08f, 0.18f) });
                PlaceCloud(clouds[i], Mathf.Lerp(-World.CoverWidth * 0.5f, World.CoverWidth * 0.5f, i / 5f));
            }

            AddLayer("HillsFar", art.HillsFar, World.GroundTop - 0.3f, 0.12f, -70);
            AddLayer("HillsMid", art.HillsMid, World.GroundTop - 0.3f, 0.3f, -60);
            AddLayer("Ground", art.Ground, World.Bottom, 1f, 0);
            Refit();
        }

        /// <summary>Re-size layers after the camera view changed (resolution / orientation).</summary>
        public void Refit()
        {
            if (!Mathf.Approximately(skyTop, World.Top))
            {
                skyTop = World.Top;
                int rows = Mathf.CeilToInt((World.Top - World.GroundTop + 1f) * World.PPU);
                sky.sprite = Art.BuildSky(rows);
            }
            sky.size = new Vector2(World.CoverWidth + 1f, sky.sprite.rect.height / World.PPU);
            sky.transform.localPosition = new Vector3(0f, World.Top, 0f);

            foreach (var layer in layers)
                layer.Renderer.size = new Vector2(World.CoverWidth + layer.TileWidth * 2f, layer.Renderer.sprite.rect.height / World.PPU);
        }

        public void Tick(float dt, float scroll)
        {
            time += dt;
            foreach (var layer in layers)
            {
                layer.Offset = Mathf.Repeat(layer.Offset + scroll * layer.Factor, layer.TileWidth);
                layer.Renderer.transform.localPosition = new Vector3(-layer.Offset, layer.Y, 0f);
            }

            float edge = World.CoverWidth * 0.5f + 3.5f;
            foreach (var cloud in clouds)
            {
                var t = cloud.Renderer.transform;
                var p = t.localPosition;
                p.x -= scroll * cloud.Factor + dt * 0.12f;
                t.localPosition = p;
                if (p.x < -edge) PlaceCloud(cloud, edge);
            }

            foreach (var star in stars)
            {
                float a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * star.Rate + star.Phase));
                star.Renderer.color = new Color(1f, 1f, 1f, a);
                var t = star.Renderer.transform;
                var p = t.localPosition;
                p.x -= scroll * 0.02f;
                if (p.x < -StarFieldHalfWidth) p.x += StarFieldHalfWidth * 2f;
                t.localPosition = p;
            }
        }

        void PlaceCloud(Cloud cloud, float x)
        {
            cloud.Renderer.sprite = art.Clouds[Random.Range(0, art.Clouds.Length)];
            float top = Mathf.Min(World.Top - 1.5f, World.GroundTop + 13f);
            cloud.Renderer.transform.localPosition = new Vector3(x, Random.Range(World.GroundTop + 6f, top), 0f);
        }

        void AddLayer(string layerName, Sprite sprite, float y, float factor, int order)
        {
            var r = NewRenderer(layerName, order);
            r.sprite = sprite;
            r.drawMode = SpriteDrawMode.Tiled;
            r.transform.localPosition = new Vector3(0f, y, 0f);
            layers.Add(new Layer { Renderer = r, TileWidth = sprite.rect.width / World.PPU, Factor = factor, Y = y });
        }

        SpriteRenderer NewRenderer(string rendererName, int order)
        {
            var go = new GameObject(rendererName);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            return r;
        }
    }
}
