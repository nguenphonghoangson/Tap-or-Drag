using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Biome hazards. Icicles, bat flocks and laser sweepers are spawned by the obstacle spawner when the biome at the
    /// spawn cursor has them; meteors are a timed event in the space biome (warning marker, then a fast rock).
    /// </summary>
    public partial class GameManager
    {
        readonly Stack<Icicle> iciclePool = new Stack<Icicle>();
        readonly Stack<LaserSweeper> laserPool = new Stack<LaserSweeper>();
        readonly Stack<Meteor> meteorPool = new Stack<Meteor>();

        float meteorTimer, meteorWarnElapsed, meteorLockY;
        bool meteorWarning;

        /// <summary>Biome at the spawn cursor (it runs ~2 obstacles ahead of the bird, which drives the visuals).</summary>
        BiomeHazard SpawnHazard =>
            art.Biomes[Mathf.Max(0, spawned - 2) / Mathf.Max(1, cfg.biomeEveryClears) % art.Biomes.Length].Hazard;

        static ObstacleKind? HazardKind(BiomeHazard hazard)
        {
            switch (hazard)
            {
                case BiomeHazard.Icicles: return ObstacleKind.Icicle;
                case BiomeHazard.Bats: return ObstacleKind.Bats;
                case BiomeHazard.Lasers: return ObstacleKind.Laser;
                default: return null; // meteors are an event, not a spawned obstacle
            }
        }

        void ResetHazards()
        {
            meteorWarning = false;
            meteorTimer = Random.Range(cfg.meteorIntervalMin, cfg.meteorIntervalMax);
            hud.SetMeteorWarning(false, 0f, false);
        }

        // ---------------------------------------------------------------- spawned hazards

        void SpawnIcicle(float x)
        {
            var icicle = iciclePool.Count > 0 ? iciclePool.Pop() : Create<Icicle>(obstacleRoot, "Icicle", i => { i.Build(art); i.Shattered = OnIcicleLanded; });
            icicle.gameObject.SetActive(true);
            icicle.Setup(x, art.Icicles[Random.Range(0, art.Icicles.Length)], cfg.icicleDropDistance);
            obstacles.Add(icicle);
        }

        void SpawnBats(float x)
        {
            float lo = World.GroundTop + 1.5f, hi = World.PlayTop - 1.5f;
            for (int i = 0; i < cfg.batFlockSize; i++)
            {
                var bat = enemyPool.Count > 0 ? enemyPool.Pop() : CreateEnemy();
                bat.gameObject.SetActive(true);
                float y = Mathf.Clamp(lastGapCenter + (i - (cfg.batFlockSize - 1) * 0.5f) * 1.1f + Random.Range(-0.3f, 0.3f), lo, hi);
                bat.Setup(x + i * 0.8f, y, cfg.batExtraSpeed, 0.6f, EnemyStyle.Bat);
                obstacles.Add(bat);
            }
        }

        void SpawnLaser(float x)
        {
            var laser = laserPool.Count > 0 ? laserPool.Pop() : Create<LaserSweeper>(obstacleRoot, "LaserSweeper", l => l.Build(art));
            laser.gameObject.SetActive(true);
            float amplitude = cfg.laserSweepAmplitude;
            float lo = World.GroundTop + 1f + amplitude, hi = World.PlayTop - 1f - amplitude;
            float center = lo < hi ? Mathf.Clamp(lastGapCenter + Random.Range(-1.5f, 1.5f), lo, hi) : (lo + hi) * 0.5f;
            laser.Setup(x, center, amplitude, cfg.laserSweepPeriod);
            obstacles.Add(laser);
        }

        void OnIcicleLanded(Icicle icicle)
        {
            if (state != GameState.Playing) return;
            var at = new Vector2(icicle.X, World.GroundTop + 0.2f);
            fx.Burst(at, Art.IceColor, Pal.White, 16, 5f, worldSpeed * 0.5f, true, 0.5f);
            fx.Shake(0.1f, 0.12f);
            sound.IceShatter();
        }

        // ---------------------------------------------------------------- meteors (space biome event)

        void TickMeteors(float dt, Vector2 bp)
        {
            bool active = art.Biomes[biomeIndex].Hazard == BiomeHazard.Meteors && !fever;
            if (!active)
            {
                if (meteorWarning) hud.SetMeteorWarning(false, 0f, false);
                meteorWarning = false;
                return;
            }

            if (!meteorWarning)
            {
                meteorTimer -= dt;
                if (meteorTimer > 0f) return;
                if (!MeteorLaneClear())
                {
                    meteorTimer = 0.4f; // try again shortly
                    return;
                }
                meteorWarning = true;
                meteorWarnElapsed = 0f;
                sound.MeteorWarn();
            }

            meteorWarnElapsed += dt;
            bool locked = meteorWarnElapsed >= cfg.meteorTrackTime;
            if (!locked) meteorLockY = bp.y;
            hud.SetMeteorWarning(true, meteorLockY, locked);
            if (meteorWarnElapsed < cfg.meteorWarnTime) return;

            meteorWarning = false;
            meteorTimer = Random.Range(cfg.meteorIntervalMin, cfg.meteorIntervalMax);
            hud.SetMeteorWarning(false, 0f, false);
            var meteor = meteorPool.Count > 0 ? meteorPool.Pop() : Create<Meteor>(obstacleRoot, "Meteor", m => m.Build(art));
            meteor.gameObject.SetActive(true);
            meteor.Setup(World.SpawnX, meteorLockY, cfg.meteorSpeed);
            obstacles.Add(meteor);
            sound.MeteorLaunch();
        }

        /// <summary>Do not aim a meteor at the moment the bird is threading a pipe/gate column.</summary>
        bool MeteorLaneClear()
        {
            float warn = cfg.meteorWarnTime;
            float travel = (World.SpawnX - World.BirdX) / (worldSpeed + cfg.meteorSpeed);
            float impactShift = worldSpeed * (warn + travel); // how far obstacles move until impact
            foreach (var o in obstacles)
            {
                if (!(o is PipePair || o is DashGate || o is TrapGate || o is SwitchWall || o is LaserSweeper)) continue;
                float xAtImpact = o.X - impactShift;
                if (Mathf.Abs(xAtImpact - World.BirdX) < o.HalfWidth + 1.2f) return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- helpers

        T Create<T>(Transform parent, string objectName, System.Action<T> build) where T : Component
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var component = go.AddComponent<T>();
            build(component);
            return component;
        }
    }
}
