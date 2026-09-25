using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Switch walls: every swipe flips the global GOLD/BLUE state, which makes walls of the active colour solid
    /// and the others passable. Dashing through a gate also flips it, so the player has to plan swipes ahead.
    /// </summary>
    public partial class GameManager
    {
        const string SwitchWallsKey = "TapOrDrag.SwitchWalls";

        readonly Stack<SwitchWall> switchPool = new Stack<SwitchWall>();
        int switchState;            // colour index of the walls that are currently solid
        int switchWallsTotal, lastWallColor = -1;
        bool switchShown, lastSpawnWasWall;

        void InitSwitch() => switchWallsTotal = PlayerPrefs.GetInt(SwitchWallsKey, 0);

        void ResetSwitch()
        {
            switchState = 0;
            switchShown = false;
            lastWallColor = -1;
            lastSpawnWasWall = false;
            hud.ShowSwitchState(false, 0);
        }

        void ToggleSwitch()
        {
            switchState ^= 1;
            foreach (var o in obstacles)
                if (o is SwitchWall wall) wall.SetSolid(wall.ColorIndex == switchState);
            if (!switchShown) return;
            hud.ShowSwitchState(true, switchState);
            sound.SwitchToggle(switchState);
        }

        void SpawnSwitchWall(float x, bool scriptedFirst)
        {
            var wall = switchPool.Count > 0 ? switchPool.Pop() : CreateSwitchWall();
            wall.gameObject.SetActive(true);
            // First encounter: solid on arrival so the player has to swipe. Back-to-back walls alternate colours
            // so the second one needs another swipe; otherwise random.
            int color = scriptedFirst ? switchState
                : lastSpawnWasWall && lastWallColor >= 0 ? 1 - lastWallColor
                : Random.Range(0, 2);
            wall.Setup(x, color, color == switchState);
            obstacles.Add(wall);
            lastWallColor = color;
            if (!switchShown)
            {
                switchShown = true;
                hud.ShowSwitchState(true, switchState);
            }
        }

        /// <summary>A solid switch wall close ahead makes a swipe meaningful (no MISS), even without a dash target.</summary>
        SwitchWall SolidSwitchWallAhead(Vector2 bp)
        {
            float r = cfg.hitRadius;
            foreach (var o in obstacles)
            {
                if (!(o is SwitchWall wall) || !wall.Solid) continue;
                float d = (wall.X - wall.HalfWidth) - (bp.x + r);
                if (d >= -r && d <= cfg.dashWindow) return wall;
            }
            return null;
        }

        void OnSwitchWallCleared(SwitchWall wall, int points)
        {
            switchWallsTotal++;
            PlayerPrefs.SetInt(SwitchWallsKey, switchWallsTotal);
            missions.Add(MissionType.SwitchWalls);
            var at = new Vector2(wall.X, Mathf.Clamp(bird.Position.y, World.GroundTop + 1f, World.Top - 1f));
            Color32 color = Art.SwitchMain[wall.ColorIndex];
            fx.Burst(at, color, Art.SwitchLight[wall.ColorIndex], 14, 5f, worldSpeed * 0.8f, false, 0.4f);
            fx.Float("+" + points, color, at + new Vector2(0f, 0.8f), 1.2f, worldSpeed * 0.5f);
            sound.Pass(multiplier);
        }

        SwitchWall CreateSwitchWall()
        {
            var go = new GameObject("SwitchWall");
            go.transform.SetParent(obstacleRoot, false);
            var wall = go.AddComponent<SwitchWall>();
            wall.Build(art);
            return wall;
        }
    }
}
