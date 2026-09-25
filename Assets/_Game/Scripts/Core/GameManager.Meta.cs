using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Meta layer of the run: coins, skin shop, daily missions, biomes, hints, playtest log.</summary>
    public partial class GameManager
    {
        const string RunsKey = "TapOrDrag.Runs";
        const string RedGatesKey = "TapOrDrag.RedGatesPassed";

        readonly List<Coin> coins = new List<Coin>();
        readonly Stack<Coin> coinPool = new Stack<Coin>();
        Transform coinRoot;
        Missions missions;

        int runsPlayed, redGatesPassedTotal, runRedGates, runCoins, runFevers, runPerfects, runCloseCalls, coinChain, biomeIndex;
        float coinChainTimer, runStartTime;
        string lastDeathCause = "";

        void InitMeta()
        {
            coinRoot = new GameObject("Coins").transform;
            coinRoot.SetParent(transform, false);

            Economy.Load(best);
            missions = new Missions();
            missions.Load();
            missions.Completed += OnMissionCompleted;
            InitGravity();
            InitSwitch();
            runsPlayed = PlayerPrefs.GetInt(RunsKey, 0);
            redGatesPassedTotal = PlayerPrefs.GetInt(RedGatesKey, 0);

            hud.BuyRequested += OnBuyRequested;
            hud.SetCoins(Economy.Coins, false);
        }

        /// <summary>Called from EnterReady.</summary>
        void ResetRunMeta()
        {
            foreach (var c in coins) RecycleCoin(c);
            coins.Clear();
            pattern.Clear();
            nextFromPattern = false;
            runCoins = runRedGates = runFevers = runPerfects = runCloseCalls = coinChain = 0;
            lastDeathCause = "";

            missions.Load(); // the day may have rolled over
            RefreshMissionsUi();
            hud.SetCoins(Economy.Coins, false);

            ResetGravity();
            ResetSwitch();
            ResetHazards();
            biomeIndex = 0;
            background.SetBiome(art.Biomes[0], 0.8f);
            ResetShooterMode();
        }

        /// <summary>Called from StartRun.</summary>
        void BeginRunMeta() => runStartTime = Time.time;

        void SaveMeta()
        {
            Economy.Save();
            missions.Save();
            PlayerPrefs.SetInt(RedGatesKey, redGatesPassedTotal);
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- coins

        void SpawnCoin(Vector2 position)
        {
            var coin = coinPool.Count > 0 ? coinPool.Pop() : CreateCoin();
            coin.gameObject.SetActive(true);
            coin.Setup(position);
            coins.Add(coin);
        }

        void SpawnCoinLine(float x, float y, int count, float step)
        {
            for (int i = 0; i < count; i++) SpawnCoin(new Vector2(x + i * step, y));
        }

        /// <summary>A small hump of coins in the open air between two obstacles.</summary>
        void SpawnCoinArc(float fromX, float toX, float y)
        {
            float a = fromX + 1.3f, b = toX - 1.3f;
            if (b - a < 1.5f) return;
            const int count = 4;
            for (int i = 0; i < count; i++)
            {
                float t = i / (count - 1f);
                SpawnCoin(new Vector2(Mathf.Lerp(a, b, t), y + Mathf.Sin(t * Mathf.PI) * 0.8f));
            }
        }

        Coin CreateCoin()
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(coinRoot, false);
            var coin = go.AddComponent<Coin>();
            coin.Build(art);
            return coin;
        }

        void RecycleCoin(Coin coin)
        {
            coin.gameObject.SetActive(false);
            coinPool.Push(coin);
        }

        void TickCoins(float dt, Vector2 bp)
        {
            coinChainTimer -= dt;
            if (coinChainTimer <= 0f) coinChain = 0;
            float pickup = cfg.hitRadius * (fever ? cfg.feverBirdScale : 1f) + cfg.coinPickupRadius;
            float magnet = cfg.coinMagnetRange * cfg.coinMagnetRange;
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                var coin = coins[i];
                coin.Tick(dt, worldSpeed);
                float d2 = (coin.Position - bp).sqrMagnitude;
                if (fever && d2 < magnet) coin.PullTowards(bp, 14f * dt);
                if (d2 < pickup * pickup)
                {
                    CollectCoin(coin);
                    coins.RemoveAt(i);
                }
                else if (coin.Position.x < -World.SpawnX - 1f)
                {
                    RecycleCoin(coin);
                    coins.RemoveAt(i);
                }
            }
        }

        /// <summary>Dead state: coins keep sliding with the slowing world, no pickups.</summary>
        void ScrollCoins(float dt)
        {
            foreach (var coin in coins) coin.Tick(dt, worldSpeed);
        }

        void CollectCoin(Coin coin)
        {
            runCoins++;
            Economy.Add(1);
            coinChain++;
            coinChainTimer = 0.6f;
            fx.CoinPickup(coin.Position);
            sound.Coin(coinChain);
            hud.SetCoins(Economy.Coins, true);
            missions.Add(MissionType.CollectCoins);
            RecycleCoin(coin);
        }

        // ---------------------------------------------------------------- shop

        void OnBuyRequested()
        {
            if (state != GameState.Ready || Economy.Owns(skinIndex)) return;
            if (!Economy.TryBuy(skinIndex))
            {
                hud.BuyFailed();
                sound.Miss();
                return;
            }
            equippedSkin = skinIndex;
            PlayerPrefs.SetInt(SkinKey, equippedSkin);
            PlayerPrefs.Save();
            ApplySkin();
            bird.Pop();
            sound.Purchase();
            hud.SetCoins(Economy.Coins, true);
            hud.Flash(Color.white, 0.5f);
            fx.Burst(bird.Position, Pal.Gold, Pal.Hex(SkinDef.All[skinIndex].Body), 30, 7f, 0f, true, 0.8f);
        }

        // ---------------------------------------------------------------- missions

        void OnMissionCompleted(MissionDef def)
        {
            Economy.Add(def.Reward);
            Economy.Save();
            hud.SetCoins(Economy.Coins, true);
            hud.Toast("MISSION +" + def.Reward, Pal.Hex("7dff9a"));
            sound.Mission();
        }

        void RefreshMissionsUi()
        {
            for (int slot = 0; slot < Missions.Count; slot++)
            {
                var def = missions.Get(slot);
                hud.SetMissionRow(slot, def.Label + " " + missions.ProgressText(slot), def.Reward, missions.Done(slot));
            }
        }

        void AfterScoreChanged()
        {
            missions.Record(MissionType.ScoreInRun, score);
            missions.Record(MissionType.ReachCombo, multiplier);
        }

        // ---------------------------------------------------------------- biomes

        void UpdateBiome()
        {
            int target = clears / Mathf.Max(1, cfg.biomeEveryClears) % art.Biomes.Length;
            if (target == biomeIndex) return;
            biomeIndex = target;
            background.SetBiome(art.Biomes[target], cfg.biomeFadeSeconds);
            hud.Toast(art.Biomes[target].Name, Pal.White);
            sound.Biome();
        }

        // ---------------------------------------------------------------- red / switch gates

        void OnGateFlipped(TrapGate gate)
        {
            sound.SwitchFlip();
            var color = Art.GateMain[gate.Variant];
            fx.Burst(new Vector2(gate.X, Mathf.Clamp(bird.Position.y, World.GroundTop + 1f, World.Top - 1f)), color, Pal.White, 14, 4f, worldSpeed * 0.5f, false, 0.35f);
        }

        void UpdateHint(Vector2 bp)
        {
            if (dashing || fever)
            {
                hud.SetHint(false, "", Pal.White);
                return;
            }
            float r = cfg.hitRadius;
            foreach (var o in obstacles)
            {
                if (!(o is TrapGate t) || t.Cleared || t.Mode != TrapGate.GateMode.Trap || (t.IsSwitch && !t.Flipped)) continue;
                float d = (t.X - t.HalfWidth) - (bp.x + r);
                if (d < -r || d > cfg.dashWindow) continue;
                bool teach = redGatesPassedTotal < cfg.noSwipeHintUntil || runRedGates == 0 || t.IsSwitch;
                hud.SetHint(teach, "NO SWIPE!", Art.GateMain[Art.RedVariant]);
                return;
            }

            var wallAhead = SolidSwitchWallAhead(bp);
            if (wallAhead != null && switchWallsTotal < cfg.switchHintUntil)
            {
                hud.SetHint(true, "SWIPE TO SWITCH", Art.SwitchMain[wallAhead.ColorIndex]);
                return;
            }

            var target = FindDashTarget(bp, false);
            if (target is TrapGate unflipped && unflipped.IsSwitch && !unflipped.Flipped) target = null; // wait for it
            bool tutorial = target is SpikyEnemy ? runStomps == 0
                : target is TrapGate ? true
                : gatesClearedTotal < cfg.swipeHintUntilGates || runGatesCleared == 0;
            hud.SetHint(tutorial && target != null, "SWIPE >>", DashColor(target));
        }

        static int DeathVariant(Obstacle o) => o is DashGate g ? g.Variant : o is TrapGate t ? t.Variant : -1;

        string DeathCause(Obstacle o)
        {
            switch (o)
            {
                case PipePair _: return "PIPE";
                case DashGate _: return "GATE";
                case SpikyEnemy bat when bat.Style == EnemyStyle.Bat: return "BAT";
                case SpikyEnemy _: return "ENEMY";
                case Icicle _: return "ICICLE";
                case LaserSweeper _: return "LASER";
                case Meteor _: return "METEOR";
                case SwitchWall _: return "SWITCH WALL";
                case TrapGate t when t.IsSwitch: return "SWITCH GATE";
                case TrapGate t when dashing && t.Mode == TrapGate.GateMode.Trap: return "RED GATE DASHED";
                case TrapGate _: return "RED GATE";
                default: return "OTHER";
            }
        }

        // ---------------------------------------------------------------- game over

        void TickDeathFlow(bool landed)
        {
            if (landed) ShowGameOverNow();
        }

        void ShowGameOverNow()
        {
            gameOverShown = true;
            retryAt = stateTime + 0.45f;
            runsPlayed++;
            PlayerPrefs.SetInt(RunsKey, runsPlayed);
            hud.ShowGameOver(score, best, newBestThisRun, runCoins);
            if (newBestThisRun) sound.NewBest();
            else sound.GameOver();
            SaveMeta();

            PlaytestStats.Log(new PlaytestStats.Run
            {
                Skin = SkinDef.All[equippedSkin].Name,
                DeathCause = lastDeathCause,
                Duration = Time.time - runStartTime,
                Score = score,
                Clears = clears,
                Fevers = runFevers,
                Perfects = runPerfects,
                CloseCalls = runCloseCalls,
                Coins = runCoins,
            });
        }
    }
}
