using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TapOrDrag
{
    /// <summary>
    /// DOG BLAST: vertical shooter mode. Drag anywhere to move the dog's saucer (relative to the finger); it fires
    /// automatically. Cats attack in waves; one of three bosses (rotating) arrives every
    /// <see cref="GameConfig.shooterBossEvery"/> seconds with its own health bar and a harder second phase.
    ///
    /// Progression layers:
    ///  - Weapons (Blaster / Homing / Plasma / Wave) from capsules, scaled by power level, lost on taking a hit.
    ///  - Ship skill per skin, charged by kills, fired from the HUD button (or E).
    ///  - Support pickups (magnet, shield, wingmen, rapid fire, bomb) and hangar items (bombs, shield charm, lucky bone).
    ///  - Permanent hangar upgrades (ShipUpgrades) applied at the start of each run.
    /// Everything is pooled SpriteRenderers under one root that is hidden outside the mode.
    /// </summary>
    public class ShooterGame : MonoBehaviour
    {
        enum EnemyKind { Drone, Saucer, Diver, Rock, Boss }
        enum BossKind { Mothership, LaserCat, YarnKing }
        enum PickupKind { Bone, Power, Heart, Magnet, Shield, Wingman, Rapid, Bomb, WeaponHoming, WeaponPlasma, WeaponWave }
        enum Weapon { Blaster, Homing, Plasma, Wave }
        enum ShotKind { Pellet, Missile, Plasma, Wave, Enemy }

        // Buff bits for the HUD icon row.
        public const int BuffMagnet = 1, BuffRapid = 2, BuffWingmen = 4, BuffShield = 8, BuffGold = 16, BuffWarp = 32, BuffIce = 64;

        sealed class Shot
        {
            public SpriteRenderer R;
            public ShotKind Kind;
            public Vector2 P, V;
            public float Damage, T, BaseX, Phase;
            public Enemy LastHit;
            public bool Alive;
        }

        sealed class Enemy
        {
            public SpriteRenderer R, BarBack, BarFill;
            public EnemyKind Kind;
            public BossKind Boss;
            public Vector2 P, V, DiveVelocity;
            public float Hp, MaxHp, T, FireT, AltT, Radius, BaseX, TargetY, Flash;
            public int Score, Pattern;
            public bool Alive, Diving, Phase2;
        }

        sealed class Pickup
        {
            public SpriteRenderer R;
            public PickupKind Kind;
            public Vector2 P;
            public float T;
            public bool Alive;
        }

        sealed class LaserStrike
        {
            public SpriteRenderer R;
            public float X, T;
            public bool Alive;
        }

        const float StrikeWarn = 0.8f, StrikeBeam = 0.5f, StrikeHalfWidth = 0.35f;

        public event Action<int, int> Finished;  // score, bones collected
        public event Action BoneCollected, EnemyKilled;
        public bool Running { get; private set; }

        GameConfig cfg;
        Art art;
        Fx fx;
        AudioManager sound;
        Hud hud;
        Bird bird;
        Func<Vector2, bool> isBlocked;
        Transform root;

        readonly List<Shot> shots = new List<Shot>();
        readonly List<Shot> enemyShots = new List<Shot>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Pickup> pickups = new List<Pickup>();
        readonly List<LaserStrike> strikes = new List<LaserStrike>();
        readonly List<(SpriteRenderer r, float speed)> stars = new List<(SpriteRenderer, float)>();
        readonly List<(SpriteRenderer r, float speed)> nebulas = new List<(SpriteRenderer, float)>();
        SpriteRenderer planet, shockwave, megaLaser, wingmanLeft, wingmanRight;

        // Run state
        Vector2 shipPos, lastPointer;
        bool dragging, dying, luckyBone;
        int pointerId = -1, hearts, maxHearts, power, score, bones, combo, multiplier, bossesDefeated, shotCount, boneChain, bombs, shieldHits, buffMask = -1;
        float comboTimer, fireTimer, invulnerable, time, spawnTimer, bossTimer, endTimer, velocityX, hitSoundCooldown, boneChainTimer;
        float magnetTimer, rapidTimer, wingmanTimer, wingmanFire, iceTimer, goldTimer, warpTimer, laserTimer, shockTimer;
        float damageMultiplier, fireIntervalMultiplier, magnetRange, killsForSkill, skillCharge;
        Weapon weapon;
        ShipSkill skill;
        string skillShort, skillName;
        Enemy boss;

        float MinX => -World.ViewWidth * 0.5f + 0.55f;
        float MaxX => World.ViewWidth * 0.5f - 0.55f;
        float MinY => World.Bottom + 1.4f;
        float MaxY => World.Top - 3.2f;
        bool Warped => warpTimer > 0f;

        public void Build(GameConfig config, Art sourceArt, Fx effects, AudioManager audio, Hud ui, Bird player, Func<Vector2, bool> blocked)
        {
            cfg = config;
            art = sourceArt;
            fx = effects;
            sound = audio;
            hud = ui;
            bird = player;
            isBlocked = blocked;
            root = new GameObject("ShooterWorld").transform;
            root.SetParent(transform, false);
            BuildBackground();

            shockwave = NewRenderer("Shockwave", 12);
            shockwave.sprite = art.GlowRadial;
            shockwave.enabled = false;
            megaLaser = NewRenderer("MegaLaser", 7);
            megaLaser.sprite = art.ShipLaser;
            megaLaser.drawMode = SpriteDrawMode.Tiled;
            megaLaser.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            megaLaser.enabled = false;
            wingmanLeft = NewRenderer("WingmanL", 9);
            wingmanRight = NewRenderer("WingmanR", 9);
            wingmanLeft.sprite = wingmanRight.sprite = art.IconWingman;
            wingmanLeft.transform.localScale = wingmanRight.transform.localScale = Vector3.one * 1.6f;
            wingmanLeft.enabled = wingmanRight.enabled = false;

            root.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- lifecycle

        public void Begin(SkinDef ship)
        {
            Running = true;
            dying = false;
            root.gameObject.SetActive(true);
            ClearEntities();

            shipPos = new Vector2(0f, World.Bottom + 3f);
            bird.gameObject.SetActive(true);
            bird.ResetAt(shipPos.y);
            bird.TickShooter(0f, shipPos, 0f);

            // Hangar upgrades.
            damageMultiplier = ShipUpgrades.DamageMultiplier;
            fireIntervalMultiplier = ShipUpgrades.FireIntervalMultiplier;
            magnetRange = 2f * ShipUpgrades.MagnetMultiplier;
            killsForSkill = Mathf.Max(8f, 20f * ShipUpgrades.SkillChargeMultiplier);
            maxHearts = cfg.shooterMaxHearts + ShipUpgrades.BonusHearts;
            hearts = cfg.shooterHearts + ShipUpgrades.BonusHearts;

            // Hangar items (passives are consumed now if switched on).
            shieldHits = Inventory.ConsumePassive(ItemKind.ShieldCharm) ? 1 : 0;
            luckyBone = Inventory.ConsumePassive(ItemKind.LuckyBone);
            bombs = 0;

            skill = ship.ShipSkill;
            skillShort = ship.ShipSkillShort;
            skillName = ship.ShipSkillName;
            skillCharge = 0f;
            weapon = Weapon.Blaster;
            power = 1;
            score = bones = combo = bossesDefeated = shotCount = 0;
            multiplier = 1;
            time = 0f;
            spawnTimer = 1.5f;
            bossTimer = cfg.shooterBossEvery;
            invulnerable = 0f;
            magnetTimer = rapidTimer = wingmanTimer = iceTimer = goldTimer = warpTimer = laserTimer = shockTimer = 0f;
            dragging = false;
            boss = null;
            buffMask = -1;

            hud.SetShooterHud(true);
            hud.SetHearts(hearts, maxHearts);
            hud.SetWeapon(WeaponName, power);
            hud.SetBombs(TotalBombs);
            hud.SetSkillCharge(0f, false, skillShort);
            hud.ShowBossBar(false, 0f);
            hud.SetScore(0, 1, false);
            if (shieldHits > 0) bird.SetShield(true);
            if (luckyBone) hud.Toast("LUCKY BONE X2", Pal.Gold);
            UpdateBuffIcons();
        }

        public void End()
        {
            Running = false;
            ClearEntities();
            root.gameObject.SetActive(false);
            bird.SetShield(false);
            hud.SetShooterHud(false);
            hud.ShowBossBar(false, 0f);
        }

        void ClearEntities()
        {
            foreach (var s in shots) Kill(s);
            foreach (var s in enemyShots) Kill(s);
            foreach (var e in enemies) Remove(e);
            foreach (var p in pickups) { p.Alive = false; p.R.enabled = false; }
            foreach (var st in strikes) { st.Alive = false; st.R.enabled = false; }
            if (megaLaser != null) megaLaser.enabled = shockwave.enabled = wingmanLeft.enabled = wingmanRight.enabled = false;
        }

        int TotalBombs => bombs + Inventory.Count(ItemKind.Bomb);
        string WeaponName => weapon == Weapon.Homing ? "HOMING" : weapon == Weapon.Plasma ? "PLASMA" : weapon == Weapon.Wave ? "WAVE" : "BLASTER";

        public void Tick(float dt)
        {
            time += dt;
            TickBackground(dt);
            float enemyDt = Warped ? dt * 0.3f : dt;
            if (dying)
            {
                TickShots(dt, enemyDt);
                TickEnemies(enemyDt);
                endTimer -= dt;
                if (endTimer <= 0f)
                {
                    Running = false;
                    Finished?.Invoke(score, bones);
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.E)) ActivateSkill();
            if (Input.GetKeyDown(KeyCode.Q)) UseBomb();

            TickInput(dt);
            bird.TickShooter(dt, shipPos, velocityX);
            TickFire(dt);
            TickSkillEffects(dt);
            TickSpawning(dt);
            TickShots(dt, enemyDt);
            TickEnemies(enemyDt);
            TickStrikes(enemyDt);
            TickPickups(dt);
            TickCollisions();
            TickTimers(dt);
        }

        void TickTimers(float dt)
        {
            if (invulnerable > 0f) invulnerable -= dt;
            if (hitSoundCooldown > 0f) hitSoundCooldown -= dt;
            magnetTimer -= dt;
            rapidTimer -= dt;
            wingmanTimer -= dt;
            goldTimer -= dt;
            warpTimer -= dt;
            if (iceTimer > 0f && (iceTimer -= dt) <= 0f) bird.SetShield(shieldHits > 0);
            boneChainTimer -= dt;
            if (boneChainTimer <= 0f) boneChain = 0;
            comboTimer -= dt;
            if (comboTimer <= 0f && combo > 0)
            {
                combo = 0;
                multiplier = 1;
                hud.SetScore(score, 1, false);
            }
            UpdateBuffIcons();
        }

        void UpdateBuffIcons()
        {
            int mask = (magnetTimer > 0f ? BuffMagnet : 0) | (rapidTimer > 0f ? BuffRapid : 0) | (wingmanTimer > 0f ? BuffWingmen : 0)
                       | (shieldHits > 0 ? BuffShield : 0) | (goldTimer > 0f ? BuffGold : 0) | (warpTimer > 0f ? BuffWarp : 0) | (iceTimer > 0f ? BuffIce : 0);
            if (mask == buffMask) return;
            buffMask = mask;
            hud.SetBuffIcons(mask);
        }

        // ---------------------------------------------------------------- input

        /// <summary>Screen point to world, from the unshaken camera metrics so camera shake does not jitter the drag.</summary>
        static Vector2 ScreenToWorld(Vector2 screen)
        {
            float unitsPerPixel = World.CamHalfHeight * 2f / Mathf.Max(1, Screen.height);
            return new Vector2((screen.x - Screen.width * 0.5f) * unitsPerPixel, World.CamCenterY + (screen.y - Screen.height * 0.5f) * unitsPerPixel);
        }

        void TickInput(float dt)
        {
            Vector2 before = shipPos;
            var keys = new Vector2(
                (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? 1f : 0f));
            shipPos += keys * (cfg.shooterKeyboardSpeed * dt);

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase == TouchPhase.Began && !dragging && !isBlocked(t.position))
                    {
                        dragging = true;
                        pointerId = t.fingerId;
                        lastPointer = ScreenToWorld(t.position);
                    }
                    else if (dragging && t.fingerId == pointerId)
                    {
                        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) dragging = false;
                        else Drag(t.position);
                    }
                }
            }
            else
            {
                if (dragging && pointerId >= 0) dragging = false; // touch lost
                if (Input.GetMouseButtonDown(0) && !isBlocked(Input.mousePosition))
                {
                    dragging = true;
                    pointerId = -1;
                    lastPointer = ScreenToWorld(Input.mousePosition);
                }
                else if (dragging && pointerId == -1)
                {
                    if (Input.GetMouseButton(0)) Drag(Input.mousePosition);
                    else dragging = false;
                }
            }

            shipPos.x = Mathf.Clamp(shipPos.x, MinX, MaxX);
            shipPos.y = Mathf.Clamp(shipPos.y, MinY, MaxY);
            velocityX = dt > 0f ? (shipPos.x - before.x) / dt : 0f;
        }

        void Drag(Vector2 screen)
        {
            var world = ScreenToWorld(screen);
            shipPos += (world - lastPointer) * cfg.shooterDragSensitivity;
            lastPointer = world;
        }

        // ---------------------------------------------------------------- weapons

        void TickFire(float dt)
        {
            fireTimer -= dt;
            if (fireTimer > 0f) return;
            float weaponRate = weapon == Weapon.Homing ? 1.6f : weapon == Weapon.Plasma ? 1.8f : weapon == Weapon.Wave ? 1.2f : 1f;
            fireTimer = cfg.shooterFireInterval * fireIntervalMultiplier * weaponRate * (rapidTimer > 0f ? 0.5f : 1f);
            var muzzle = shipPos + new Vector2(0.05f, 0.7f);
            float dmg = damageMultiplier;
            switch (weapon)
            {
                case Weapon.Homing:
                    int missiles = 1 + power / 2;
                    for (int i = 0; i < missiles; i++)
                        Fire(ShotKind.Missile, art.Missile, muzzle + new Vector2((i - (missiles - 1) * 0.5f) * 0.3f, 0f), (i - (missiles - 1) * 0.5f) * 25f, 11f, 1.6f * dmg);
                    break;
                case Weapon.Plasma:
                    if (power >= 3)
                    {
                        Fire(ShotKind.Plasma, art.PlasmaOrb, muzzle + Vector2.left * 0.25f, -4f, 12f, (1.6f + 0.3f * power) * dmg);
                        Fire(ShotKind.Plasma, art.PlasmaOrb, muzzle + Vector2.right * 0.25f, 4f, 12f, (1.6f + 0.3f * power) * dmg);
                    }
                    else Fire(ShotKind.Plasma, art.PlasmaOrb, muzzle, 0f, 12f, (1.8f + 0.4f * power) * dmg);
                    break;
                case Weapon.Wave:
                    int waves = power >= 4 ? 3 : power >= 2 ? 2 : 1;
                    for (int i = 0; i < waves; i++)
                    {
                        var s = Fire(ShotKind.Wave, art.WaveShot, muzzle, 0f, 13f, dmg);
                        s.Phase = i * (Mathf.PI * 2f / waves); // phase offset per wave
                    }
                    break;
                default:
                    switch (power)
                    {
                        case 1: Fire(ShotKind.Pellet, art.PlayerPellet, muzzle, 0f, cfg.shooterBulletSpeed, dmg); break;
                        case 2:
                            Fire(ShotKind.Pellet, art.PlayerPellet, muzzle + Vector2.left * 0.18f, 0f, cfg.shooterBulletSpeed, dmg);
                            Fire(ShotKind.Pellet, art.PlayerPellet, muzzle + Vector2.right * 0.18f, 0f, cfg.shooterBulletSpeed, dmg);
                            break;
                        case 3:
                            foreach (float a in new[] { 0f, -10f, 10f }) Fire(ShotKind.Pellet, art.PlayerPellet, muzzle, a, cfg.shooterBulletSpeed, dmg);
                            break;
                        default:
                            foreach (float a in new[] { 0f, -8f, 8f, -18f, 18f }) Fire(ShotKind.Pellet, art.PlayerPellet, muzzle, a, cfg.shooterBulletSpeed, dmg);
                            break;
                    }
                    break;
            }
            if (++shotCount % 2 == 0) sound.Shoot();
        }

        Shot Fire(ShotKind kind, Sprite sprite, Vector2 from, float angleDegrees, float speed, float damage)
        {
            var s = GetShot(shots, 8);
            float a = angleDegrees * Mathf.Deg2Rad;
            s.Kind = kind;
            s.R.sprite = sprite;
            s.P = from;
            s.BaseX = from.x;
            s.V = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * speed;
            s.Damage = damage;
            s.T = s.Phase = 0f;
            s.LastHit = null;
            s.R.transform.localRotation = Quaternion.Euler(0f, 0f, -angleDegrees);
            Place(s.R, s.P);
            return s;
        }

        void EnemyFire(Vector2 from, Vector2 direction, float speed)
        {
            var s = GetShot(enemyShots, 9);
            s.Kind = ShotKind.Enemy;
            s.R.sprite = art.EnemyOrb;
            s.P = from;
            s.V = direction.normalized * speed;
            Place(s.R, s.P);
        }

        Enemy NearestEnemy(Vector2 from)
        {
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in enemies)
            {
                if (!e.Alive || e.P.y > World.Top + 0.5f) continue;
                float d = (e.P - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        // ---------------------------------------------------------------- ship skill & bombs

        public void ActivateSkill()
        {
            if (!Running || dying || skillCharge < 1f) return;
            skillCharge = 0f;
            hud.SetSkillCharge(0f, false, skillShort);
            hud.Flash(Color.white, 0.35f);
            hud.Toast(skillName + "!", Art.PelletColor);
            switch (skill)
            {
                case ShipSkill.BarkBlast:
                    sound.RunStart(); // the bark
                    sound.BigExplode();
                    foreach (var s in enemyShots) Kill(s);
                    foreach (var e in enemies)
                        if (e.Alive && (e.P - shipPos).sqrMagnitude < 25f) Damage(e, 12f * damageMultiplier);
                    shockTimer = 0.45f;
                    fx.Shake(0.4f, 0.35f);
                    break;
                case ShipSkill.IceShield:
                    iceTimer = 6f;
                    bird.SetShield(true);
                    sound.ShieldPop();
                    break;
                case ShipSkill.Wingmen:
                    wingmanTimer = 8f;
                    sound.PowerUp();
                    break;
                case ShipSkill.TimeWarp:
                    warpTimer = 5f;
                    sound.Portal(true);
                    break;
                case ShipSkill.MegaLaser:
                    laserTimer = 3f;
                    sound.MeteorLaunch();
                    break;
                case ShipSkill.GoldRush:
                    goldTimer = 8f;
                    sound.Purchase();
                    break;
            }
            fx.PerfectRing(shipPos, Pal.Gold);
            UpdateBuffIcons();
        }

        public void UseBomb()
        {
            if (!Running || dying) return;
            if (bombs > 0) bombs--;
            else if (!Inventory.TryUse(ItemKind.Bomb))
            {
                sound.Miss();
                return;
            }
            hud.SetBombs(TotalBombs);
            foreach (var s in enemyShots) Kill(s);
            foreach (var st in strikes) { st.Alive = false; st.R.enabled = false; }
            foreach (var e in enemies)
                if (e.Alive) Damage(e, 15f);
            hud.Flash(Color.white, 0.8f);
            fx.Shake(0.5f, 0.45f);
            sound.BigExplode();
            shockTimer = 0.45f;
        }

        void AddSkillCharge(float kills)
        {
            if (skillCharge >= 1f) return;
            skillCharge = Mathf.Min(1f, skillCharge + kills / killsForSkill);
            hud.SetSkillCharge(skillCharge, skillCharge >= 1f, skillShort);
            if (skillCharge >= 1f) sound.SkillReady();
        }

        void TickSkillEffects(float dt)
        {
            // Bark / bomb shockwave visual.
            if (shockTimer > 0f)
            {
                shockTimer -= dt;
                float k = 1f - shockTimer / 0.45f;
                shockwave.enabled = shockTimer > 0f;
                shockwave.transform.localPosition = shipPos;
                shockwave.transform.localScale = Vector3.one * Mathf.Lerp(1f, 9f, k);
                shockwave.color = new Color(1f, 0.95f, 0.7f, 0.8f * (1f - k));
            }

            // Wingmen: two drones beside the ship firing straight up.
            bool wings = wingmanTimer > 0f;
            wingmanLeft.enabled = wingmanRight.enabled = wings;
            if (wings)
            {
                var l = shipPos + new Vector2(-0.95f, -0.25f + Mathf.Sin(time * 6f) * 0.08f);
                var r = shipPos + new Vector2(0.95f, -0.25f - Mathf.Sin(time * 6f) * 0.08f);
                Place(wingmanLeft, l);
                Place(wingmanRight, r);
                if ((wingmanFire -= dt) <= 0f)
                {
                    wingmanFire = 0.18f;
                    Fire(ShotKind.Pellet, art.PlayerPellet, l + Vector2.up * 0.3f, 0f, cfg.shooterBulletSpeed, damageMultiplier);
                    Fire(ShotKind.Pellet, art.PlayerPellet, r + Vector2.up * 0.3f, 0f, cfg.shooterBulletSpeed, damageMultiplier);
                }
            }

            // ROBO mega laser: column beam that shreds enemies and bullets.
            bool laser = laserTimer > 0f;
            megaLaser.enabled = laser;
            if (laser)
            {
                laserTimer -= dt;
                float length = World.Top - shipPos.y + 1f;
                megaLaser.size = new Vector2(length, 0.9f);
                megaLaser.transform.localPosition = new Vector3(shipPos.x, shipPos.y + 0.5f + length * 0.5f, 0f);
                megaLaser.color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(time * 40f));
                foreach (var e in enemies)
                    if (e.Alive && e.P.y > shipPos.y && Mathf.Abs(e.P.x - shipPos.x) < 0.45f + e.Radius) Damage(e, 30f * damageMultiplier * dt);
                foreach (var s in enemyShots)
                    if (s.Alive && s.P.y > shipPos.y && Mathf.Abs(s.P.x - shipPos.x) < 0.5f) Kill(s);
            }

            if (goldTimer > 0f && (int)(time * 20f) % 2 == 0) fx.Trail(shipPos + Vector2.down * 0.4f, Pal.Gold, 0f);
        }

        // ---------------------------------------------------------------- spawning

        void TickSpawning(float dt)
        {
            bossTimer -= dt;
            if (boss == null && bossTimer <= 0f)
            {
                SpawnBoss();
                return;
            }

            float interval = Mathf.Max(cfg.shooterSpawnIntervalMin, cfg.shooterSpawnIntervalStart - time * cfg.shooterSpawnRamp);
            if (boss != null) interval *= 2.5f;
            spawnTimer -= dt;
            if (spawnTimer > 0f) return;
            spawnTimer = interval;

            float hpScale = 1f + time / 90f;
            float top = World.Top + 1f;
            if (boss != null)
            {
                SpawnDroneLine(3, top);
                return;
            }
            float roll = Random.value;
            if (roll < 0.35f)
            {
                if (Random.value < 0.5f) SpawnDroneLine(5, top);
                else SpawnDroneV(top);
            }
            else if (roll < 0.6f) SpawnEnemy(EnemyKind.Saucer, new Vector2(Random.Range(MinX + 0.6f, MaxX - 0.6f), top), 4f * hpScale);
            else if (roll < 0.8f)
            {
                SpawnEnemy(EnemyKind.Diver, new Vector2(Random.Range(MinX, -0.5f), top), 2f * hpScale);
                SpawnEnemy(EnemyKind.Diver, new Vector2(Random.Range(0.5f, MaxX), top + 0.8f), 2f * hpScale);
            }
            else SpawnEnemy(EnemyKind.Rock, new Vector2(Random.Range(MinX, MaxX), top), 5f * hpScale);
        }

        void SpawnDroneLine(int count, float top)
        {
            float width = MaxX - MinX;
            for (int i = 0; i < count; i++)
                SpawnEnemy(EnemyKind.Drone, new Vector2(MinX + width * (i + 0.5f) / count, top + (i % 2) * 0.4f), 1f);
        }

        void SpawnDroneV(float top)
        {
            float cx = Random.Range(MinX + 1.5f, MaxX - 1.5f);
            for (int i = -2; i <= 2; i++)
                SpawnEnemy(EnemyKind.Drone, new Vector2(cx + i * 0.8f, top + Mathf.Abs(i) * 0.6f), 1f);
        }

        Enemy SpawnEnemy(EnemyKind kind, Vector2 position, float hp)
        {
            Enemy e = null;
            foreach (var candidate in enemies)
                if (!candidate.Alive) { e = candidate; break; }
            if (e == null)
            {
                e = new Enemy { R = NewRenderer("Enemy", 5), BarBack = NewRenderer("HpBack", 11), BarFill = NewRenderer("HpFill", 12) };
                e.BarBack.sprite = e.BarFill.sprite = art.Pixel;
                e.BarBack.color = new Color(0.1f, 0.05f, 0.15f, 0.85f);
                enemies.Add(e);
            }
            e.Kind = kind;
            e.P = position;
            e.V = Vector2.zero;
            e.BaseX = position.x;
            e.Hp = e.MaxHp = Mathf.Ceil(hp);
            e.T = e.AltT = 0f;
            e.FireT = Random.Range(0.8f, 1.6f);
            e.Flash = 0f;
            e.Diving = e.Phase2 = false;
            e.Pattern = 0;
            e.Alive = true;
            e.R.enabled = true;
            e.R.color = Color.white;
            e.R.transform.localScale = Vector3.one;
            e.R.transform.localRotation = Quaternion.identity;
            e.BarBack.enabled = e.BarFill.enabled = false;
            switch (kind)
            {
                case EnemyKind.Drone: e.R.sprite = art.CatDrone; e.Radius = 0.38f; e.Score = 10; break;
                case EnemyKind.Saucer: e.R.sprite = art.CatSaucer; e.Radius = 0.55f; e.Score = 40; e.TargetY = World.Top - Random.Range(3.2f, 5.5f); break;
                case EnemyKind.Diver: e.R.sprite = art.Spiky[0]; e.Radius = 0.45f; e.Score = 25; e.TargetY = World.Top - Random.Range(2.2f, 3.5f); break;
                case EnemyKind.Rock: e.R.sprite = art.Meteor; e.Radius = 0.5f; e.Score = 30; break;
            }
            Place(e.R, e.P);
            return e;
        }

        void SpawnBoss()
        {
            var kind = (BossKind)(bossesDefeated % 3);
            float hp = cfg.shooterBossHp + cfg.shooterBossHpGrowth * bossesDefeated;
            boss = SpawnEnemy(EnemyKind.Boss, new Vector2(0f, World.Top + 3f), hp);
            boss.Boss = kind;
            boss.Score = 1000 * (1 + bossesDefeated);
            boss.TargetY = World.Top - 3.4f;
            boss.BaseX = 0f;
            boss.R.transform.localScale = Vector3.one * 3f;
            string bossName;
            Color32 color;
            switch (kind)
            {
                case BossKind.LaserCat:
                    boss.R.sprite = art.LaserCatBoss;
                    boss.Radius = 1.4f;
                    bossName = "LASER CAT";
                    color = Pal.Red;
                    break;
                case BossKind.YarnKing:
                    boss.R.sprite = art.YarnKingBoss;
                    boss.Radius = 1.5f;
                    boss.V = new Vector2(2.2f, 1.6f);
                    bossName = "YARN KING";
                    color = Pal.Hex("ff8fc4");
                    break;
                default:
                    boss.R.sprite = art.CatBoss;
                    boss.Radius = 1.35f;
                    bossName = "CAT MOTHERSHIP";
                    color = Pal.Hex("b58cff");
                    break;
            }
            hud.SetBossInfo(bossName, color);
            hud.ShowBossBar(true, 1f);
            hud.BossWarning(bossName);
            sound.BossAlarm();
            fx.Shake(0.2f, 0.4f);
        }

        // ---------------------------------------------------------------- simulation

        void TickShots(float dt, float enemyDt)
        {
            const float margin = 1.5f;
            foreach (var s in shots)
            {
                if (!s.Alive) continue;
                s.T += dt;
                switch (s.Kind)
                {
                    case ShotKind.Missile:
                        var target = NearestEnemy(s.P);
                        if (target != null)
                        {
                            Vector2 desired = (target.P - s.P).normalized * s.V.magnitude;
                            s.V = Vector3.RotateTowards(s.V, desired, 8f * dt, 0f);
                        }
                        s.R.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(s.V.y, s.V.x) * Mathf.Rad2Deg - 90f);
                        s.P += s.V * dt;
                        break;
                    case ShotKind.Wave:
                        s.P.y += s.V.y * dt;
                        s.P.x = s.BaseX + Mathf.Sin(s.T * 11f + s.Phase) * 0.9f * Mathf.Min(1f, s.T * 4f);
                        break;
                    default:
                        s.P += s.V * dt;
                        break;
                }
                Place(s.R, s.P);
                if (s.P.y > World.Top + margin || s.P.y < World.Bottom - margin || Mathf.Abs(s.P.x) > World.CamHalfWidth + margin) Kill(s);
            }
            foreach (var s in enemyShots)
            {
                if (!s.Alive) continue;
                s.P += s.V * enemyDt;
                Place(s.R, s.P);
                if (s.P.y > World.Top + margin || s.P.y < World.Bottom - margin || Mathf.Abs(s.P.x) > World.CamHalfWidth + margin) Kill(s);
            }
        }

        void TickEnemies(float dt)
        {
            foreach (var e in enemies)
            {
                if (!e.Alive) continue;
                e.T += dt;
                if (e.Flash > 0f) e.Flash -= dt;
                e.R.color = e.Flash > 0f ? new Color(1f, 0.55f, 0.55f) : Warped ? new Color(0.75f, 0.65f, 1f) : Color.white;
                switch (e.Kind)
                {
                    case EnemyKind.Drone:
                        e.P.y -= 2.4f * dt;
                        e.P.x = e.BaseX + Mathf.Sin(e.T * 3f) * 0.6f;
                        break;
                    case EnemyKind.Saucer:
                        if (e.P.y > e.TargetY && e.T < 9f) e.P.y = Mathf.MoveTowards(e.P.y, e.TargetY, 3f * dt);
                        else if (e.T >= 9f) e.P.y -= 2f * dt;
                        e.P.x = Mathf.Clamp(e.BaseX + Mathf.Sin(e.T * 1.2f) * 2f, MinX, MaxX);
                        if (!dying && e.T < 9f && (e.FireT -= dt) <= 0f)
                        {
                            e.FireT = 1.5f;
                            EnemyFire(e.P + Vector2.down * 0.4f, shipPos - e.P, cfg.shooterEnemyBulletSpeed);
                        }
                        break;
                    case EnemyKind.Diver:
                        if (!e.Diving)
                        {
                            e.P.y = Mathf.MoveTowards(e.P.y, e.TargetY, 4f * dt);
                            if (e.T > 1.3f)
                            {
                                e.Diving = true;
                                e.DiveVelocity = (shipPos - e.P).normalized * 9f;
                            }
                            e.R.sprite = art.Spiky[(int)(e.T * 10f) & 1];
                        }
                        else
                        {
                            e.P += e.DiveVelocity * dt;
                            e.R.transform.localRotation = Quaternion.Euler(0f, 0f, e.T * 540f);
                        }
                        break;
                    case EnemyKind.Rock:
                        e.P += new Vector2(Mathf.Sin(e.BaseX * 7f) * 0.5f, -2f) * dt;
                        e.R.transform.localRotation = Quaternion.Euler(0f, 0f, e.T * 90f);
                        break;
                    case EnemyKind.Boss:
                        TickBoss(e, dt);
                        break;
                }
                Place(e.R, e.P);
                UpdateHealthBar(e);
                if (e.Kind != EnemyKind.Boss && (e.P.y < World.Bottom - 2f || e.P.y > World.Top + 4f || Mathf.Abs(e.P.x) > World.CamHalfWidth + 3f))
                    Remove(e);
            }
        }

        /// <summary>Small bar above tougher enemies once they have taken damage.</summary>
        void UpdateHealthBar(Enemy e)
        {
            bool show = e.Kind != EnemyKind.Boss && e.MaxHp >= 3f && e.Hp < e.MaxHp;
            e.BarBack.enabled = e.BarFill.enabled = show;
            if (!show) return;
            float width = e.Radius * 2f, frac = Mathf.Clamp01(e.Hp / e.MaxHp);
            var top = e.P + Vector2.up * (e.Radius + 0.25f);
            e.BarBack.transform.localPosition = top;
            e.BarBack.transform.localScale = new Vector3(width + 0.1f, 0.16f, 1f);
            e.BarFill.transform.localPosition = new Vector3(top.x - width * 0.5f * (1f - frac), top.y, 0f);
            e.BarFill.transform.localScale = new Vector3(width * frac, 0.09f, 1f);
            e.BarFill.color = Color.Lerp(Pal.Red, new Color32(125, 255, 154, 255), frac);
        }

        // ---------------------------------------------------------------- bosses

        void TickBoss(Enemy e, float dt)
        {
            if (!e.Phase2 && e.Hp < e.MaxHp * 0.5f)
            {
                e.Phase2 = true;
                hud.Toast("PHASE 2!", Pal.Red);
                sound.BossAlarm();
                fx.Shake(0.3f, 0.4f);
            }
            if (e.P.y > e.TargetY)
            {
                e.P.y = Mathf.MoveTowards(e.P.y, e.TargetY, 1.5f * dt);
                return;
            }
            switch (e.Boss)
            {
                case BossKind.Mothership: TickMothership(e, dt); break;
                case BossKind.LaserCat: TickLaserCat(e, dt); break;
                case BossKind.YarnKing: TickYarnKing(e, dt); break;
            }
        }

        void TickMothership(Enemy e, float dt)
        {
            e.P.x = Mathf.Sin(e.T * 1.25f) * 2.2f;
            if (dying || (e.FireT -= dt) > 0f) return;
            e.FireT = e.Phase2 ? 1.2f : 1.8f;
            var mouth = e.P + Vector2.down * 1.1f;
            float speed = cfg.shooterEnemyBulletSpeed;
            switch (e.Pattern++ % 3)
            {
                case 0:
                    int half = e.Phase2 ? 4 : 3;
                    for (int i = -half; i <= half; i++)
                    {
                        float a = (-90f + i * 15f) * Mathf.Deg2Rad;
                        EnemyFire(mouth, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), speed);
                    }
                    break;
                case 1:
                    int count = e.Phase2 ? 16 : 12;
                    for (int i = 0; i < count; i++)
                    {
                        float a = (i * 360f / count + e.T * 40f) * Mathf.Deg2Rad;
                        EnemyFire(mouth, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), speed * 0.8f);
                    }
                    break;
                default:
                    var aim = (shipPos - mouth).normalized;
                    for (int i = -1; i <= 1; i++) EnemyFire(mouth, Quaternion.Euler(0f, 0f, i * 8f) * aim, speed * 1.2f);
                    break;
            }
        }

        /// <summary>Telegraphs vertical laser strikes (thin flashing line, then a deadly beam) and fires aimed shots between.</summary>
        void TickLaserCat(Enemy e, float dt)
        {
            e.P.x = Mathf.Sin(e.T * 0.9f) * 2f;
            if (dying) return;
            if ((e.AltT -= dt) <= 0f)
            {
                e.AltT = e.Phase2 ? 2.2f : 3.2f;
                int count = e.Phase2 ? 4 : 2;
                for (int i = 0; i < count; i++)
                {
                    float x = i == 0 ? shipPos.x : Random.Range(MinX, MaxX); // one strike always aims at the player
                    AddStrike(Mathf.Clamp(x, MinX, MaxX));
                }
                sound.MeteorWarn();
            }
            if ((e.FireT -= dt) <= 0f)
            {
                e.FireT = e.Phase2 ? 0.9f : 1.4f;
                var cannonL = e.P + new Vector2(-1.3f, -0.9f);
                var cannonR = e.P + new Vector2(1.3f, -0.9f);
                EnemyFire(cannonL, shipPos - cannonL, cfg.shooterEnemyBulletSpeed * 1.1f);
                EnemyFire(cannonR, shipPos - cannonR, cfg.shooterEnemyBulletSpeed * 1.1f);
            }
        }

        /// <summary>Bounces around the upper screen spraying spirals; in phase 2 it doubles the spiral and lunges.</summary>
        void TickYarnKing(Enemy e, float dt)
        {
            float speedScale = e.Phase2 ? 1.4f : 1f;
            e.P += e.V * speedScale * dt;
            float minY = World.Top - 7f, maxY = World.Top - 2f;
            if (e.P.x < MinX + 1f || e.P.x > MaxX - 1f) e.V.x = -e.V.x;
            if (e.P.y < minY || e.P.y > maxY) e.V.y = -e.V.y;
            e.P.x = Mathf.Clamp(e.P.x, MinX + 1f, MaxX - 1f);
            e.P.y = Mathf.Clamp(e.P.y, minY, maxY);
            e.R.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(e.T * 2f) * 10f);
            if (dying || (e.FireT -= dt) > 0f) return;
            e.FireT = e.Phase2 ? 0.1f : 0.14f;
            int arms = e.Phase2 ? 2 : 1;
            for (int i = 0; i < arms; i++)
            {
                float a = (e.T * 190f + i * 180f) * Mathf.Deg2Rad;
                EnemyFire(e.P, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), cfg.shooterEnemyBulletSpeed * 0.75f);
            }
        }

        void AddStrike(float x)
        {
            LaserStrike st = null;
            foreach (var candidate in strikes)
                if (!candidate.Alive) { st = candidate; break; }
            if (st == null)
            {
                st = new LaserStrike { R = NewRenderer("LaserStrike", 4) };
                st.R.sprite = art.Pixel;
                strikes.Add(st);
            }
            st.X = x;
            st.T = 0f;
            st.Alive = true;
            st.R.enabled = true;
        }

        void TickStrikes(float dt)
        {
            float height = World.Top - World.Bottom + 2f, mid = (World.Top + World.Bottom) * 0.5f;
            foreach (var st in strikes)
            {
                if (!st.Alive) continue;
                st.T += dt;
                bool beam = st.T >= StrikeWarn;
                st.R.transform.localPosition = new Vector3(st.X, mid, 0f);
                if (!beam)
                {
                    st.R.transform.localScale = new Vector3(0.08f, height, 1f);
                    st.R.color = new Color(1f, 0.3f, 0.4f, ((int)(st.T * 16f) & 1) == 0 ? 0.8f : 0.25f);
                }
                else
                {
                    st.R.transform.localScale = new Vector3(StrikeHalfWidth * 2f, height, 1f);
                    st.R.color = new Color(1f, 0.35f, 0.45f, 0.85f);
                    if (Mathf.Abs(shipPos.x - st.X) < StrikeHalfWidth + cfg.shooterHitRadius) PlayerHit();
                }
                if (st.T >= StrikeWarn + StrikeBeam)
                {
                    st.Alive = false;
                    st.R.enabled = false;
                }
            }
        }

        // ---------------------------------------------------------------- pickups & collisions

        void TickPickups(float dt)
        {
            float pull = goldTimer > 0f || magnetTimer > 0f ? 99f : magnetRange;
            foreach (var p in pickups)
            {
                if (!p.Alive) continue;
                p.T += dt;
                var toShip = shipPos - p.P;
                if (toShip.sqrMagnitude < pull * pull) p.P = Vector2.MoveTowards(p.P, shipPos, (pull > 50f ? 14f : 9f) * dt);
                else p.P += new Vector2(Mathf.Sin(p.T * 3f) * 0.4f, -1.3f) * dt;
                Place(p.R, p.P);
                p.R.transform.localRotation = p.Kind == PickupKind.Bone ? Quaternion.Euler(0f, 0f, Mathf.Sin(p.T * 5f) * 18f) : Quaternion.identity;
                p.R.transform.localScale = Vector3.one * (p.Kind == PickupKind.Bone ? 1f : 1f + 0.12f * Mathf.Sin(p.T * 8f));
                if (p.P.y < World.Bottom - 1f)
                {
                    p.Alive = false;
                    p.R.enabled = false;
                }
            }
        }

        void TickCollisions()
        {
            foreach (var s in shots)
            {
                if (!s.Alive) continue;
                foreach (var e in enemies)
                {
                    if (!e.Alive || e == s.LastHit) continue;
                    float rr = e.Radius + (s.Kind == ShotKind.Plasma ? 0.28f : 0.1f);
                    if ((s.P - e.P).sqrMagnitude > rr * rr) continue;
                    Damage(e, s.Damage);
                    if (s.Kind == ShotKind.Plasma) s.LastHit = e; // pierces: keeps flying, hits each enemy once
                    else Kill(s);
                    break;
                }
            }

            float hit = cfg.shooterHitRadius;
            foreach (var s in enemyShots)
            {
                if (!s.Alive) continue;
                float rr = hit + 0.12f;
                if ((s.P - shipPos).sqrMagnitude < rr * rr)
                {
                    Kill(s);
                    PlayerHit();
                }
            }

            foreach (var e in enemies)
            {
                if (!e.Alive) continue;
                float rr = e.Radius + hit + 0.15f;
                if ((e.P - shipPos).sqrMagnitude < rr * rr)
                {
                    PlayerHit();
                    if (e.Kind != EnemyKind.Boss) Damage(e, 3f);
                }
            }

            foreach (var p in pickups)
            {
                if (!p.Alive || (p.P - shipPos).sqrMagnitude > 0.36f) continue;
                p.Alive = false;
                p.R.enabled = false;
                Collect(p.Kind, p.P);
            }
        }

        void Damage(Enemy e, float amount)
        {
            if (!e.Alive) return;
            e.Hp -= amount;
            e.Flash = 0.06f;
            if (hitSoundCooldown <= 0f)
            {
                sound.EnemyHit();
                hitSoundCooldown = 0.05f;
            }
            if (e.Kind == EnemyKind.Boss)
            {
                hud.ShowBossBar(true, Mathf.Clamp01(e.Hp / e.MaxHp));
                AddSkillCharge(amount / 25f); // chipping a boss also charges the skill, slowly
            }
            if (e.Hp <= 0f) DestroyEnemy(e);
        }

        void DestroyEnemy(Enemy e)
        {
            Remove(e);
            combo++;
            comboTimer = 1.2f;
            int newMultiplier = Mathf.Min(1 + combo / 5, 8);
            bool comboUp = newMultiplier > multiplier;
            multiplier = newMultiplier;
            int points = e.Score * multiplier * (goldTimer > 0f ? 2 : 1);
            score += points;
            hud.SetScore(score, multiplier, comboUp);
            EnemyKilled?.Invoke();

            if (e.Kind == EnemyKind.Boss)
            {
                boss = null;
                bossesDefeated++;
                bossTimer = cfg.shooterBossEvery;
                hud.ShowBossBar(false, 0f);
                hud.Toast("BOSS DOWN!", Pal.Gold);
                for (int i = 0; i < 5; i++) fx.Death(e.P + Random.insideUnitCircle * 1.2f, 1);
                fx.Shake(0.6f, 0.6f);
                hud.Flash(Color.white, 0.7f);
                sound.BigExplode();
                foreach (var s in enemyShots) Kill(s);
                foreach (var st in strikes) { st.Alive = false; st.R.enabled = false; }
                for (int i = 0; i < 30; i++) Drop(PickupKind.Bone, e.P + Random.insideUnitCircle * 1.5f);
                Drop(RandomWeaponCapsule(), e.P + Vector2.left * 0.7f);
                Drop(RandomSupport(), e.P + Vector2.right * 0.7f);
                Drop(PickupKind.Heart, e.P + Vector2.down * 0.6f);
                fx.Float("+" + points, Pal.Gold, e.P, 1.8f);
                return;
            }

            AddSkillCharge(1f);
            fx.Burst(e.P, Pal.Orange, Pal.Gold, 16, 6f, 0f, false, 0.45f);
            fx.Float("+" + points, Pal.White, e.P + Vector2.up * 0.4f, 1f);
            sound.Explode();
            int boneDrops = e.Kind == EnemyKind.Rock ? Random.Range(3, 6) : e.Kind == EnemyKind.Saucer ? Random.Range(2, 4) : Random.Range(0, 2);
            for (int i = 0; i < boneDrops; i++) Drop(PickupKind.Bone, e.P + Random.insideUnitCircle * 0.4f);

            float roll = Random.value;
            if (power < 4 && roll < cfg.shooterPowerDropChance) Drop(PickupKind.Power, e.P);
            else if ((roll -= cfg.shooterPowerDropChance) < 0.04f) Drop(RandomWeaponCapsule(), e.P);
            else if ((roll -= 0.04f) < 0.04f) Drop(RandomSupport(), e.P);
            else if (hearts < maxHearts && (roll -= 0.04f) < cfg.shooterHeartDropChance) Drop(PickupKind.Heart, e.P);
        }

        static PickupKind RandomWeaponCapsule()
        {
            float r = Random.value;
            return r < 0.34f ? PickupKind.WeaponHoming : r < 0.67f ? PickupKind.WeaponPlasma : PickupKind.WeaponWave;
        }

        static PickupKind RandomSupport()
        {
            switch (Random.Range(0, 5))
            {
                case 0: return PickupKind.Magnet;
                case 1: return PickupKind.Shield;
                case 2: return PickupKind.Wingman;
                case 3: return PickupKind.Rapid;
                default: return PickupKind.Bomb;
            }
        }

        Sprite PickupSprite(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.Bone: return art.Coin[0];
                case PickupKind.Power: return art.PowerCapsule;
                case PickupKind.Heart: return art.HeartPickup;
                case PickupKind.Magnet: return art.IconMagnet;
                case PickupKind.Shield: return art.IconShieldItem;
                case PickupKind.Wingman: return art.IconWingman;
                case PickupKind.Rapid: return art.IconRapid;
                case PickupKind.Bomb: return art.IconBomb;
                case PickupKind.WeaponHoming: return art.CapsuleHoming;
                case PickupKind.WeaponPlasma: return art.CapsulePlasma;
                default: return art.CapsuleWave;
            }
        }

        void Drop(PickupKind kind, Vector2 position)
        {
            Pickup p = null;
            foreach (var candidate in pickups)
                if (!candidate.Alive) { p = candidate; break; }
            if (p == null)
            {
                p = new Pickup { R = NewRenderer("Pickup", 6) };
                pickups.Add(p);
            }
            p.Kind = kind;
            p.P = position;
            p.T = Random.value * 3f;
            p.Alive = true;
            p.R.enabled = true;
            p.R.sprite = PickupSprite(kind);
            p.R.transform.localScale = Vector3.one;
            Place(p.R, p.P);
        }

        void Collect(PickupKind kind, Vector2 at)
        {
            switch (kind)
            {
                case PickupKind.Bone:
                    int amount = (luckyBone ? 2 : 1) * (goldTimer > 0f ? 2 : 1);
                    for (int i = 0; i < amount; i++)
                    {
                        bones++;
                        BoneCollected?.Invoke();
                    }
                    boneChain++;
                    boneChainTimer = 0.6f;
                    fx.CoinPickup(at);
                    sound.Coin(boneChain);
                    break;
                case PickupKind.Power:
                    power = Mathf.Min(4, power + 1);
                    hud.SetWeapon(WeaponName, power);
                    hud.Toast(power >= 4 ? "MAX POWER!" : "POWER UP!", Pal.Orange);
                    fx.PerfectRing(at, Pal.Orange);
                    sound.PowerUp();
                    break;
                case PickupKind.Heart:
                    hearts = Mathf.Min(maxHearts, hearts + 1);
                    hud.SetHearts(hearts, maxHearts);
                    fx.Burst(at, Pal.Red, Pal.White, 12, 4f, 0f, false, 0.4f);
                    sound.SkillReady();
                    break;
                case PickupKind.Magnet:
                    magnetTimer = 10f;
                    Announce("MAGNET!", Pal.Red, at);
                    break;
                case PickupKind.Shield:
                    shieldHits = 1;
                    bird.SetShield(true);
                    Announce("SHIELD!", Pal.Hex("3ff0ff"), at);
                    break;
                case PickupKind.Wingman:
                    wingmanTimer = Mathf.Max(wingmanTimer, 10f);
                    Announce("WINGMEN!", Art.PelletColor, at);
                    break;
                case PickupKind.Rapid:
                    rapidTimer = 8f;
                    Announce("RAPID FIRE!", Pal.Gold, at);
                    break;
                case PickupKind.Bomb:
                    bombs++;
                    hud.SetBombs(TotalBombs);
                    Announce("+1 BOMB", Pal.Orange, at);
                    break;
                case PickupKind.WeaponHoming:
                case PickupKind.WeaponPlasma:
                case PickupKind.WeaponWave:
                    weapon = kind == PickupKind.WeaponHoming ? Weapon.Homing : kind == PickupKind.WeaponPlasma ? Weapon.Plasma : Weapon.Wave;
                    hud.SetWeapon(WeaponName, power);
                    Color32 c = kind == PickupKind.WeaponHoming ? Art.HomingColor : kind == PickupKind.WeaponPlasma ? Art.PlasmaColor : Art.WaveColor;
                    Announce(WeaponName + "!", c, at);
                    fx.PerfectRing(at, c);
                    break;
            }
            UpdateBuffIcons();
        }

        void Announce(string text, Color32 color, Vector2 at)
        {
            hud.Toast(text, color);
            fx.Burst(at, color, Pal.White, 10, 4f, 0f, false, 0.35f);
            sound.PowerUp();
        }

        void PlayerHit()
        {
            if (invulnerable > 0f || dying || iceTimer > 0f) return;
            if (shieldHits > 0)
            {
                shieldHits = 0;
                bird.SetShield(false);
                invulnerable = 1f;
                bird.StartInvulnerable(1f);
                fx.ShieldPop(shipPos);
                sound.ShieldPop();
                UpdateBuffIcons();
                return;
            }

            hearts--;
            hud.SetHearts(hearts, maxHearts);
            combo = 0;
            multiplier = 1;
            hud.ComboBreak();
            hud.SetScore(score, 1, false);
            fx.Shake(0.35f, 0.3f);
            hud.Flash(Pal.Red, 0.45f);
            sound.Hit();
            foreach (var s in enemyShots)
                if (s.Alive && (s.P - shipPos).sqrMagnitude < 9f) Kill(s); // mercy: no chained hits

            if (hearts > 0)
            {
                invulnerable = cfg.shooterInvulnerable;
                bird.StartInvulnerable(invulnerable);
                power = Mathf.Max(1, power - 1);
                if (weapon != Weapon.Blaster)
                {
                    weapon = Weapon.Blaster; // special weapons are lost on a hit
                    hud.Toast("WEAPON LOST", Pal.Red);
                }
                hud.SetWeapon(WeaponName, power);
                return;
            }

            dying = true;
            endTimer = 1.3f;
            for (int i = 0; i < 3; i++) fx.Death(shipPos + Random.insideUnitCircle * 0.5f, -1);
            fx.Shake(0.6f, 0.5f);
            sound.BigExplode();
            megaLaser.enabled = wingmanLeft.enabled = wingmanRight.enabled = false;
            bird.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- background (vertical space scroll)

        void BuildBackground()
        {
            var space = System.Array.Find(art.Biomes, b => b.Hazard == BiomeHazard.Meteors) ?? art.Biomes[art.Biomes.Length - 1];
            var sky = NewRenderer("Sky", -100);
            sky.drawMode = SpriteDrawMode.Tiled;
            int rows = Mathf.CeilToInt((World.Top - World.Bottom + 1f) * World.PPU);
            sky.sprite = Art.BuildSky(rows, space.Sky);
            sky.size = new Vector2(Mathf.Max(World.CoverWidth, 40f), rows / World.PPU);
            sky.transform.localPosition = new Vector3(0f, World.Top, 0f);

            planet = NewRenderer("Planet", -92);
            planet.sprite = space.Sun;
            planet.transform.localScale = Vector3.one * 2f;
            planet.transform.localPosition = new Vector3(1.8f, World.Top - 4f, 0f);

            for (int i = 0; i < 3; i++)
            {
                var n = NewRenderer("Nebula", -95);
                n.sprite = art.Clouds[i % art.Clouds.Length];
                n.color = new Color(0.65f, 0.45f, 1f, 0.35f);
                n.transform.localScale = Vector3.one * 2.5f;
                n.transform.localPosition = new Vector3(Random.Range(-4f, 4f), Random.Range(World.Bottom, World.Top), 0f);
                nebulas.Add((n, 0.4f));
            }

            float[] speeds = { 0.8f, 2f, 4.5f };
            for (int i = 0; i < 90; i++)
            {
                int layer = i % 3;
                var r = NewRenderer("Star", -94 + layer);
                r.sprite = layer == 2 ? art.StarBig : art.StarSmall;
                r.color = new Color(1f, 1f, 1f, 0.4f + 0.3f * layer);
                r.transform.localPosition = new Vector3(Random.Range(-20f, 20f), Random.Range(World.Bottom, World.Top + 1f), 0f);
                stars.Add((r, speeds[layer]));
            }
        }

        void TickBackground(float dt)
        {
            float top = World.Top + 1f;
            float warp = Warped ? 0.3f : 1f;
            foreach (var (r, speed) in stars)
            {
                var p = r.transform.localPosition;
                p.y -= speed * warp * dt;
                if (p.y < World.Bottom - 0.5f) p.y = top;
                r.transform.localPosition = p;
            }
            foreach (var (r, speed) in nebulas)
            {
                var p = r.transform.localPosition;
                p.y -= speed * warp * dt;
                if (p.y < World.Bottom - 3f) p = new Vector3(Random.Range(-4f, 4f), World.Top + 3f, 0f);
                r.transform.localPosition = p;
            }
            var pp = planet.transform.localPosition;
            pp.y -= 0.15f * dt;
            if (pp.y < World.Bottom - 3f) pp = new Vector3(Random.Range(-2.5f, 2.5f), World.Top + 3f, 0f);
            planet.transform.localPosition = pp;
        }

        // ---------------------------------------------------------------- pooling helpers

        Shot GetShot(List<Shot> list, int order)
        {
            foreach (var s in list)
                if (!s.Alive)
                {
                    s.Alive = true;
                    s.R.enabled = true;
                    return s;
                }
            var shot = new Shot { R = NewRenderer("Shot", order), Alive = true };
            list.Add(shot);
            return shot;
        }

        static void Kill(Shot s)
        {
            s.Alive = false;
            s.R.enabled = false;
        }

        static void Remove(Enemy e)
        {
            e.Alive = false;
            e.R.enabled = false;
            e.BarBack.enabled = e.BarFill.enabled = false;
        }

        SpriteRenderer NewRenderer(string rendererName, int order)
        {
            var go = new GameObject(rendererName);
            go.transform.SetParent(root, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            return r;
        }

        static void Place(SpriteRenderer r, Vector2 p) => r.transform.localPosition = new Vector3(p.x, p.y, 0f);
    }
}
