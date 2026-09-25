using UnityEngine;

namespace TapOrDrag
{
    public enum GameMode { Flappy, Shooter }

    /// <summary>
    /// Mode selection and the DOG BLAST shooter hand-off. The shooter owns its own world (ShooterGame); this file
    /// hides the flappy scenery while it runs, routes rewards into the shared economy/missions, and shows game over.
    /// </summary>
    public partial class GameManager
    {
        const string ModeKey = "TapOrDrag.Mode";
        const string BestShooterKey = "TapOrDrag.BestShooter";

        ShooterGame shooter;
        GameMode mode;
        int bestShooter;
        bool shooterGameOver; // Dead state reached from the shooter: skip the flappy death animation
        SpriteRenderer jetPreview; // title screen stand-in for the dog while DOG BLAST is selected

        void InitShooter()
        {
            shooter = Create<ShooterGame>("Shooter");
            shooter.Build(cfg, art, fx, sound, hud, bird, hud.IsOverButton);
            shooter.Finished += OnShooterFinished;
            shooter.BoneCollected += () =>
            {
                runCoins++;
                Economy.Add(1);
                missions.Add(MissionType.CollectCoins);
                hud.SetCoins(Economy.Coins, true);
            };
            shooter.EnemyKilled += () => missions.Add(MissionType.StompEnemies);
            mode = (GameMode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 1);
            bestShooter = PlayerPrefs.GetInt(BestShooterKey, 0);
            jetPreview = new GameObject("JetPreview").AddComponent<SpriteRenderer>();
            jetPreview.transform.SetParent(transform, false);
            jetPreview.sortingOrder = 10;
            jetPreview.enabled = false;
            hud.ModeStepRequested += StepMode;
            hud.SkillPressed += () => shooter.ActivateSkill();
            hud.BombPressed += () => shooter.UseBomb();
            hud.HangarOpenRequested += () => SetHangar(true);
            hud.HangarCloseRequested += () => SetHangar(false);
            hud.UpgradeBuyRequested += OnUpgradeBuy;
            hud.ItemBuyRequested += OnItemBuy;
            hud.ItemToggleRequested += item =>
            {
                Inventory.SetEnabled(item, !Inventory.IsEnabled(item));
                sound.Click();
                hud.RefreshHangar(Economy.Coins);
            };
        }

        // ---------------------------------------------------------------- hangar

        void SetHangar(bool open)
        {
            if (open && (state != GameState.Ready || mode != GameMode.Shooter)) return;
            hud.ShowHangar(open, Economy.Coins);
            sound.Click();
        }

        void OnUpgradeBuy(UpgradeStat stat)
        {
            if (ShipUpgrades.TryBuy(stat)) HangarPurchased();
            else HangarPurchaseFailed(System.Array.IndexOf(ShipUpgrades.All, stat));
        }

        void OnItemBuy(ItemKind item)
        {
            if (Inventory.TryBuy(item)) HangarPurchased();
            else HangarPurchaseFailed(System.Array.IndexOf(Inventory.All, item));
        }

        void HangarPurchased()
        {
            sound.Purchase();
            hud.SetCoins(Economy.Coins, true);
            hud.RefreshHangar(Economy.Coins);
            ApplySkin(); // skin price affordability may have changed
        }

        void HangarPurchaseFailed(int row)
        {
            sound.Miss();
            hud.HangarBuyFailed(row);
        }

        void StepMode(int direction)
        {
            if (state != GameState.Ready || hud.HangarOpen) return;
            mode = mode == GameMode.Flappy ? GameMode.Shooter : GameMode.Flappy;
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            PlayerPrefs.Save();
            ApplyModeUi();
            bird.Pop();
            sound.Click();
        }

        void ApplyModeUi()
        {
            bool shooterMode = mode == GameMode.Shooter;
            hud.SetMode(shooterMode);
            hud.SetBest(shooterMode ? bestShooter : best, false);
            hud.SetReadyPanel(runsPlayed >= cfg.missionsAfterRuns, shooterMode);
            bird.gameObject.SetActive(!shooterMode);
            ApplySkin(); // skill line switches between flappy and ship skills
        }

        /// <summary>Title screen: the idle dog is shown in its fighter jet when DOG BLAST is selected.</summary>
        void TickJetPreview()
        {
            bool show = state == GameState.Ready && mode == GameMode.Shooter;
            jetPreview.enabled = show;
            if (!show) return;
            jetPreview.sprite = art.Jets[skinIndex][0];
            jetPreview.color = IsSkinUnlocked(skinIndex) ? Color.white : new Color(0.12f, 0.07f, 0.2f);
            jetPreview.transform.position = bird.transform.position;
        }

        /// <summary>Called from EnterReady: leave the shooter world and bring the flappy scenery back.</summary>
        void ResetShooterMode()
        {
            if (shooter.Running) shooter.End();
            shooterGameOver = false;
            background.gameObject.SetActive(true);
            bird.gameObject.SetActive(true);
            ApplyModeUi();
        }

        void StartShooter()
        {
            if (skinIndex != equippedSkin)
            {
                skinIndex = equippedSkin; // a locked skin was only being previewed
                ApplySkin();
            }
            bird.SetShield(false); // flappy skills do not apply in the shooter
            state = GameState.Shooter;
            stateTime = 0f;
            runCoins = 0;
            BeginRunMeta();
            jetPreview.enabled = false;
            background.gameObject.SetActive(false);
            hud.ShowPlaying();
            if (hud.HangarOpen) hud.ShowHangar(false, Economy.Coins);
            shooter.Begin(SkinDef.All[equippedSkin]);
            sound.RunStart();
        }

        void OnShooterFinished(int finalScore, int bonesCollected)
        {
            shooter.End();
            bool newBest = finalScore > bestShooter;
            if (newBest)
            {
                bestShooter = finalScore;
                PlayerPrefs.SetInt(BestShooterKey, bestShooter);
            }
            state = GameState.Dead;
            shooterGameOver = true;
            stateTime = 0f;
            gameOverShown = true;
            retryAt = 0.6f;
            runsPlayed++;
            PlayerPrefs.SetInt(RunsKey, runsPlayed);
            hud.SetBest(bestShooter, newBest);
            hud.ShowGameOver(finalScore, bestShooter, newBest, bonesCollected);
            if (newBest) sound.NewBest();
            else sound.GameOver();
            missions.Record(MissionType.ScoreInRun, finalScore);
            SaveMeta();

            PlaytestStats.Log(new PlaytestStats.Run
            {
                Skin = SkinDef.All[equippedSkin].Name,
                DeathCause = "SHOOTER",
                Duration = Time.time - runStartTime,
                Score = finalScore,
                Coins = bonesCollected,
            });
        }
    }
}
