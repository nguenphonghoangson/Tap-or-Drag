using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Gravity portals. The spawner tracks the gravity state at its cursor (ahead of the bird) so that a flipped
    /// section always ends with a return portal after <see cref="GameConfig.flippedSectionLength"/> obstacles;
    /// the bird's own gravity flips when it actually crosses a portal.
    /// </summary>
    public partial class GameManager
    {
        const string FlipsKey = "TapOrDrag.GravityFlips";

        readonly List<GravityPortal> portals = new List<GravityPortal>();
        readonly Stack<GravityPortal> portalPool = new Stack<GravityPortal>();
        bool gravityInverted;   // at the bird
        bool spawnInverted;     // at the spawn cursor
        int obstaclesSinceFlip, flipsTotal;

        void InitGravity() => flipsTotal = PlayerPrefs.GetInt(FlipsKey, 0);

        void ResetGravity()
        {
            foreach (var portal in portals) RecyclePortal(portal);
            portals.Clear();
            gravityInverted = spawnInverted = false;
            obstaclesSinceFlip = 0;
            hud.SetGravityInverted(false);
        }

        bool ReturnPortalDue => spawnInverted && obstaclesSinceFlip >= cfg.flippedSectionLength;

        void SpawnPortal(float x)
        {
            var portal = portalPool.Count > 0 ? portalPool.Pop() : CreatePortal();
            portal.gameObject.SetActive(true);
            portal.Setup(x, !spawnInverted);
            portals.Add(portal);
            spawnInverted = !spawnInverted;
            obstaclesSinceFlip = 0;
        }

        /// <summary>Spawner hook: counts real obstacles inside a flipped section.</summary>
        void CountObstacleForFlip()
        {
            if (spawnInverted) obstaclesSinceFlip++;
        }

        void TickPortals(float dt, float birdX, bool allowCross)
        {
            for (int i = portals.Count - 1; i >= 0; i--)
            {
                var portal = portals[i];
                portal.Tick(dt, worldSpeed);
                if (allowCross && portal.CheckCross(birdX)) FlipGravity(portal.Inverts);
                if (portal.X < -World.SpawnX - 1f)
                {
                    RecyclePortal(portal);
                    portals.RemoveAt(i);
                }
            }
        }

        void FlipGravity(bool inverted)
        {
            if (gravityInverted == inverted) return;
            gravityInverted = inverted;
            bird.SetGravityInverted(inverted);
            hud.SetGravityInverted(inverted);

            Vector2 bp = bird.Position;
            fx.Burst(bp, Art.PortalMain, Art.PortalCore, 22, 6f, worldSpeed * 0.5f, false, 0.45f);
            fx.Shake(0.15f, 0.15f);
            hud.Flash(Art.PortalMain, 0.4f);
            sound.Portal(inverted);
            StartCoroutine(SlowMo(cfg.flipSlowMo));

            if (inverted)
            {
                flipsTotal++;
                PlayerPrefs.SetInt(FlipsKey, flipsTotal);
                missions.Add(MissionType.GravityFlips);
                hud.Toast(flipsTotal <= cfg.flipHintUntil ? "TAP PUSHES DOWN" : "GRAVITY FLIP!", Art.PortalMain);
            }
            else hud.Toast("GRAVITY BACK", Art.PortalCore);
        }

        GravityPortal CreatePortal()
        {
            var go = new GameObject("GravityPortal");
            go.transform.SetParent(obstacleRoot, false);
            var portal = go.AddComponent<GravityPortal>();
            portal.Build(art);
            return portal;
        }

        void RecyclePortal(GravityPortal portal)
        {
            portal.gameObject.SetActive(false);
            portalPool.Push(portal);
        }
    }
}
