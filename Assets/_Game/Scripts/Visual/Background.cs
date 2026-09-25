using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Parallax scenery: dithered sky, twinkling stars, sun/moon/planet, drifting clouds, two hill layers and the road.
    /// Biome changes crossfade every element through a second "fade" renderer, and lerp the cloud/star/ground tints.
    /// </summary>
    public class Background : MonoBehaviour
    {
        sealed class Layer
        {
            public SpriteRenderer Main, Fade;
            public float TileWidth, Factor, Offset, Y;
            public System.Func<Art.BiomeArt, Sprite> Pick; // null = not biome dependent (ground)
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

        sealed class Flake
        {
            public SpriteRenderer Renderer;
            public float Fall, Drift, Phase, Parallax;
        }

        const float StarFieldHalfWidth = 20f;

        readonly List<Layer> layers = new List<Layer>();
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly List<Star> stars = new List<Star>();
        readonly List<Flake> flakes = new List<Flake>();
        Art art;
        SpriteRenderer sky, skyFade, sun, sunFade, sunGlow;
        Layer ground;
        Sprite[] skySprites;
        float skyTop = float.NaN, time;

        Art.BiomeArt current, target;
        float fadeT = 1f, fadeDuration = 1f;
        Color cloudTint = Color.white, groundTint = Color.white, glowTint;
        float starAlpha = 1f, snowAlpha;

        public Art.BiomeArt Current => target ?? current;

        public void Build(Art sourceArt)
        {
            art = sourceArt;
            current = art.Biomes[0];
            glowTint = current.SunGlow;

            sky = NewRenderer("Sky", -100);
            skyFade = NewRenderer("SkyFade", -99);
            sky.drawMode = skyFade.drawMode = SpriteDrawMode.Tiled;

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
            sunGlow = NewRenderer("SunGlow", -92);
            sunGlow.sprite = art.GlowRadial;
            sunGlow.transform.localPosition = sunPos;
            sunGlow.transform.localScale = Vector3.one * 4.5f;
            sun = NewRenderer("Sun", -90);
            sunFade = NewRenderer("SunFade", -89);
            sun.sprite = current.Sun;
            sun.transform.localPosition = sunFade.transform.localPosition = sunPos;

            for (int i = 0; i < 6; i++)
            {
                var r = NewRenderer("Cloud", -80);
                clouds.Add(new Cloud { Renderer = r, Factor = Random.Range(0.08f, 0.18f) });
                PlaceCloud(clouds[i], Mathf.Lerp(-World.CoverWidth * 0.5f, World.CoverWidth * 0.5f, i / 5f));
            }

            // Snowfall: far flakes behind the play field, a few big near flakes in front for depth.
            for (int i = 0; i < 70; i++)
            {
                bool near = i % 5 == 0;
                var r = NewRenderer("Flake", near ? 12 : -55);
                r.sprite = near ? art.StarBig : art.StarSmall;
                r.enabled = false;
                var flake = new Flake { Renderer = r, Fall = near ? Random.Range(1.6f, 2.4f) : Random.Range(0.6f, 1.2f), Drift = Random.Range(0.2f, 0.6f), Phase = Random.value * 6f, Parallax = near ? 0.9f : 0.35f };
                r.transform.localPosition = new Vector3(Random.Range(-StarFieldHalfWidth, StarFieldHalfWidth), Random.Range(World.Bottom, World.Top + 2f), 0f);
                flakes.Add(flake);
            }

            AddLayer("HillsFar", b => b.HillsFar, World.GroundTop - 0.3f, 0.12f, -70);
            AddLayer("HillsMid", b => b.HillsMid, World.GroundTop - 0.3f, 0.3f, -60);
            ground = AddLayer("Ground", null, World.Bottom, 1f, 0);
            ApplyTints(0f);
            Refit();
        }

        /// <summary>Re-size layers after the camera view changed (resolution / orientation).</summary>
        public void Refit()
        {
            if (!Mathf.Approximately(skyTop, World.Top))
            {
                skyTop = World.Top;
                int rows = Mathf.CeilToInt((World.Top - World.GroundTop + 1f) * World.PPU);
                skySprites = new Sprite[art.Biomes.Length];
                for (int i = 0; i < skySprites.Length; i++) skySprites[i] = Art.BuildSky(rows, art.Biomes[i].Sky);
                sky.sprite = SkyFor(current);
                if (target != null) skyFade.sprite = SkyFor(target);
            }
            foreach (var s in new[] { sky, skyFade })
            {
                if (s.sprite == null) continue;
                s.size = new Vector2(World.CoverWidth + 1f, s.sprite.rect.height / World.PPU);
                s.transform.localPosition = new Vector3(0f, World.Top, 0f);
            }

            foreach (var layer in layers)
            {
                var size = new Vector2(World.CoverWidth + layer.TileWidth * 2f, layer.Main.sprite.rect.height / World.PPU);
                layer.Main.size = size;
                if (layer.Fade != null && layer.Fade.sprite != null) layer.Fade.size = size;
            }
        }

        Sprite SkyFor(Art.BiomeArt biome) => skySprites[System.Array.IndexOf(art.Biomes, biome)];

        /// <summary>Crossfade to another biome.</summary>
        public void SetBiome(Art.BiomeArt biome, float duration)
        {
            if (target != null) FinishFade();
            if (biome == current) return;
            target = biome;
            fadeT = 0f;
            fadeDuration = Mathf.Max(0.05f, duration);
            skyFade.sprite = SkyFor(biome);
            sunFade.sprite = biome.Sun;
            foreach (var layer in layers)
            {
                if (layer.Pick == null) continue;
                layer.Fade.sprite = layer.Pick(biome);
                layer.Fade.size = layer.Main.size;
            }
            Refit();
            SetFadeAlpha(0f);
        }

        void FinishFade()
        {
            current = target;
            target = null;
            sky.sprite = SkyFor(current);
            sun.sprite = current.Sun;
            foreach (var layer in layers)
                if (layer.Pick != null) layer.Main.sprite = layer.Pick(current);
            SetFadeAlpha(0f);
            ApplyTints(1f);
        }

        void SetFadeAlpha(float a)
        {
            var c = new Color(1f, 1f, 1f, a);
            skyFade.color = sunFade.color = c;
            skyFade.enabled = sunFade.enabled = a > 0f;
            foreach (var layer in layers)
            {
                if (layer.Fade == null) continue;
                layer.Fade.color = c;
                layer.Fade.enabled = a > 0f;
            }
        }

        void ApplyTints(float k)
        {
            var to = target ?? current;
            cloudTint = Color.Lerp(current.CloudTint, to.CloudTint, k);
            groundTint = Color.Lerp(current.GroundTint, to.GroundTint, k);
            glowTint = Color.Lerp(current.SunGlow, to.SunGlow, k);
            starAlpha = Mathf.Lerp(current.StarAlpha, to.StarAlpha, k);
            snowAlpha = Mathf.Lerp(current.Snow ? 1f : 0f, to.Snow ? 1f : 0f, k);
            ground.Main.color = groundTint;
            sunGlow.color = glowTint;
            foreach (var cloud in clouds) cloud.Renderer.color = cloudTint;
        }

        public void Tick(float dt, float scroll)
        {
            time += dt;
            if (target != null)
            {
                fadeT = Mathf.Min(1f, fadeT + dt / fadeDuration);
                float k = fadeT * fadeT * (3f - 2f * fadeT);
                SetFadeAlpha(k);
                ApplyTints(k);
                if (fadeT >= 1f) FinishFade();
            }

            foreach (var layer in layers)
            {
                layer.Offset = Mathf.Repeat(layer.Offset + scroll * layer.Factor, layer.TileWidth);
                var pos = new Vector3(-layer.Offset, layer.Y, 0f);
                layer.Main.transform.localPosition = pos;
                if (layer.Fade != null) layer.Fade.transform.localPosition = pos;
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

            TickSnow(dt, scroll);

            foreach (var star in stars)
            {
                float a = (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(time * star.Rate + star.Phase))) * starAlpha;
                star.Renderer.color = new Color(1f, 1f, 1f, a);
                var t = star.Renderer.transform;
                var p = t.localPosition;
                p.x -= scroll * 0.02f;
                if (p.x < -StarFieldHalfWidth) p.x += StarFieldHalfWidth * 2f;
                t.localPosition = p;
            }
        }

        void TickSnow(float dt, float scroll)
        {
            bool visible = snowAlpha > 0.01f;
            float top = World.Top + 1f;
            foreach (var f in flakes)
            {
                f.Renderer.enabled = visible;
                if (!visible) continue;
                var t = f.Renderer.transform;
                var p = t.localPosition;
                p.y -= f.Fall * dt;
                p.x -= scroll * f.Parallax + Mathf.Sin(time * 1.3f + f.Phase) * f.Drift * dt;
                if (p.y < World.GroundTop) p.y = top;
                if (p.x < -StarFieldHalfWidth) p.x += StarFieldHalfWidth * 2f;
                t.localPosition = p;
                f.Renderer.color = new Color(1f, 1f, 1f, snowAlpha * (f.Parallax > 0.5f ? 0.9f : 0.6f));
            }
        }

        void PlaceCloud(Cloud cloud, float x)
        {
            cloud.Renderer.sprite = art.Clouds[Random.Range(0, art.Clouds.Length)];
            float top = Mathf.Min(World.Top - 1.5f, World.GroundTop + 13f);
            cloud.Renderer.transform.localPosition = new Vector3(x, Random.Range(World.GroundTop + 6f, top), 0f);
        }

        Layer AddLayer(string layerName, System.Func<Art.BiomeArt, Sprite> pick, float y, float factor, int order)
        {
            var main = NewRenderer(layerName, order);
            main.sprite = pick != null ? pick(current) : art.Ground;
            main.drawMode = SpriteDrawMode.Tiled;
            main.transform.localPosition = new Vector3(0f, y, 0f);
            SpriteRenderer fade = null;
            if (pick != null)
            {
                fade = NewRenderer(layerName + "Fade", order + 1);
                fade.drawMode = SpriteDrawMode.Tiled;
                fade.enabled = false;
            }
            var layer = new Layer { Main = main, Fade = fade, Pick = pick, TileWidth = main.sprite.rect.width / World.PPU, Factor = factor, Y = y };
            layers.Add(layer);
            return layer;
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
