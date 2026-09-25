using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TapOrDrag
{
    /// <summary>
    /// DOG BLAST: vertical shooter mode. Drag anywhere to move the dog's saucer (relative to the finger), it fires
    /// automatically. Cats attack in waves (drones, saucers that aim, divers, rocks) with a mothership boss every
    /// <see cref="GameConfig.shooterBossEvery"/> seconds. Enemies drop golden bones, power capsules and hearts.
    /// Everything is pooled SpriteRenderers under one root that is hidden outside the mode.
    /// </summary>
    public class ShooterGame : MonoBehaviour
    {
        enum EnemyKind { Drone, Saucer, Diver, Rock, Boss }
        enum PickupKind { Bone, Power, Heart }

        sealed class Shot
        {
            public SpriteRenderer R;
            public Vector2 P, V;
            public bool Alive;
        }

        sealed class Enemy
        {
            public SpriteRenderer R;
            public EnemyKind Kind;
            public Vector2 P;
            public float Hp, MaxHp, T, FireT, Radius, BaseX, TargetY, Flash;
            public Vector2 DiveVelocity;
            public int Score, Pattern;
            public bool Alive, Diving;
        }

        sealed class Pickup
        {
            public SpriteRenderer R;
            public PickupKind Kind;
            public Vector2 P;
            public float T;
            public bool Alive;
        }

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
        readonly List<(SpriteRenderer r, float speed)> stars = new List<(SpriteRenderer, float)>();
        readonly List<(SpriteRenderer r, float speed)> nebulas = new List<(SpriteRenderer, float)>();
        SpriteRenderer planet;

        Vector2 shipPos, lastPointer;
        bool dragging, dying;
        int pointerId = -1, hearts, power, score, bones, combo, multiplier, bossesDefeated, shotCount;
        float comboTimer, fireTimer, invulnerable, time, spawnTimer, bossTimer, endTimer, velocityX, hitSoundCooldown, boneChainTimer;
        int boneChain;
        Enemy boss;

        float MinX => -World.ViewWidth * 0.5f + 0.55f;
        float MaxX => World.ViewWidth * 0.5f - 0.55f;
        float MinY => World.Bottom + 1.2f;
        float MaxY => World.Top - 3.2f;

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
            root.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- lifecycle

        public void Begin()
        {
            Running = true;
            dying = false;
            root.gameObject.SetActive(true);
            ClearEntities();

            shipPos = new Vector2(0f, World.Bottom + 3f);
            bird.gameObject.SetActive(true);
            bird.ResetAt(shipPos.y);
            bird.TickShooter(0f, shipPos, 0f);
            hearts = cfg.shooterHearts;
            power = 1;
            score = bones = combo = bossesDefeated = shotCount = 0;
            multiplier = 1;
            time = 0f;
            spawnTimer = 1.5f;
            bossTimer = cfg.shooterBossEvery;
            invulnerable = 0f;
            dragging = false;
            boss = null;

            hud.SetShooterHud(true);
            hud.SetHearts(hearts, cfg.shooterMaxHearts);
            hud.SetPower(power);
            hud.ShowBossBar(false, 0f);
            hud.SetScore(0, 1, false);
        }

        public void End()
        {
            Running = false;
            ClearEntities();
            root.gameObject.SetActive(false);
            hud.SetShooterHud(false);
            hud.ShowBossBar(false, 0f);
        }

        void ClearEntities()
        {
            foreach (var s in shots) Kill(s);
            foreach (var s in enemyShots) Kill(s);
            foreach (var e in enemies) { e.Alive = false; e.R.enabled = false; }
            foreach (var p in pickups) { p.Alive = false; p.R.enabled = false; }
        }

        public void Tick(float dt)
        {
            time += dt;
            TickBackground(dt);
            if (dying)
            {
                TickShots(dt);
                TickEnemies(dt);
                endTimer -= dt;
                if (endTimer <= 0f)
                {
                    Running = false;
                    Finished?.Invoke(score, bones);
                }
                return;
            }

            TickInput(dt);
            bird.TickShooter(dt, shipPos, velocityX);
            TickFire(dt);
            TickSpawning(dt);
            TickShots(dt);
            TickEnemies(dt);
            TickPickups(dt);
            TickCollisions();

            if (invulnerable > 0f) invulnerable -= dt;
            if (hitSoundCooldown > 0f) hitSoundCooldown -= dt;
            boneChainTimer -= dt;
            if (boneChainTimer <= 0f) boneChain = 0;
            comboTimer -= dt;
            if (comboTimer <= 0f && combo > 0)
            {
                combo = 0;
                multiplier = 1;
                hud.SetScore(score, 1, false);
            }
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

        // ---------------------------------------------------------------- firing

        void TickFire(float dt)
        {
            fireTimer -= dt;
            if (fireTimer > 0f) return;
            fireTimer = cfg.shooterFireInterval;
            var muzzle = shipPos + new Vector2(0.05f, 0.7f);
            switch (power)
            {
                case 1:
                    FireShot(muzzle, 0f);
                    break;
                case 2:
                    FireShot(muzzle + new Vector2(-0.18f, 0f), 0f);
                    FireShot(muzzle + new Vector2(0.18f, 0f), 0f);
                    break;
                case 3:
                    FireShot(muzzle, 0f);
                    FireShot(muzzle, -10f);
                    FireShot(muzzle, 10f);
                    break;
                default:
                    FireShot(muzzle, 0f);
                    FireShot(muzzle, -8f);
                    FireShot(muzzle, 8f);
                    FireShot(muzzle, -18f);
                    FireShot(muzzle, 18f);
                    break;
            }
            if (++shotCount % 2 == 0) sound.Shoot();
        }

        void FireShot(Vector2 from, float angleDegrees)
        {
            var s = GetShot(shots, art.PlayerPellet, 8);
            float a = angleDegrees * Mathf.Deg2Rad;
            s.P = from;
            s.V = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * cfg.shooterBulletSpeed;
            s.R.transform.localRotation = Quaternion.Euler(0f, 0f, -angleDegrees);
            Place(s.R, s.P);
        }

        void EnemyFire(Vector2 from, Vector2 direction, float speed)
        {
            var s = GetShot(enemyShots, art.EnemyOrb, 9);
            s.P = from;
            s.V = direction.normalized * speed;
            Place(s.R, s.P);
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
                e = new Enemy { R = NewRenderer("Enemy", 5) };
                enemies.Add(e);
            }
            e.Kind = kind;
            e.P = position;
            e.BaseX = position.x;
            e.Hp = e.MaxHp = Mathf.Ceil(hp);
            e.T = 0f;
            e.FireT = Random.Range(0.8f, 1.6f);
            e.Flash = 0f;
            e.Diving = false;
            e.Pattern = 0;
            e.Alive = true;
            e.R.enabled = true;
            e.R.color = Color.white;
            e.R.transform.localScale = Vector3.one;
            e.R.transform.localRotation = Quaternion.identity;
            switch (kind)
            {
                case EnemyKind.Drone: e.R.sprite = art.CatDrone; e.Radius = 0.38f; e.Score = 10; break;
                case EnemyKind.Saucer: e.R.sprite = art.CatSaucer; e.Radius = 0.55f; e.Score = 40; e.TargetY = World.Top - Random.Range(3.2f, 5.5f); break;
                case EnemyKind.Diver: e.R.sprite = art.Spiky[0]; e.Radius = 0.45f; e.Score = 25; e.TargetY = World.Top - Random.Range(2.2f, 3.5f); break;
                case EnemyKind.Rock: e.R.sprite = art.Meteor; e.Radius = 0.5f; e.Score = 30; break;
                case EnemyKind.Boss:
                    e.R.sprite = art.CatBoss;
                    e.R.transform.localScale = Vector3.one * 3f;
                    e.Radius = 1.35f;
                    e.Score = 1000;
                    e.TargetY = World.Top - 3.4f;
                    e.BaseX = 0f;
                    break;
            }
            Place(e.R, e.P);
            return e;
        }

        void SpawnBoss()
        {
            boss = SpawnEnemy(EnemyKind.Boss, new Vector2(0f, World.Top + 2.5f), cfg.shooterBossHp + cfg.shooterBossHpGrowth * bossesDefeated);
            hud.ShowBossBar(true, 1f);
            hud.Toast("CAT MOTHERSHIP!", Pal.Rose);
            sound.BossAlarm();
            fx.Shake(0.2f, 0.4f);
        }

        // ---------------------------------------------------------------- simulation

        void TickShots(float dt)
        {
            TickShotList(shots, dt);
            TickShotList(enemyShots, dt);
        }

        static void TickShotList(List<Shot> list, float dt)
        {
            const float margin = 1.5f;
            foreach (var s in list)
            {
                if (!s.Alive) continue;
                s.P += s.V * dt;
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
                if (e.Flash > 0f)
                {
                    e.Flash -= dt;
                    e.R.color = e.Flash > 0f ? new Color(1f, 0.55f, 0.55f) : Color.white;
                }
                switch (e.Kind)
                {
                    case EnemyKind.Drone:
                        e.P.y -= 2.4f * dt;
                        e.P.x = e.BaseX + Mathf.Sin(e.T * 3f) * 0.6f;
                        break;
                    case EnemyKind.Saucer:
                        if (e.P.y > e.TargetY && e.T < 9f) e.P.y = Mathf.MoveTowards(e.P.y, e.TargetY, 3f * dt);
                        else if (e.T >= 9f) e.P.y -= 2f * dt; // leaves after a while
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
                if (e.P.y < World.Bottom - 2f || e.P.y > World.Top + 4f || Mathf.Abs(e.P.x) > World.CamHalfWidth + 3f)
                {
                    e.Alive = false;
                    e.R.enabled = false;
                }
            }
        }

        void TickBoss(Enemy e, float dt)
        {
            if (e.P.y > e.TargetY)
            {
                e.P.y = Mathf.MoveTowards(e.P.y, e.TargetY, 1.5f * dt);
                return;
            }
            e.P.x = Mathf.Sin(e.T * 1.25f) * 2.2f;
            if (dying || (e.FireT -= dt) > 0f) return;
            e.FireT = 1.8f;
            var mouth = e.P + Vector2.down * 1.1f;
            float speed = cfg.shooterEnemyBulletSpeed;
            switch (e.Pattern++ % 3)
            {
                case 0: // fan
                    for (int i = -3; i <= 3; i++)
                    {
                        float a = (-90f + i * 15f) * Mathf.Deg2Rad;
                        EnemyFire(mouth, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), speed);
                    }
                    break;
                case 1: // ring burst
                    for (int i = 0; i < 12; i++)
                    {
                        float a = (i * 30f + e.T * 40f) * Mathf.Deg2Rad;
                        EnemyFire(mouth, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), speed * 0.8f);
                    }
                    break;
                default: // aimed triple
                    var aim = (shipPos - mouth).normalized;
                    for (int i = -1; i <= 1; i++)
                        EnemyFire(mouth, Quaternion.Euler(0f, 0f, i * 8f) * aim, speed * 1.2f);
                    break;
            }
        }

        void TickPickups(float dt)
        {
            foreach (var p in pickups)
            {
                if (!p.Alive) continue;
                p.T += dt;
                var toShip = shipPos - p.P;
                if (toShip.sqrMagnitude < 4f) p.P = Vector2.MoveTowards(p.P, shipPos, 9f * dt); // magnet
                else p.P += new Vector2(Mathf.Sin(p.T * 3f) * 0.4f, -1.3f) * dt;
                Place(p.R, p.P);
                p.R.transform.localRotation = p.Kind == PickupKind.Bone ? Quaternion.Euler(0f, 0f, Mathf.Sin(p.T * 5f) * 18f) : Quaternion.identity;
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
                    if (!e.Alive) continue;
                    float rr = e.Radius + 0.1f;
                    if ((s.P - e.P).sqrMagnitude > rr * rr) continue;
                    Kill(s);
                    Damage(e, 1f);
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
            e.Hp -= amount;
            e.Flash = 0.06f;
            if (hitSoundCooldown <= 0f)
            {
                sound.EnemyHit();
                hitSoundCooldown = 0.05f;
            }
            if (e.Kind == EnemyKind.Boss) hud.ShowBossBar(true, Mathf.Clamp01(e.Hp / e.MaxHp));
            if (e.Hp <= 0f) Destroy(e);
        }

        void Destroy(Enemy e)
        {
            e.Alive = false;
            e.R.enabled = false;
            combo++;
            comboTimer = 1.2f;
            int newMultiplier = Mathf.Min(1 + combo / 5, 8);
            bool comboUp = newMultiplier > multiplier;
            multiplier = newMultiplier;
            int points = e.Score * multiplier;
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
                for (int i = 0; i < 30; i++) Drop(PickupKind.Bone, e.P + Random.insideUnitCircle * 1.5f);
                Drop(PickupKind.Power, e.P);
                Drop(PickupKind.Heart, e.P + Vector2.right * 0.6f);
                fx.Float("+" + points, Pal.Gold, e.P, 1.8f);
                return;
            }

            fx.Burst(e.P, Pal.Orange, Pal.Gold, 16, 6f, 0f, false, 0.45f);
            fx.Float("+" + points, Pal.White, e.P + Vector2.up * 0.4f, 1f);
            sound.Explode();
            int boneDrops = e.Kind == EnemyKind.Rock ? Random.Range(3, 6) : e.Kind == EnemyKind.Saucer ? Random.Range(2, 4) : Random.Range(0, 2);
            for (int i = 0; i < boneDrops; i++) Drop(PickupKind.Bone, e.P + Random.insideUnitCircle * 0.4f);
            if (power < 4 && Random.value < cfg.shooterPowerDropChance) Drop(PickupKind.Power, e.P);
            else if (hearts < cfg.shooterMaxHearts && Random.value < cfg.shooterHeartDropChance) Drop(PickupKind.Heart, e.P);
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
            p.R.sprite = kind == PickupKind.Bone ? art.Coin[0] : kind == PickupKind.Power ? art.PowerCapsule : art.HeartPickup;
            Place(p.R, p.P);
        }

        void Collect(PickupKind kind, Vector2 at)
        {
            switch (kind)
            {
                case PickupKind.Bone:
                    bones++;
                    boneChain++;
                    boneChainTimer = 0.6f;
                    fx.CoinPickup(at);
                    sound.Coin(boneChain);
                    BoneCollected?.Invoke();
                    break;
                case PickupKind.Power:
                    power = Mathf.Min(4, power + 1);
                    hud.SetPower(power);
                    hud.Toast(power >= 4 ? "MAX POWER!" : "POWER UP!", Pal.Orange);
                    fx.PerfectRing(at, Pal.Orange);
                    sound.PowerUp();
                    break;
                case PickupKind.Heart:
                    hearts = Mathf.Min(cfg.shooterMaxHearts, hearts + 1);
                    hud.SetHearts(hearts, cfg.shooterMaxHearts);
                    fx.Burst(at, Pal.Red, Pal.White, 12, 4f, 0f, false, 0.4f);
                    sound.SkillReady();
                    break;
            }
        }

        void PlayerHit()
        {
            if (invulnerable > 0f || dying) return;
            hearts--;
            hud.SetHearts(hearts, cfg.shooterMaxHearts);
            combo = 0;
            multiplier = 1;
            hud.ComboBreak();
            hud.SetScore(score, 1, false);
            fx.Shake(0.35f, 0.3f);
            hud.Flash(Pal.Red, 0.45f);
            sound.Hit();
            // Mercy: clear nearby bullets so one mistake does not chain into another.
            foreach (var s in enemyShots)
                if (s.Alive && (s.P - shipPos).sqrMagnitude < 9f) Kill(s);

            if (hearts > 0)
            {
                invulnerable = cfg.shooterInvulnerable;
                bird.StartInvulnerable(invulnerable);
                power = Mathf.Max(1, power - 1); // losing a heart also costs a power level
                hud.SetPower(power);
                return;
            }

            dying = true;
            endTimer = 1.3f;
            for (int i = 0; i < 3; i++) fx.Death(shipPos + Random.insideUnitCircle * 0.5f, -1);
            fx.Shake(0.6f, 0.5f);
            sound.BigExplode();
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
            foreach (var (r, speed) in stars)
            {
                var p = r.transform.localPosition;
                p.y -= speed * dt;
                if (p.y < World.Bottom - 0.5f) p.y = top;
                r.transform.localPosition = p;
            }
            foreach (var (r, speed) in nebulas)
            {
                var p = r.transform.localPosition;
                p.y -= speed * dt;
                if (p.y < World.Bottom - 3f) p = new Vector3(Random.Range(-4f, 4f), World.Top + 3f, 0f);
                r.transform.localPosition = p;
            }
            var pp = planet.transform.localPosition;
            pp.y -= 0.15f * dt;
            if (pp.y < World.Bottom - 3f) pp = new Vector3(Random.Range(-2.5f, 2.5f), World.Top + 3f, 0f);
            planet.transform.localPosition = pp;
        }

        // ---------------------------------------------------------------- pooling helpers

        Shot GetShot(List<Shot> list, Sprite sprite, int order)
        {
            foreach (var s in list)
                if (!s.Alive)
                {
                    s.Alive = true;
                    s.R.enabled = true;
                    return s;
                }
            var shot = new Shot { R = NewRenderer("Shot", order), Alive = true };
            shot.R.sprite = sprite;
            list.Add(shot);
            return shot;
        }

        static void Kill(Shot s)
        {
            s.Alive = false;
            s.R.enabled = false;
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
