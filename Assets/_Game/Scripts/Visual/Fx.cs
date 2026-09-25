using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Juice: pixel particles, dash afterimages, floating score text, camera shake and zoom kick.</summary>
    public class Fx : MonoBehaviour
    {
        sealed class Ghost
        {
            public SpriteRenderer Renderer;
            public Color Color;
            public float Life, MaxLife;
        }

        sealed class Floater
        {
            public SpriteRenderer Renderer;
            public float Life, MaxLife, Scale, Vy, Vx;
        }

        Camera cam;
        ParticleSystem debris, sparkle;
        float shakeTime, shakeDuration, shakeAmplitude, kick;
        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly List<Floater> floaters = new List<Floater>();

        public void Build(Art art, Camera targetCamera)
        {
            cam = targetCamera;
            var shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { mainTexture = art.ParticleTexture };
            debris = MakeSystem("Debris", 1.6f, 5, material);
            sparkle = MakeSystem("Sparkle", 0f, 6, material);
        }

        ParticleSystem MakeSystem(string systemName, float gravity, int order, Material material)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = material;
            r.sortingOrder = order;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector2 pos, Vector2 vel, Color32 color, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = new Vector3(pos.x, pos.y, 0f),
                velocity = new Vector3(vel.x, vel.y, 0f),
                startColor = color,
                startSize = size,
                startLifetime = life,
                applyShapeToPosition = false,
            };
            ps.Emit(ep, 1);
        }

        static float PixelSize(int minPx, int maxPx) => Random.Range(minPx, maxPx + 1) / World.PPU;

        public void Burst(Vector2 pos, Color32 a, Color32 b, int count, float speed, float drift, bool gravity, float life = 0.6f)
        {
            var ps = gravity ? debris : sparkle;
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                var v = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (speed * Random.Range(0.35f, 1f));
                v.x -= drift;
                if (gravity) v.y += 2f;
                Emit(ps, pos, v, Random.value < 0.5f ? a : b, PixelSize(2, 4), life * Random.Range(0.6f, 1.1f));
            }
        }

        public void CoinPickup(Vector2 pos)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Emit(sparkle, pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.5f, 3.5f), i % 2 == 0 ? Pal.Hex("fff6e0") : Pal.White, PixelSize(1, 2), 0.3f);
            }
        }

        public void PipePass(Vector2 pos, float worldSpeed)
        {
            Burst(pos, Pal.Orange, Pal.OrangeLight, 18, 6f, worldSpeed * 0.8f, true);
            Burst(pos, Pal.White, Pal.Gold, 8, 3f, worldSpeed * 0.8f, false, 0.4f);
        }

        public void GateBreak(float gateX, int v, float worldSpeed)
        {
            for (int i = 0; i < 48; i++)
            {
                var pos = new Vector2(gateX + Random.Range(-0.3f, 0.3f), Random.Range(World.GroundTop, World.Top));
                var vel = new Vector2(Random.Range(-2f, 5f) - worldSpeed * 0.6f, Random.Range(-2.5f, 2.5f));
                Color32 c = i % 3 == 0 ? Art.GateCore[v] : i % 3 == 1 ? Art.GateMain[v] : Art.GateDeep[v];
                Emit(i % 3 == 0 ? debris : sparkle, pos, vel, c, PixelSize(2, 4), Random.Range(0.3f, 0.7f));
            }
        }

        public void EnemyStomp(Vector2 pos, float worldSpeed)
        {
            Burst(pos, Art.SpikyColor, Pal.Hex("d4ff8a"), 20, 7f, worldSpeed * 0.6f, true, 0.7f);
            Burst(pos, Pal.Hex("fff1c4"), Pal.White, 10, 5f, worldSpeed * 0.6f, false, 0.4f);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                var vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f - new Vector2(worldSpeed * 0.6f, 0f);
                Emit(sparkle, pos, vel, Pal.Gold, 3f / World.PPU, 0.3f);
            }
        }

        public void PipeSmash(PipePair pipe, float worldSpeed)
        {
            for (int i = 0; i < 44; i++)
            {
                bool top = i % 2 == 0;
                float y = top ? pipe.GapTop + Random.Range(0f, 3.5f) : pipe.GapBottom - Random.Range(0f, 3.5f);
                var pos = new Vector2(pipe.X + Random.Range(-0.7f, 0.7f), y);
                var vel = new Vector2(Random.Range(-1f, 6f) - worldSpeed * 0.5f, (top ? 1f : -1f) * Random.Range(1f, 6f));
                Color32 c = i % 3 == 0 ? Pal.Hex("ffe0a6") : i % 3 == 1 ? Pal.Orange : Pal.Hex("c24b1a");
                Emit(debris, pos, vel, c, PixelSize(2, 5), Random.Range(0.4f, 0.9f));
            }
        }

        public void PerfectRing(Vector2 pos, Color32 color)
        {
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Emit(sparkle, pos + dir * 0.4f, dir * 8f, i % 2 == 0 ? color : (Color32)Color.white, 3f / World.PPU, 0.3f);
            }
        }

        public void ShieldPop(Vector2 pos)
        {
            Burst(pos, Pal.Hex("8ff6ff"), Pal.White, 24, 7f, 0f, false, 0.45f);
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Emit(sparkle, pos + dir * 0.8f, dir * 3f, Pal.Hex("3ff0ff"), 2f / World.PPU, 0.35f);
            }
        }

        /// <summary>Single drifting sparkle, used for skill trails.</summary>
        public void Trail(Vector2 pos, Color32 color, float worldSpeed)
        {
            var vel = new Vector2(-worldSpeed * 0.5f + Random.Range(-0.5f, 0.5f), Random.Range(-0.8f, 0.8f));
            Emit(sparkle, pos + Random.insideUnitCircle * 0.3f, vel, color, PixelSize(1, 2), Random.Range(0.3f, 0.6f));
        }

        public void DashStart(Vector2 pos, Color32 color)
        {
            for (int i = 0; i < 12; i++)
            {
                var vel = new Vector2(-Random.Range(4f, 10f), Random.Range(-1.5f, 1.5f));
                Emit(sparkle, pos + new Vector2(-0.3f, Random.Range(-0.3f, 0.3f)), vel, i % 2 == 0 ? color : (Color32)Color.white, PixelSize(1, 3), Random.Range(0.2f, 0.4f));
            }
        }

        public void Death(Vector2 pos, int gateVariant)
        {
            Burst(pos, Pal.Gold, Pal.White, 28, 7f, 0f, true, 0.9f);
            if (gateVariant >= 0) Burst(pos, Art.GateMain[gateVariant], Art.GateCore[gateVariant], 22, 9f, 0f, false, 0.5f);
            else Burst(pos, Pal.Hex("ff7b2e"), Pal.White, 10, 5f, 0f, false, 0.4f);
        }

        public void FlapPuff(Vector2 pos)
        {
            for (int i = 0; i < 3; i++)
            {
                var vel = new Vector2(-Random.Range(1.5f, 3f), -Random.Range(0.5f, 2f));
                Emit(sparkle, pos + new Vector2(-0.45f, -0.15f), vel, new Color32(255, 255, 255, 170), PixelSize(1, 2), 0.25f);
            }
        }

        public void Dust(Vector2 pos)
        {
            for (int i = 0; i < 12; i++)
            {
                var vel = new Vector2(Random.Range(-3f, 3f), Random.Range(0.5f, 3f));
                Emit(debris, new Vector2(pos.x + Random.Range(-0.4f, 0.4f), World.GroundTop + 0.05f), vel, Pal.Hex("8a6aa8"), PixelSize(1, 3), 0.5f);
            }
        }

        public void Afterimage(Bird bird, Color color)
        {
            Ghost g = null;
            foreach (var candidate in ghosts)
                if (candidate.Life <= 0f) { g = candidate; break; }
            if (g == null)
            {
                var go = new GameObject("Ghost");
                go.transform.SetParent(transform, false);
                g = new Ghost { Renderer = go.AddComponent<SpriteRenderer>() };
                g.Renderer.sortingOrder = 8;
                ghosts.Add(g);
            }
            var t = g.Renderer.transform;
            t.position = bird.transform.position;
            t.rotation = bird.transform.rotation;
            t.localScale = bird.transform.localScale;
            g.Renderer.sprite = bird.Silhouette;
            g.Renderer.enabled = true;
            g.Color = color;
            g.Life = g.MaxLife = 0.22f;
        }

        public void Float(string text, Color32 color, Vector2 pos, float scale = 1f, float drift = 0f)
        {
            Floater f = null;
            foreach (var candidate in floaters)
                if (candidate.Life <= 0f) { f = candidate; break; }
            if (f == null)
            {
                var go = new GameObject("Floater");
                go.transform.SetParent(transform, false);
                f = new Floater { Renderer = go.AddComponent<SpriteRenderer>() };
                f.Renderer.sortingOrder = 20;
                floaters.Add(f);
            }
            f.Renderer.sprite = PixelFont.Get(text, color);
            f.Renderer.enabled = true;
            f.Renderer.color = Color.white;
            f.Renderer.transform.position = new Vector3(pos.x, pos.y, 0f);
            f.Scale = scale;
            f.Vy = 1.8f;
            f.Vx = -drift;
            f.Life = f.MaxLife = 0.8f;
        }

        public void Shake(float amplitude, float duration)
        {
            float remaining = shakeTime > 0f ? shakeAmplitude * shakeTime / shakeDuration : 0f;
            if (amplitude < remaining) return;
            shakeAmplitude = amplitude;
            shakeDuration = shakeTime = duration;
        }

        public void Kick(float amount) => kick = Mathf.Max(kick, amount);

        public void Tick(float dt, float worldSpeed)
        {
            foreach (var g in ghosts)
            {
                if (g.Life <= 0f) continue;
                g.Life -= dt;
                if (g.Life <= 0f) { g.Renderer.enabled = false; continue; }
                g.Renderer.transform.position += Vector3.left * (worldSpeed * dt);
                var c = g.Color;
                c.a *= g.Life / g.MaxLife;
                g.Renderer.color = c;
            }

            foreach (var f in floaters)
            {
                if (f.Life <= 0f) continue;
                f.Life -= dt;
                if (f.Life <= 0f) { f.Renderer.enabled = false; continue; }
                f.Vy = Mathf.MoveTowards(f.Vy, 0.3f, dt * 3f);
                f.Renderer.transform.position += new Vector3(f.Vx * dt, f.Vy * dt, 0f);
                float k = 1f - f.Life / f.MaxLife;
                float pop = k < 0.15f ? Mathf.Lerp(1.7f, 1f, k / 0.15f) : 1f;
                f.Renderer.transform.localScale = Vector3.one * (f.Scale * pop);
                f.Renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(f.Life / (f.MaxLife * 0.4f)));
            }

            if (shakeTime > 0f) shakeTime -= dt;
            kick = Mathf.MoveTowards(kick, 0f, dt * 0.4f);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            Vector2 offset = Vector2.zero;
            if (shakeTime > 0f)
            {
                float k = shakeTime / shakeDuration;
                offset = Random.insideUnitCircle * (shakeAmplitude * k * k);
                offset = new Vector2(Mathf.Round(offset.x * World.PPU), Mathf.Round(offset.y * World.PPU)) / World.PPU;
            }
            cam.orthographicSize = World.CamHalfHeight * (1f - kick);
            cam.transform.position = new Vector3(offset.x, World.CamCenterY + offset.y, -10f);
        }
    }
}
