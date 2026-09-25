using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    public enum GameState { Ready, Playing, Dead }

    enum ObstacleKind { Pipe, Gate, Enemy, RedGate, SwitchGate, Portal, SwitchWall }

    /// <summary>
    /// Owns the run: state machine, obstacle spawning, collisions, dash, score/combo and best score.
    /// Rules: tap = flap through orange pipes; swipe = dash through neon gates. Crashing into anything is fatal.
    /// Spiky enemies are a choice: dodge them with taps or dash through them to stomp them for more points.
    /// Red gates are the reverse: fly through the gap and do NOT dash. Switch gates flip between both rules when close.
    /// A swipe with no gate/enemy in range is a wrong move: combo resets to x1 (or death if GameConfig.wrongSwipeIsFatal).
    /// Meta systems (coins, shop, missions, biomes, playtest log) live in GameManager.Meta.cs.
    /// </summary>
    public partial class GameManager : MonoBehaviour
    {
        const string BestKey = "TapOrDrag.Best";
        const string GatesClearedKey = "TapOrDrag.GatesCleared";
        const string SkinKey = "TapOrDrag.Skin";

        GameConfig cfg;
        Camera cam;
        Art art;
        Bird bird;
        Background background;
        Fx fx;
        Hud hud;
        AudioManager sound;
        InputRouter input;

        readonly List<Obstacle> obstacles = new List<Obstacle>();
        readonly Stack<PipePair> pipePool = new Stack<PipePair>();
        readonly Stack<DashGate> gatePool = new Stack<DashGate>();
        readonly Stack<SpikyEnemy> enemyPool = new Stack<SpikyEnemy>();
        readonly Stack<TrapGate> trapPool = new Stack<TrapGate>();
        readonly Queue<ObstacleKind> pattern = new Queue<ObstacleKind>();
        Transform obstacleRoot;

        GameState state;
        int score, best, streak, multiplier = 1, clears, spawned, specialsInRow, runGatesCleared, gatesClearedTotal, runStomps;
        ObstacleKind nextKind;
        bool newBestThisRun, announcedBest, flapQueued, dashing, gameOverShown;
        float nextSpawnX, lastGapCenter, worldSpeed, stateTime, retryAt;
        float dashTimer, dashBoost, dashCooldown, ghostTimer;
        Obstacle dashTarget;
        int screenW, screenH;
        int skinIndex, equippedSkin;

        // Skill of the bird used for the current run.
        SkillKind skill;
        int shieldCharges, shieldRegenCount;
        float shadowCooldown, trailTimer;
        bool dashPhasing, phasedPipe;

        // Fever / perfect dash.
        bool fever;
        float feverTime, feverGhostTimer;
        int feverReadyAt; // clears count from which the next Fever may start
        Obstacle perfectTarget;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Input.simulateMouseWithTouches = false;
            Screen.orientation = ScreenOrientation.Portrait;

            cfg = Resources.Load<GameConfig>("GameConfig");
            if (cfg == null) cfg = ScriptableObject.CreateInstance<GameConfig>();
            best = PlayerPrefs.GetInt(BestKey, 0);
            gatesClearedTotal = PlayerPrefs.GetInt(GatesClearedKey, 0);

            cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Pal.Hex("140b2e");
            FitCamera();

            art = Art.Build();
            background = Create<Background>("Background");
            background.Build(art);
            obstacleRoot = new GameObject("Obstacles").transform;
            obstacleRoot.SetParent(transform, false);
            bird = Create<Bird>("Bird");
            bird.Build(art, cfg);
            fx = Create<Fx>("Fx");
            fx.Build(art, cam);
            sound = Create<AudioManager>("Audio");
            sound.Build();
            hud = Create<Hud>("HUD");
            hud.Build(art, sound, cam);
            hud.SetBest(best, false);
            hud.SkinStepRequested += StepSkin;
            InitMeta();

            equippedSkin = Mathf.Clamp(PlayerPrefs.GetInt(SkinKey, 0), 0, art.Skins.Length - 1);
            if (!IsSkinUnlocked(equippedSkin)) equippedSkin = 0;
            skinIndex = equippedSkin;
            ApplySkin();

            input = new InputRouter(cfg, hud.IsOverButton);
            input.Tap += OnTap;
            input.Swipe += OnSwipe;

            EnterReady();
        }

        void OnDisable() => Time.timeScale = 1f;

        void OnApplicationPause(bool paused)
        {
            if (paused) PlayerPrefs.Save();
        }

        T Create<T>(string objectName) where T : Component
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void FitCamera()
        {
            World.Fit(cam);
            cam.transform.position = new Vector3(0f, World.CamCenterY, -10f);
            screenW = Screen.width;
            screenH = Screen.height;
        }

        // ---------------------------------------------------------------- loop

        void Update()
        {
            if (Screen.width != screenW || Screen.height != screenH)
            {
                FitCamera();
                background.Refit();
            }

            float dt = Time.deltaTime;
            if (state == GameState.Ready)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) StepSkin(-1);
                if (Input.GetKeyDown(KeyCode.RightArrow)) StepSkin(1);
            }
            input.Tick();
            stateTime += dt;
            switch (state)
            {
                case GameState.Ready: TickReady(dt); break;
                case GameState.Playing: TickPlaying(dt); break;
                case GameState.Dead: TickDead(dt); break;
            }
            fx.Tick(dt, worldSpeed);
            hud.Tick(Time.unscaledDeltaTime);
        }

        void TickReady(float dt)
        {
            worldSpeed = cfg.startSpeed * 0.5f;
            background.Tick(dt, worldSpeed * dt);
            bird.TickIdle(dt);
        }

        void TickPlaying(float dt)
        {
            if (dashCooldown > 0f) dashCooldown -= dt;
            if (!dashing) dashBoost = Mathf.MoveTowards(dashBoost, 0f, cfg.dashBoostDecay * dt);

            float baseSpeed = Mathf.Min(cfg.maxSpeed, cfg.startSpeed + clears * cfg.speedPerClear);
            worldSpeed = baseSpeed * (fever ? cfg.feverSpeedScale : 1f) + dashBoost;
            TickFever(dt);
            float dx = worldSpeed * dt;

            foreach (var o in obstacles) o.Tick(dt, worldSpeed);
            nextSpawnX -= dx;
            background.Tick(dt, dx);
            bird.TickPlay(dt);
            TickPortals(dt, bird.Position.x, true);

            Vector2 bp = bird.Position;
            float r = cfg.hitRadius;
            TickSkill(dt, bp);

            if (dashing)
            {
                dashTimer += dt;
                if (dashPhasing && !phasedPipe)
                    foreach (var o in obstacles)
                        if (o is PipePair && o.Hits(bp, r)) phasedPipe = true;
                ghostTimer -= dt;
                if (ghostTimer <= 0f)
                {
                    ghostTimer = 0.035f;
                    fx.Afterimage(bird, DashColor(dashTarget));
                }
                // Locked onto an enemy: steer vertically into it.
                if (dashTarget is SpikyEnemy target && !target.Defeated)
                {
                    bird.HomeTowards(target.Position.y, 14f * dt);
                    bp = bird.Position;
                }

                // While dashing, any gate the bird reaches shatters and any enemy it touches is stomped.
                for (int i = 0; i < obstacles.Count; i++)
                {
                    if (obstacles[i] is DashGate g && !g.Broken && g.X - g.HalfWidth < bp.x + r)
                    {
                        g.Break();
                        OnCleared(g);
                    }
                    else if (obstacles[i] is TrapGate tg && !tg.Broken && tg.Mode == TrapGate.GateMode.Dash && tg.X - tg.HalfWidth < bp.x + r)
                    {
                        tg.Break();
                        OnCleared(tg);
                    }
                    else if (obstacles[i] is SpikyEnemy e && !e.Defeated)
                    {
                        float reach = r + SpikyEnemy.Radius + 0.15f;
                        if ((e.Position - bp).sqrMagnitude < reach * reach)
                        {
                            e.Defeat();
                            OnCleared(e);
                        }
                    }
                }

                bool passed = dashTarget == null || dashTarget.Cleared || dashTarget.X + dashTarget.HalfWidth < bp.x - r - 0.15f;
                if ((passed && dashTimer >= cfg.dashMinDuration) || dashTimer >= cfg.dashMaxDuration) EndDash();
            }

            bool hitsGround = bp.y - r <= World.GroundTop, hitsCeiling = bp.y + r >= World.Top;
            if (hitsGround || hitsCeiling)
            {
                if (!fever && !bird.Invulnerable && !TryShield(null))
                {
                    Die(-1, hitsGround ? "GROUND" : "CEILING");
                    return;
                }
                if (hitsGround) bird.Bounce(World.GroundTop + r + 0.02f, cfg.flapVelocity);
                else bird.Bounce(World.Top - r - 0.02f, Mathf.Min(bird.Vy, -2f));
                bp = bird.Position;
            }
            if (fever) FeverSmash(bp, r);
            else if (!bird.Invulnerable)
                foreach (var o in obstacles)
                {
                    if (dashPhasing && o is PipePair) continue; // NINJA shadow dash
                    // Dashing into a red gate is fatal anywhere in its column, gap included.
                    bool lethal = o is TrapGate trap && dashing && trap.Mode == TrapGate.GateMode.Trap ? trap.TouchesColumn(bp, r) : o.Hits(bp, r);
                    if (!lethal) continue;
                    if (TryShield(o)) break;
                    Die(DeathVariant(o), DeathCause(o));
                    return;
                }

            // Close-call tracking only counts clean flying (no fever / shield grace / shadow phasing).
            if (!fever && !bird.Invulnerable && !dashPhasing)
                foreach (var o in obstacles)
                    if (o is PipePair p && !p.Cleared) p.TrackClearance(bp, r);

            foreach (var o in obstacles)
                if (!o.Cleared && !(o is DashGate) && o.X + o.HalfWidth < bp.x - r)
                {
                    o.Cleared = true;
                    OnCleared(o); // pipe passed, or enemy dodged
                }

            TickCoins(dt, bp);

            while (nextSpawnX < World.SpawnX) SpawnNext();
            for (int i = obstacles.Count - 1; i >= 0; i--)
                if (obstacles[i].X < -World.SpawnX - 1f)
                {
                    Recycle(obstacles[i]);
                    obstacles.RemoveAt(i);
                }

            UpdateHint(bp);
        }

        void TickDead(float dt)
        {
            worldSpeed = Mathf.MoveTowards(worldSpeed, 0f, 25f * dt);
            foreach (var o in obstacles) o.Tick(dt, worldSpeed);
            ScrollCoins(dt);
            TickPortals(dt, bird.Position.x, false);
            background.Tick(dt, worldSpeed * dt);

            bool wasGrounded = bird.Grounded;
            bird.TickDead(dt);
            if (!wasGrounded && bird.Grounded)
            {
                fx.Dust(bird.Position);
                fx.Shake(0.15f, 0.15f);
                sound.Thud();
            }

            if (!gameOverShown) TickDeathFlow((bird.Grounded && stateTime > 0.55f) || stateTime > 1.6f);
        }

        // ---------------------------------------------------------------- input

        void OnTap()
        {
            switch (state)
            {
                case GameState.Ready:
                    StartRun();
                    break;
                case GameState.Playing:
                    if (dashing) flapQueued = true;
                    else DoFlap();
                    break;
                case GameState.Dead:
                    if (gameOverShown && stateTime >= retryAt)
                    {
                        sound.Click();
                        EnterReady();
                    }
                    break;
            }
        }

        void OnSwipe()
        {
            if (state != GameState.Playing || stateTime < 0.25f) return;
            if (fever) return; // Fever already smashes everything; dashing would only turn it into a speed exploit
            if (dashing || dashCooldown > 0f) return;

            Vector2 bp = bird.Position;
            dashPhasing = skill == SkillKind.ShadowDash && shadowCooldown <= 0f;
            phasedPipe = false;
            bird.SetPhasing(dashPhasing);
            bool switchesWall = SolidSwitchWallAhead(bp) != null; // checked before the toggle below
            dashTarget = FindDashTarget(bp, dashPhasing);
            perfectTarget = null;
            if (dashTarget != null && (dashTarget.X - dashTarget.HalfWidth) - (bp.x + cfg.hitRadius) <= cfg.perfectDashDistance)
                perfectTarget = dashTarget; // paid out when this target is cleared
            dashing = true;
            dashTimer = 0f;
            ghostTimer = 0f;
            dashBoost = cfg.dashBoostSpeed;
            bird.BeginDash();
            ToggleSwitch();
            sound.Dash();
            fx.Kick(0.035f);
            fx.DashStart(bp, DashColor(dashTarget));

            if (dashTarget == null && !fever && !switchesWall)
            {
                Miss();
                if (cfg.wrongSwipeIsFatal) Die(-1, "WRONG SWIPE");
            }
        }

        /// <summary>
        /// Nearest gate or enemy a swipe would count for (enemies also need to be within lock-on height).
        /// Pipes count too while the NINJA shadow dash is ready.
        /// </summary>
        Obstacle FindDashTarget(Vector2 bp, bool includePipes)
        {
            Obstacle target = null;
            float bestDistance = float.MaxValue, r = cfg.hitRadius;
            foreach (var o in obstacles)
            {
                if (o is DashGate g && g.Broken) continue;
                if (o is TrapGate tg && (tg.Broken || tg.Mode == TrapGate.GateMode.Trap)) continue;
                if (o is SpikyEnemy e && (e.Defeated || Mathf.Abs(e.Position.y - bp.y) > cfg.enemyLockRange)) continue;
                if (o is PipePair && (!includePipes || o.Cleared)) continue;
                float d = (o.X - o.HalfWidth) - (bp.x + r);
                if (d >= -r && d <= cfg.dashWindow && d < bestDistance)
                {
                    bestDistance = d;
                    target = o;
                }
            }
            return target;
        }

        static Color DashColor(Obstacle target)
        {
            Color c = target is DashGate g ? (Color)Art.GateMain[g.Variant]
                : target is TrapGate tg ? (Color)Art.GateMain[tg.Variant]
                : target is SpikyEnemy ? (Color)Art.SpikyColor
                : new Color(0.85f, 0.8f, 1f);
            c.a = 0.7f;
            return c;
        }

        void DoFlap()
        {
            bird.Flap();
            sound.Flap();
            fx.FlapPuff(bird.Position);
        }

        void EndDash()
        {
            dashing = false;
            dashTarget = null;
            dashCooldown = cfg.dashCooldown;
            bird.EndDash();
            if (dashPhasing)
            {
                bird.SetPhasing(false);
                if (phasedPipe) shadowCooldown = cfg.shadowCooldown;
                dashPhasing = phasedPipe = false;
            }
            if (flapQueued)
            {
                flapQueued = false;
                DoFlap();
            }
        }

        // ---------------------------------------------------------------- flow

        void EnterReady()
        {
            state = GameState.Ready;
            stateTime = 0f;
            foreach (var o in obstacles) Recycle(o);
            obstacles.Clear();

            score = streak = clears = spawned = specialsInRow = runGatesCleared = runStomps = 0;
            multiplier = 1;
            newBestThisRun = announcedBest = flapQueued = dashing = gameOverShown = false;
            dashBoost = dashCooldown = 0f;
            dashTarget = null;

            EndFever(true);
            feverReadyAt = 0;
            perfectTarget = null;
            bird.ResetAt(ReadyBirdY);
            ApplySkin();
            ResetRunMeta();
            hud.SetSelectorAnchor(new Vector3(World.BirdX, ReadyBirdY, 0f));
            hud.ShowReady();
            hud.SetScore(0, 1, false);
            sound.DuckMusic(false);
            sound.EnsureMusic();
        }

        void StartRun()
        {
            if (skinIndex != equippedSkin)
            {
                skinIndex = equippedSkin; // a locked skin was only being previewed
                ApplySkin();
            }
            SetupSkill();
            state = GameState.Playing;
            stateTime = 0f;
            nextSpawnX = World.BirdX + cfg.firstObstacleDistance;
            nextKind = ObstacleKind.Pipe;
            lastGapCenter = bird.Position.y;
            hud.ShowPlaying();
            BeginRunMeta();
            sound.RunStart();
            DoFlap();
        }

        void Die(int gateVariant, string cause)
        {
            if (state != GameState.Playing) return;
            lastDeathCause = gravityInverted ? cause + " FLIPPED" : cause;
            state = GameState.Dead;
            stateTime = 0f;
            gameOverShown = false;
            dashing = false;
            dashTarget = null;
            dashBoost = 0f;
            flapQueued = false;
            dashPhasing = phasedPipe = false;
            bird.SetPhasing(false);
            EndFever(true);

            Vector2 bp = bird.Position;
            bool onGround = bp.y - cfg.hitRadius <= World.GroundTop;
            bool onCeiling = bp.y + cfg.hitRadius >= World.Top;
            bird.Kill(onGround ? 0f : onCeiling ? -2f : 6f);

            fx.Death(bp, gateVariant);
            fx.Shake(0.5f, 0.45f);
            hud.Flash(gateVariant >= 0 ? (Color)Art.GateMain[gateVariant] : Color.white);
            hud.SetGravityInverted(false);
            hud.SetHint(false, "", Pal.White);
            sound.Hit();
            if (gateVariant >= 0) sound.Zap();
            else if (!onGround) StartCoroutine(Delayed(0.3f, sound.Fall));
            sound.DuckMusic(true);

            if (newBestThisRun) PlayerPrefs.SetInt(BestKey, best);
            PlayerPrefs.SetInt(GatesClearedKey, gatesClearedTotal);
            SaveMeta();

            StartCoroutine(HitStop(0.09f));
        }

        IEnumerator HitStop(float seconds)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
        }

        IEnumerator Delayed(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            if (state == GameState.Dead) action();
        }

        // ---------------------------------------------------------------- scoring

        void OnCleared(Obstacle o)
        {
            clears++;
            streak++;
            int newMultiplier = Mathf.Clamp(1 + streak / ComboStep, 1, MaxMultiplier);
            bool comboUp = newMultiplier > multiplier;
            multiplier = newMultiplier;
            int basePoints = o is DashGate ? cfg.gatePoints
                : o is SwitchWall ? cfg.switchWallPoints
                : o is TrapGate scoredTrap ? (scoredTrap.Broken ? cfg.gatePoints : cfg.redGatePoints) + (scoredTrap.IsSwitch ? cfg.switchGateBonus : 0)
                : o is SpikyEnemy enemy ? (enemy.Defeated ? cfg.enemyStompPoints : cfg.enemyDodgePoints)
                : cfg.pipePoints;
            int points = Scaled(basePoints);
            score += points;

            if (skill == SkillKind.Shield && shieldCharges == 0 && ++shieldRegenCount >= cfg.shieldRegenClears)
            {
                shieldCharges = 1;
                bird.SetShield(true);
                hud.Toast("SHIELD READY", SkillColor());
                sound.SkillReady();
            }

            if (o is SpikyEnemy spiky)
            {
                var at = spiky.Position;
                if (spiky.Defeated)
                {
                    runStomps++;
                    missions.Add(MissionType.StompEnemies);
                    fx.EnemyStomp(at, worldSpeed);
                    fx.Float("+" + points, Art.SpikyColor, at + new Vector2(0.4f, 0.9f), 1.4f, worldSpeed * 0.4f);
                    fx.Shake(0.18f, 0.15f);
                    fx.Kick(0.03f);
                    sound.Stomp();
                }
                else
                {
                    fx.Float("+" + points, Pal.Lilac, at + new Vector2(0f, 0.7f), 1f, worldSpeed * 0.5f);
                    sound.Pass(multiplier);
                }
            }
            else if (o is DashGate gate)
            {
                runGatesCleared++;
                gatesClearedTotal++;
                missions.Add(MissionType.BreakGates);
                fx.GateBreak(gate.X, gate.Variant, worldSpeed);
                fx.Float("+" + points, Art.GateMain[gate.Variant], new Vector2(gate.X + 0.6f, bird.Position.y + 0.9f), 1.2f, worldSpeed * 0.4f);
                sound.GateBreak();
            }
            else if (o is SwitchWall clearedWall) OnSwitchWallCleared(clearedWall, points);
            else if (o is TrapGate trapped)
            {
                if (trapped.Broken)
                {
                    runGatesCleared++;
                    gatesClearedTotal++;
                    missions.Add(MissionType.BreakGates);
                    fx.GateBreak(trapped.X, trapped.Variant, worldSpeed);
                    fx.Float("+" + points, Art.GateMain[trapped.Variant], new Vector2(trapped.X + 0.6f, bird.Position.y + 0.9f), 1.2f, worldSpeed * 0.4f);
                    sound.GateBreak();
                }
                else
                {
                    runRedGates++;
                    redGatesPassedTotal++;
                    missions.Add(MissionType.PassRedGates);
                    var at = new Vector2(trapped.X, trapped.GapCenter);
                    Color32 red = Art.GateMain[Art.RedVariant];
                    fx.Burst(at, red, Pal.White, 16, 5f, worldSpeed * 0.8f, false, 0.45f);
                    fx.Float("+" + points, red, at + new Vector2(0f, 0.3f), 1.2f, worldSpeed * 0.5f);
                    sound.Pass(multiplier);
                }
            }
            else
            {
                var pipe = (PipePair)o;
                missions.Add(MissionType.PassPipes);
                var at = new Vector2(pipe.X, pipe.GapCenter);
                fx.PipePass(at, worldSpeed);
                fx.Float("+" + points, Pal.OrangeLight, at + new Vector2(0f, 0.3f), 1.2f, worldSpeed * 0.5f);
                sound.Pass(multiplier);
            }

            if (o == perfectTarget)
            {
                perfectTarget = null;
                bool dodgedNotStomped = o is SpikyEnemy target && !target.Defeated;
                if (!dodgedNotStomped) AwardPerfect();
            }
            if (o is PipePair passed && !passed.Smashed && passed.Closest >= 0f && passed.Closest < cfg.closeCallDistance)
                AwardCloseCall(passed);

            if (comboUp)
            {
                sound.ComboUp(multiplier);
                fx.Kick(0.02f);
            }
            hud.SetScore(score, multiplier, comboUp);

            if (!fever && multiplier >= cfg.feverAtMultiplier && clears >= feverReadyAt) StartFever();

            if (score > best)
            {
                if (best > 0 && !announcedBest)
                {
                    announcedBest = true;
                    hud.Toast("NEW BEST!", Pal.Gold);
                    sound.NewBest();
                }
                best = score;
                newBestThisRun = true;
                hud.SetBest(best, true);
            }
            AfterScoreChanged();
            UpdateBiome();
        }

        void Miss()
        {
            if (fever) return; // no penalties during Fever
            if (skill == SkillKind.RoyalCombo && multiplier > 2)
            {
                // FLAMINGO soft miss: combo is halved instead of reset.
                multiplier = Mathf.Max(1, multiplier / 2);
                streak = (multiplier - 1) * ComboStep;
            }
            else
            {
                streak = 0;
                multiplier = 1;
                hud.ComboBreak();
            }
            hud.SetScore(score, multiplier, false);
            fx.Float("MISS", Pal.Red, bird.Position + new Vector2(0f, 1f), 1.3f);
            fx.Shake(0.12f, 0.15f);
            sound.Miss();
        }

        // ---------------------------------------------------------------- fever / perfect / close

        /// <summary>Base points → awarded points: combo multiplier, GOLD skill, Fever bonus.</summary>
        int Scaled(int basePoints)
        {
            int points = basePoints * multiplier;
            if (skill == SkillKind.Midas) points *= Mathf.Max(1, cfg.midasMultiplier);
            if (fever) points *= Mathf.Max(1, cfg.feverPointMultiplier);
            return points;
        }

        void StartFever()
        {
            fever = true;
            runFevers++;
            missions.Add(MissionType.Fevers);
            feverTime = cfg.feverDuration;
            feverGhostTimer = 0f;
            bird.SetFeverScale(cfg.feverBirdScale);
            hud.SetFever(true);
            hud.SetFeverProgress(1f);
            hud.Flash(Color.white, 0.6f);
            fx.Kick(0.05f);
            fx.Shake(0.2f, 0.25f);
            sound.FeverStart();
            sound.SetMusicPitch(1.15f);
        }

        void EndFever(bool silent)
        {
            if (!fever) return;
            fever = false;
            bird.SetFeverScale(1f);
            hud.SetFever(false);
            sound.SetMusicPitch(1f);
            feverReadyAt = clears + cfg.feverRechargeClears; // misses and shield hits cannot shorten this
            if (silent) return;
            bird.StartInvulnerable(cfg.feverGrace);
            sound.FeverEnd();
        }

        void TickFever(float dt)
        {
            if (!fever) return;
            feverTime -= dt;
            hud.SetFeverProgress(feverTime / cfg.feverDuration);
            feverGhostTimer -= dt;
            if (feverGhostTimer <= 0f)
            {
                feverGhostTimer = 0.04f;
                var rainbow = Color.HSVToRGB(Time.time * 2f % 1f, 0.7f, 1f);
                rainbow.a = 0.6f;
                fx.Afterimage(bird, rainbow);
            }
            if (feverTime <= 0f) EndFever(false);
        }

        /// <summary>Fever: anything the (bigger) bird touches is smashed and scored.</summary>
        void FeverSmash(Vector2 bp, float r)
        {
            float reach = r * cfg.feverBirdScale;
            foreach (var o in obstacles)
            {
                if (o.Cleared || !o.Hits(bp, reach)) continue;
                if (o is PipePair pipe)
                {
                    pipe.Smash();
                    fx.PipeSmash(pipe, worldSpeed);
                }
                else if (o is DashGate gate) gate.Break();
                else if (o is TrapGate trap) trap.Break();
                else if (o is SwitchWall wall) wall.Break();
                else if (o is SpikyEnemy enemy) enemy.Defeat();
                OnCleared(o);
                sound.Smash();
                fx.Shake(0.15f, 0.12f);
            }
        }

        void AwardPerfect()
        {
            runPerfects++;
            missions.Add(MissionType.Perfects);
            int points = Scaled(cfg.perfectBonus);
            score += points;
            Vector2 bp = bird.Position;
            fx.PerfectRing(bp, Pal.Gold);
            fx.Float("PERFECT!", Pal.Gold, bp + new Vector2(0f, 1.4f), 1.4f);
            fx.Float("+" + points, Pal.Gold, bp + new Vector2(0.9f, 0.6f), 1f, worldSpeed * 0.3f);
            sound.Perfect();
            StartCoroutine(SlowMo(cfg.perfectSlowMo));
        }

        void AwardCloseCall(PipePair pipe)
        {
            runCloseCalls++;
            missions.Add(MissionType.CloseCalls);
            int points = Scaled(cfg.closeBonus);
            score += points;
            Vector2 bp = bird.Position;
            fx.Float("CLOSE!", Pal.Lilac, bp + new Vector2(0f, 1.1f), 1.1f, worldSpeed * 0.3f);
            fx.Burst(bp, Pal.White, Pal.Lilac, 8, 3f, worldSpeed * 0.5f, false, 0.3f);
            sound.CloseCall();
        }

        IEnumerator SlowMo(float realSeconds)
        {
            Time.timeScale = 0.35f;
            yield return new WaitForSecondsRealtime(realSeconds);
            if (Time.timeScale > 0f) Time.timeScale = 1f; // don't cut a death hit-stop short
        }

        // ---------------------------------------------------------------- skills

        int ComboStep => Mathf.Max(1, skill == SkillKind.RoyalCombo ? cfg.comboStep / 2 : cfg.comboStep);
        int MaxMultiplier => Mathf.Max(1, cfg.maxMultiplier + (skill == SkillKind.RoyalCombo ? cfg.royalComboBonusMax : 0));

        Color32 SkillColor() => Pal.Hex(SkinDef.All[equippedSkin].SkillColor);

        void SetupSkill()
        {
            skill = SkinDef.All[equippedSkin].Skill;
            shieldCharges = skill == SkillKind.Shield ? 1 : 0;
            shieldRegenCount = 0;
            shadowCooldown = 0f;
            trailTimer = 0f;
            dashPhasing = phasedPipe = false;
            bird.SetShield(shieldCharges > 0);
            bool antiGrav = skill == SkillKind.AntiGrav;
            bird.GravityScale = antiGrav ? cfg.antiGravGravity : 1f;
            bird.FlapScale = antiGrav ? cfg.antiGravFlap : 1f;
            bird.FallScale = antiGrav ? cfg.antiGravFall : 1f;
        }

        void TickSkill(float dt, Vector2 bp)
        {
            if (skill == SkillKind.ShadowDash && shadowCooldown > 0f)
            {
                shadowCooldown -= dt;
                if (shadowCooldown <= 0f)
                {
                    fx.Float("SHADOW READY", SkillColor(), bp + new Vector2(0f, 1f), 0.9f);
                    sound.SkillReady();
                }
            }

            trailTimer -= dt;
            if (trailTimer > 0f) return;
            if (skill == SkillKind.Midas)
            {
                trailTimer = 0.05f;
                fx.Trail(bp + new Vector2(-0.3f, 0f), Random.value < 0.5f ? Pal.Gold : Pal.White, worldSpeed);
            }
            else if (skill == SkillKind.ShadowDash && shadowCooldown <= 0f)
            {
                trailTimer = 0.12f;
                fx.Trail(bp + new Vector2(-0.3f, 0f), SkillColor(), worldSpeed);
            }
        }

        /// <summary>BLUEJAY: absorb one crash. The obstacle is neutralised without points and the combo resets.</summary>
        bool TryShield(Obstacle hit)
        {
            if (shieldCharges <= 0) return false;
            shieldCharges--;
            shieldRegenCount = 0;
            bird.SetShield(false);
            bird.StartInvulnerable(cfg.shieldInvulnerable);

            if (hit is DashGate gate) gate.Break();
            else if (hit is TrapGate trap) trap.Break();
            else if (hit is SwitchWall wall) wall.Break();
            else if (hit is SpikyEnemy enemy) enemy.Defeat();
            else if (hit != null) hit.Cleared = true;

            streak = 0;
            multiplier = 1;
            hud.ComboBreak();
            hud.SetScore(score, 1, false);

            Vector2 bp = bird.Position;
            fx.ShieldPop(bp);
            fx.Shake(0.25f, 0.2f);
            fx.Float("SAVED!", SkillColor(), bp + new Vector2(0f, 1f), 1.2f);
            hud.Flash(SkillColor(), 0.45f);
            sound.ShieldPop();
            return true;
        }

        // ---------------------------------------------------------------- skins

        const float ReadyBirdY = World.GroundTop + 7f;

        bool IsSkinUnlocked(int index) => cfg.unlockAllSkins || Economy.Owns(index);

        void ApplySkin()
        {
            var def = SkinDef.All[skinIndex];
            bool locked = !IsSkinUnlocked(skinIndex);
            bird.ApplySkin(art.Skins[skinIndex], locked);
            bird.SetShield(def.Skill == SkillKind.Shield && !locked); // preview the bubble on the title screen
            hud.SetSkin(def.Name, !locked, def.Price, Economy.Coins >= def.Price, skinIndex, art.Skins.Length, def.SkillText, Pal.Hex(def.SkillColor));
        }

        /// <summary>Title-screen skin browsing. Unlocked skins are equipped (and saved) immediately; locked ones are only previewed.</summary>
        void StepSkin(int direction)
        {
            if (state != GameState.Ready) return;
            int count = art.Skins.Length;
            skinIndex = ((skinIndex + direction) % count + count) % count;
            if (IsSkinUnlocked(skinIndex))
            {
                equippedSkin = skinIndex;
                PlayerPrefs.SetInt(SkinKey, equippedSkin);
                PlayerPrefs.Save();
            }
            ApplySkin();
            bird.Pop();
            sound.Click();
            var def = SkinDef.All[skinIndex];
            if (IsSkinUnlocked(skinIndex))
                fx.Burst(bird.Position, Pal.Hex(def.Body), Pal.White, 10, 4f, 0f, false, 0.35f);
        }

        // ---------------------------------------------------------------- spawning

        static readonly ObstacleKind[][] Patterns =
        {
            new[] { ObstacleKind.Pipe, ObstacleKind.Gate, ObstacleKind.Pipe },
            new[] { ObstacleKind.Gate, ObstacleKind.RedGate },
            new[] { ObstacleKind.RedGate, ObstacleKind.Gate },
            new[] { ObstacleKind.Pipe, ObstacleKind.Enemy, ObstacleKind.Gate },
            new[] { ObstacleKind.SwitchGate, ObstacleKind.Pipe },
            new[] { ObstacleKind.Gate, ObstacleKind.Pipe, ObstacleKind.RedGate },
            new[] { ObstacleKind.SwitchWall, ObstacleKind.SwitchWall },
            new[] { ObstacleKind.Gate, ObstacleKind.SwitchWall },
            new[] { ObstacleKind.SwitchWall, ObstacleKind.Pipe, ObstacleKind.SwitchWall },
        };

        bool nextFromPattern;

        void SpawnNext()
        {
            var kind = nextKind;
            bool thisFromPattern = nextFromPattern;
            float x = nextSpawnX;
            switch (kind)
            {
                case ObstacleKind.Portal:
                    SpawnPortal(x);
                    break;
                case ObstacleKind.SwitchWall:
                    SpawnSwitchWall(x, spawned == cfg.firstSwitchWallAt);
                    specialsInRow++;
                    break;
                case ObstacleKind.Gate:
                {
                    var gate = gatePool.Count > 0 ? gatePool.Pop() : CreateGate();
                    gate.gameObject.SetActive(true);
                    gate.Setup(x, Random.Range(0, 2));
                    obstacles.Add(gate);
                    specialsInRow++;
                    if (Random.value < cfg.coinArcChance) SpawnCoinLine(x + 0.8f, lastGapCenter, 3, 0.6f); // reward for dashing through
                    break;
                }
                case ObstacleKind.Enemy:
                {
                    var enemy = enemyPool.Count > 0 ? enemyPool.Pop() : CreateEnemy();
                    enemy.gameObject.SetActive(true);
                    float lo = World.GroundTop + 1.5f + cfg.enemyBobAmplitude;
                    float hi = World.PlayTop - 1.5f - cfg.enemyBobAmplitude;
                    float y = Mathf.Clamp(lastGapCenter + Random.Range(-1.5f, 1.5f), lo, hi);
                    enemy.Setup(x, y, cfg.enemyExtraSpeed, cfg.enemyBobAmplitude);
                    obstacles.Add(enemy);
                    specialsInRow++;
                    break;
                }
                case ObstacleKind.RedGate:
                case ObstacleKind.SwitchGate:
                {
                    var trap = trapPool.Count > 0 ? trapPool.Pop() : CreateTrap();
                    trap.gameObject.SetActive(true);
                    bool isSwitch = kind == ObstacleKind.SwitchGate;
                    float center = NextGapCenter(cfg.redGateGap, 0f, cfg.maxGapDelta * 0.6f);
                    var start = isSwitch && Random.value < 0.5f ? TrapGate.GateMode.Dash : TrapGate.GateMode.Trap;
                    trap.Setup(x, center, cfg.redGateGap, start, isSwitch, cfg.switchFlipDistance);
                    obstacles.Add(trap);
                    specialsInRow++;
                    if (!isSwitch && Random.value < cfg.coinInGapChance) SpawnCoin(new Vector2(x, center));
                    break;
                }
                default:
                {
                    var pipe = pipePool.Count > 0 ? pipePool.Pop() : CreatePipe();
                    pipe.gameObject.SetActive(true);
                    float gap = Mathf.Max(cfg.minGap, cfg.startGap - spawned * cfg.gapShrinkPerSpawn);
                    float amplitude = spawned >= cfg.movingPipesAfter && Random.value < cfg.movingPipeChance ? cfg.movingPipeAmplitude : 0f;
                    float center = NextGapCenter(gap, amplitude, cfg.maxGapDelta);
                    pipe.Setup(x, center, gap, amplitude);
                    obstacles.Add(pipe);
                    specialsInRow = 0;
                    if (amplitude <= 0f && Random.value < cfg.coinInGapChance) SpawnCoin(new Vector2(x, center));
                    break;
                }
            }

            if (kind != ObstacleKind.Portal)
            {
                CountObstacleForFlip();
                lastSpawnWasWall = kind == ObstacleKind.SwitchWall;
            }
            spawned++;
            nextKind = ChooseKind();
            float spacing = Spacing(kind, nextKind);
            if (thisFromPattern && nextFromPattern) spacing *= cfg.patternSpacingScale;
            if (kind == ObstacleKind.Pipe && Random.value < cfg.coinArcChance) SpawnCoinArc(x, x + spacing, lastGapCenter);
            nextSpawnX += spacing;
        }

        /// <summary>Picks the next gap centre within reach of the previous one and within the play area.</summary>
        float NextGapCenter(float gap, float amplitude, float maxDelta)
        {
            float lo = World.GroundTop + 1.2f + gap * 0.5f + amplitude;
            float hi = World.PlayTop - 1.2f - gap * 0.5f - amplitude;
            float a = Mathf.Max(lo, lastGapCenter - maxDelta);
            float b = Mathf.Min(hi, lastGapCenter + maxDelta);
            if (a > b) a = b = Mathf.Clamp(lastGapCenter, Mathf.Min(lo, hi), Mathf.Max(lo, hi));
            lastGapCenter = Random.Range(a, b);
            return lastGapCenter;
        }

        bool KindAvailable(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Gate: return spawned > cfg.firstGateAt;
                case ObstacleKind.Enemy: return spawned > cfg.firstEnemyAt;
                case ObstacleKind.RedGate: return spawned > cfg.firstRedGateAt;
                case ObstacleKind.SwitchGate: return spawned > cfg.firstSwitchGateAt;
                case ObstacleKind.Portal: return spawned > cfg.firstPortalAt;
                case ObstacleKind.SwitchWall: return spawned > cfg.firstSwitchWallAt;
                default: return true;
            }
        }

        ObstacleKind ChooseKind()
        {
            nextFromPattern = false;
            if (pattern.Count > 0)
            {
                nextFromPattern = true;
                return pattern.Dequeue();
            }
            if (ReturnPortalDue) return ObstacleKind.Portal; // close the flipped section
            // Scripted first encounters (each one also shows a hint).
            if (spawned < cfg.firstGateAt) return ObstacleKind.Pipe;
            if (spawned == cfg.firstGateAt) return ObstacleKind.Gate;
            if (spawned == cfg.firstEnemyAt) return ObstacleKind.Enemy;
            if (spawned == cfg.firstRedGateAt) return ObstacleKind.RedGate;
            if (spawned == cfg.firstSwitchGateAt) return ObstacleKind.SwitchGate;
            if (spawned == cfg.firstPortalAt && !spawnInverted) return ObstacleKind.Portal;
            if (spawned == cfg.firstSwitchWallAt) return ObstacleKind.SwitchWall;
            if (specialsInRow >= cfg.maxGatesInRow) return ObstacleKind.Pipe;
            if (!spawnInverted && KindAvailable(ObstacleKind.Portal) && Random.value < cfg.portalChance) return ObstacleKind.Portal;

            if (spawned >= cfg.firstPatternAt && Random.value < cfg.patternChance)
            {
                var candidates = new List<ObstacleKind[]>();
                foreach (var p in Patterns)
                    if (System.Array.TrueForAll(p, KindAvailable)) candidates.Add(p);
                if (candidates.Count > 0)
                {
                    foreach (var k in candidates[Random.Range(0, candidates.Count)]) pattern.Enqueue(k);
                    nextFromPattern = true;
                    return pattern.Dequeue();
                }
            }

            float roll = Random.value;
            if (roll < cfg.gateChance) return ObstacleKind.Gate;
            roll -= cfg.gateChance;
            if (KindAvailable(ObstacleKind.Enemy) && roll < cfg.enemyChance) return ObstacleKind.Enemy;
            roll -= cfg.enemyChance;
            if (KindAvailable(ObstacleKind.RedGate) && roll < cfg.redGateChance) return ObstacleKind.RedGate;
            roll -= cfg.redGateChance;
            if (KindAvailable(ObstacleKind.SwitchGate) && roll < cfg.switchGateChance) return ObstacleKind.SwitchGate;
            roll -= cfg.switchGateChance;
            if (KindAvailable(ObstacleKind.SwitchWall) && roll < cfg.switchWallChance) return ObstacleKind.SwitchWall;
            return ObstacleKind.Pipe;
        }

        float Spacing(ObstacleKind current, ObstacleKind next)
        {
            // Anything that may be dashed needs room after it (dash boost eats distance);
            // enemies also close in on whatever is ahead of them.
            bool dashable = current == ObstacleKind.Gate || current == ObstacleKind.SwitchGate;
            if (dashable) return next == ObstacleKind.Enemy ? Mathf.Max(cfg.gateSpacingAfter, cfg.enemySpacingBefore) : cfg.gateSpacingAfter;
            if (current == ObstacleKind.Portal) return next == ObstacleKind.Enemy ? Mathf.Max(cfg.portalSpacingAfter, cfg.enemySpacingBefore) : cfg.portalSpacingAfter;
            if (current == ObstacleKind.Enemy) return cfg.enemySpacingAfter;
            if (next == ObstacleKind.Portal) return cfg.portalSpacingBefore;
            if (current == ObstacleKind.SwitchWall) return cfg.switchWallSpacingAfter;
            if (next == ObstacleKind.SwitchWall) return cfg.switchWallSpacingBefore;
            if (next == ObstacleKind.Enemy) return cfg.enemySpacingBefore;
            if (current == ObstacleKind.RedGate) return cfg.pipeSpacing;
            if (next == ObstacleKind.Gate || next == ObstacleKind.SwitchGate) return cfg.gateSpacingBefore;
            return cfg.pipeSpacing; // pipe -> pipe, pipe -> red gate (needs vertical travel)
        }

        TrapGate CreateTrap()
        {
            var go = new GameObject("TrapGate");
            go.transform.SetParent(obstacleRoot, false);
            var trap = go.AddComponent<TrapGate>();
            trap.Build(art);
            trap.Flip = OnGateFlipped;
            return trap;
        }

        PipePair CreatePipe()
        {
            var go = new GameObject("Pipe");
            go.transform.SetParent(obstacleRoot, false);
            var pipe = go.AddComponent<PipePair>();
            pipe.Build(art);
            return pipe;
        }

        DashGate CreateGate()
        {
            var go = new GameObject("DashGate");
            go.transform.SetParent(obstacleRoot, false);
            var gate = go.AddComponent<DashGate>();
            gate.Build(art);
            return gate;
        }

        SpikyEnemy CreateEnemy()
        {
            var go = new GameObject("SpikyEnemy");
            go.transform.SetParent(obstacleRoot, false);
            var enemy = go.AddComponent<SpikyEnemy>();
            enemy.Build(art);
            return enemy;
        }

        void Recycle(Obstacle o)
        {
            o.gameObject.SetActive(false);
            if (o is PipePair p) pipePool.Push(p);
            else if (o is DashGate g) gatePool.Push(g);
            else if (o is SpikyEnemy e) enemyPool.Push(e);
            else if (o is TrapGate t) trapPool.Push(t);
            else if (o is SwitchWall w) switchPool.Push(w);
        }
    }
}
