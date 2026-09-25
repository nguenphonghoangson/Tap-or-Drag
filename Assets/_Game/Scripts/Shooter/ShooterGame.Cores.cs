using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TapOrDrag
{
    /// <summary>
    /// CORE RUN: roguelike variant of DOG BLAST. Enemies come in waves; after each wave the game pauses and the
    /// player picks one of three cores (weapons, stat boosts, orbiting orbs...) that stack for the rest of the run.
    /// Every fourth wave is a boss. Random weapon / support drops are off in this mode: the build comes from cores.
    /// </summary>
    public partial class ShooterGame
    {
        enum WavePhase { Spawning, Clearing, Boss }

        const float WaveDuration = 20f, ClearTimeout = 6f, ClearMinDelay = 1.2f, ShieldRegenSeconds = 20f;
        const int BossEveryWaves = 4, HeartCap = 8;

        bool coreMode, choosing, coreWingmen, coreMagnet, coreShieldRegen, coreEventsHooked;
        int wave, orbitCount;
        WavePhase wavePhase;
        float waveTimer, shieldRegenTimer, boneBonus;
        float baseDamage, baseFireInterval, baseKillsForSkill;
        readonly int[] coreStacks = new int[CoreDefs.Names.Length];
        List<CoreKind> offer = new List<CoreKind>();
        readonly List<SpriteRenderer> orbs = new List<SpriteRenderer>();

        public int Wave => wave;

        void BeginCores()
        {
            if (!coreEventsHooked)
            {
                hud.CoreChosen += OnCoreChosen;
                coreEventsHooked = true;
            }
            choosing = coreWingmen = coreMagnet = coreShieldRegen = false;
            Array.Clear(coreStacks, 0, coreStacks.Length);
            wave = orbitCount = 0;
            boneBonus = 0f;
            baseDamage = damageMultiplier;
            baseFireInterval = fireIntervalMultiplier;
            baseKillsForSkill = killsForSkill;
            maxHearts = Mathf.Min(maxHearts, HeartCap);
            hud.HideCoreChoice();
            hud.SetWave(0);
            if (coreMode) StartWave();
        }

        void StartWave()
        {
            wave++;
            hud.SetWave(wave);
            if (wave % BossEveryWaves == 0)
            {
                wavePhase = WavePhase.Boss;
                SpawnBoss();
                return;
            }
            wavePhase = WavePhase.Spawning;
            waveTimer = WaveDuration;
            spawnTimer = 1.2f;
            hud.Toast("WAVE " + wave, Pal.OrangeLight);
        }

        void TickWaves(float dt)
        {
            float difficulty = 6f + wave * 9f; // same ramp as the endless mode, stepped per wave
            switch (wavePhase)
            {
                case WavePhase.Spawning:
                    waveTimer -= dt;
                    TickSpawnTimer(dt, difficulty);
                    if (waveTimer <= 0f)
                    {
                        wavePhase = WavePhase.Clearing;
                        waveTimer = ClearTimeout;
                    }
                    break;
                case WavePhase.Clearing:
                    waveTimer -= dt;
                    if ((waveTimer <= ClearTimeout - ClearMinDelay && !AnyEnemyAlive()) || waveTimer <= 0f) OfferCores();
                    break;
                case WavePhase.Boss:
                    if (boss != null) TickSpawnTimer(dt, difficulty); // light escorts while the boss is up
                    break;
            }
        }

        void OnCoreBossDefeated()
        {
            wavePhase = WavePhase.Clearing;
            waveTimer = ClearTimeout;
        }

        bool AnyEnemyAlive()
        {
            foreach (var e in enemies)
                if (e.Alive) return true;
            return false;
        }

        // ---------------------------------------------------------------- choosing

        void OfferCores()
        {
            // Wave over: clear hostile fire, vacuum up the remaining bones, pause and show three cards.
            foreach (var s in enemyShots) Kill(s);
            foreach (var st in strikes) { st.Alive = false; st.R.enabled = false; }
            foreach (var e in enemies) Remove(e);
            foreach (var p in pickups)
            {
                if (!p.Alive) continue;
                p.Alive = false;
                p.R.enabled = false;
                if (p.Kind == PickupKind.Bone || p.Kind == PickupKind.Heart) Collect(p.Kind, p.P);
            }

            offer = CoreDefs.Roll(IsEligible);
            if (offer.Count == 0)
            {
                StartWave();
                return;
            }
            choosing = true;
            dragging = false;
            laserTimer = 0f;
            megaLaser.enabled = false;
            hud.ShowCoreChoice(wave, offer, coreStacks);
            sound.Mission();
        }

        bool IsEligible(CoreKind core)
        {
            if (coreStacks[(int)core] >= CoreDefs.MaxStacks[(int)core]) return false;
            switch (core)
            {
                case CoreKind.Power: return power < 4;
                case CoreKind.Homing: return weapon != Weapon.Homing;
                case CoreKind.Plasma: return weapon != Weapon.Plasma;
                case CoreKind.Wave: return weapon != Weapon.Wave;
                case CoreKind.Lightning: return weapon != Weapon.Lightning;
                case CoreKind.Boomerang: return weapon != Weapon.Boomerang;
                case CoreKind.Heart: return maxHearts < HeartCap;
                default: return true;
            }
        }

        void TickChoosing()
        {
            TickShip(0f); // keep the engine flickering behind the cards
            for (int i = 0; i < offer.Count; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) OnCoreChosen(i);
        }

        void OnCoreChosen(int index)
        {
            if (!Running || !choosing || index < 0 || index >= offer.Count) return;
            ApplyCore(offer[index]);
            choosing = false;
            hud.HideCoreChoice();
            invulnerable = Mathf.Max(invulnerable, 1f); // short grace as the next wave arrives
            StartWave();
        }

        void ApplyCore(CoreKind core)
        {
            int stacks = ++coreStacks[(int)core];
            switch (core)
            {
                case CoreKind.Power:
                    power = Mathf.Min(4, power + 1);
                    break;
                case CoreKind.Homing: weapon = Weapon.Homing; break;
                case CoreKind.Plasma: weapon = Weapon.Plasma; break;
                case CoreKind.Wave: weapon = Weapon.Wave; break;
                case CoreKind.Lightning: weapon = Weapon.Lightning; break;
                case CoreKind.Boomerang: weapon = Weapon.Boomerang; break;
                case CoreKind.Damage: damageMultiplier = baseDamage + 0.25f * stacks; break;
                case CoreKind.Rapid: fireIntervalMultiplier = baseFireInterval / (1f + 0.15f * stacks); break;
                case CoreKind.Wingman: coreWingmen = true; break;
                case CoreKind.Magnet: coreMagnet = true; break;
                case CoreKind.Heart:
                    maxHearts = Mathf.Min(HeartCap, maxHearts + 1);
                    hearts = maxHearts;
                    hud.SetHearts(hearts, maxHearts);
                    break;
                case CoreKind.ShieldRegen:
                    coreShieldRegen = true;
                    shieldHits = 1;
                    shieldRegenTimer = ShieldRegenSeconds;
                    break;
                case CoreKind.Skill: killsForSkill = Mathf.Max(5f, baseKillsForSkill / (1f + 0.3f * stacks)); break;
                case CoreKind.Bomb:
                    bombs += 2;
                    hud.SetBombs(TotalBombs);
                    break;
                case CoreKind.Lucky: boneBonus = 0.5f * stacks; break;
                case CoreKind.Orbit: orbitCount = stacks; break;
            }
            hud.SetWeapon(WeaponName, power);
            Color32 color = CoreDefs.Colors[(int)core];
            hud.Toast(CoreDefs.Names[(int)core] + "!", color);
            fx.PerfectRing(shipPos, color);
            fx.Burst(shipPos, color, Pal.White, 16, 5f, 0f, false, 0.45f);
            sound.PowerUp();
            UpdateBuffIcons();
        }

        /// <summary>Extra bones from the lucky core; the fractional part is rolled.</summary>
        int BonusBones(int amount)
        {
            if (!coreMode || boneBonus <= 0f) return 0;
            float extra = amount * boneBonus;
            int whole = (int)extra;
            if (Random.value < extra - whole) whole++;
            return whole;
        }

        // ---------------------------------------------------------------- per-frame core effects

        void TickCores(float dt)
        {
            if (!coreMode) return;
            if (coreShieldRegen && shieldHits == 0)
            {
                shieldRegenTimer -= dt;
                if (shieldRegenTimer <= 0f)
                {
                    shieldHits = 1;
                    shieldRegenTimer = ShieldRegenSeconds;
                    fx.PerfectRing(shipPos, Pal.Hex("3ff0ff"));
                    sound.ShieldPop();
                    UpdateBuffIcons();
                }
            }
            TickOrbits(dt);
        }

        /// <summary>Orbit core: plasma orbs circle the jet, grinding down cats and eating bullets they touch.</summary>
        void TickOrbits(float dt)
        {
            while (orbs.Count < orbitCount)
            {
                var r = NewRenderer("Orb", 9);
                r.sprite = art.PlasmaOrb;
                orbs.Add(r);
            }
            for (int i = 0; i < orbs.Count; i++)
            {
                bool on = i < orbitCount && !dying;
                orbs[i].enabled = on;
                if (!on) continue;
                float angle = time * 3.2f + i * Mathf.PI * 2f / orbitCount;
                var p = shipPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.5f;
                Place(orbs[i], p);
                foreach (var e in enemies)
                {
                    if (!e.Alive) continue;
                    float rr = e.Radius + 0.35f;
                    if ((e.P - p).sqrMagnitude < rr * rr) Damage(e, 10f * damageMultiplier * dt);
                }
                foreach (var s in enemyShots)
                    if (s.Alive && (s.P - p).sqrMagnitude < 0.16f) Kill(s);
            }
        }

        void HideCoreVisuals()
        {
            foreach (var orb in orbs) orb.enabled = false;
            choosing = false;
            if (hud != null) hud.HideCoreChoice();
        }
    }
}
