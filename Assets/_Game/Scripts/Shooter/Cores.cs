using System.Collections.Generic;
using UnityEngine;

namespace TapOrDrag
{
    public enum CoreKind { Power, Homing, Plasma, Wave, Damage, Rapid, Wingman, Magnet, Heart, ShieldRegen, Skill, Bomb, Lucky, Orbit }

    /// <summary>
    /// CORE RUN (roguelike DOG BLAST): the cores offered between waves. Each run starts weak; the player picks
    /// one of three cores after every wave and they stack into that run's build. Texts are pixel-font safe.
    /// </summary>
    public static class CoreDefs
    {
        public static readonly string[] Names =
        {
            "POWER CORE", "HOMING CORE", "PLASMA CORE", "WAVE CORE", "FANG CORE", "RAPID CORE", "WINGMAN CORE",
            "MAGNET CORE", "HEART CORE", "SHIELD CORE", "SKILL CORE", "BOMB CORE", "LUCKY CORE", "ORBIT CORE",
        };

        public static readonly string[] Descriptions =
        {
            "MORE BULLETS", "MISSILES SEEK CATS", "ORBS PIERCE CATS", "WIDE WAVE SHOTS", "+25% DAMAGE", "+15% FIRE RATE",
            "2 HELPER DRONES", "PULL ALL BONES", "+1 MAX HEART + HEAL", "SHIELD EVERY 20S", "+30% SKILL CHARGE",
            "+2 BOMBS", "+50% BONES", "+1 ORBITING ORB",
        };

        /// <summary>How many times each core can be taken in one run (weapons are swaps, bombs are unlimited).</summary>
        public static readonly int[] MaxStacks = { 3, 1, 1, 1, 5, 5, 1, 1, 3, 1, 3, 99, 3, 3 };

        public static readonly Color32[] Colors =
        {
            Pal.Orange, Art.HomingColor, Art.PlasmaColor, Art.WaveColor, Pal.Red, Pal.Gold, Art.PelletColor,
            Pal.Red, Pal.Rose, Pal.Hex("3ff0ff"), Art.PortalMain, Pal.OrangeLight, Pal.Gold, Art.PlasmaColor,
        };

        public static Sprite Icon(Art art, CoreKind core)
        {
            switch (core)
            {
                case CoreKind.Power: return art.PowerCapsule;
                case CoreKind.Homing: return art.CapsuleHoming;
                case CoreKind.Plasma: return art.CapsulePlasma;
                case CoreKind.Wave: return art.CapsuleWave;
                case CoreKind.Damage: return art.IconPaw;
                case CoreKind.Rapid: return art.IconRapid;
                case CoreKind.Wingman: return art.IconWingman;
                case CoreKind.Magnet: return art.IconMagnet;
                case CoreKind.Heart: return art.IconHeart;
                case CoreKind.ShieldRegen: return art.IconShieldItem;
                case CoreKind.Skill: return art.IconWarp;
                case CoreKind.Bomb: return art.IconBomb;
                case CoreKind.Lucky: return art.IconLucky;
                default: return art.PlasmaOrb;
            }
        }

        /// <summary>Three distinct random cores from those still eligible (fewer if the pool runs dry).</summary>
        public static List<CoreKind> Roll(System.Func<CoreKind, bool> eligible, int count = 3)
        {
            var pool = new List<CoreKind>();
            for (int i = 0; i < Names.Length; i++)
                if (eligible((CoreKind)i)) pool.Add((CoreKind)i);
            var picks = new List<CoreKind>();
            while (picks.Count < count && pool.Count > 0)
            {
                int index = Random.Range(0, pool.Count);
                picks.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picks;
        }
    }
}
