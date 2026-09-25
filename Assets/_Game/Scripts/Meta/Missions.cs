using System;
using UnityEngine;

namespace TapOrDrag
{
    public enum MissionType { PassPipes, BreakGates, StompEnemies, Perfects, Fevers, ScoreInRun, CollectCoins, CloseCalls, ReachCombo, PassRedGates, GravityFlips, SwitchWalls }

    public sealed class MissionDef
    {
        public MissionType Type;
        public int Target, Reward;
        public string Label; // pixel-font safe, keep label + progress within ~17 characters
    }

    /// <summary>
    /// Three daily missions picked (deterministically per date) from a pool. Cumulative counters add up across runs;
    /// "best in a run" types (score, combo) record the highest value. Completing one pays coins immediately.
    /// </summary>
    public sealed class Missions
    {
        const string SaveKey = "TapOrDrag.Missions";
        public const int Count = 3;

        public static readonly MissionDef[] Pool =
        {
            new MissionDef { Type = MissionType.PassPipes, Target = 30, Reward = 50, Label = "PASS PIPES" },
            new MissionDef { Type = MissionType.PassPipes, Target = 80, Reward = 90, Label = "PASS PIPES" },
            new MissionDef { Type = MissionType.BreakGates, Target = 10, Reward = 60, Label = "BREAK GATES" },
            new MissionDef { Type = MissionType.StompEnemies, Target = 5, Reward = 60, Label = "STOMP ENEMY" },
            new MissionDef { Type = MissionType.Perfects, Target = 5, Reward = 70, Label = "GET PERFECT" },
            new MissionDef { Type = MissionType.Fevers, Target = 2, Reward = 80, Label = "REACH FEVER" },
            new MissionDef { Type = MissionType.ScoreInRun, Target = 100, Reward = 60, Label = "RUN SCORE" },
            new MissionDef { Type = MissionType.ScoreInRun, Target = 250, Reward = 100, Label = "RUN SCORE" },
            new MissionDef { Type = MissionType.CollectCoins, Target = 40, Reward = 50, Label = "GET BONES" },
            new MissionDef { Type = MissionType.CloseCalls, Target = 5, Reward = 70, Label = "CLOSE CALLS" },
            new MissionDef { Type = MissionType.ReachCombo, Target = 6, Reward = 60, Label = "COMBO X" },
            new MissionDef { Type = MissionType.PassRedGates, Target = 5, Reward = 60, Label = "RED GATES" },
            new MissionDef { Type = MissionType.GravityFlips, Target = 4, Reward = 60, Label = "GRAVITY FLIPS" },
            new MissionDef { Type = MissionType.SwitchWalls, Target = 6, Reward = 60, Label = "SWITCH WALLS" },
        };

        [Serializable]
        sealed class SaveData
        {
            public string date;
            public int[] ids;
            public int[] progress;
            public bool[] done;
        }

        SaveData data;

        public event Action<MissionDef> Completed;

        public MissionDef Get(int slot) => Pool[data.ids[slot]];
        public int Progress(int slot) => data.progress[slot];
        public bool Done(int slot) => data.done[slot];

        public string ProgressText(int slot)
        {
            var def = Get(slot);
            return Mathf.Min(data.progress[slot], def.Target) + "/" + def.Target;
        }

        public void Load()
        {
            string today = DateTime.Now.ToString("yyyyMMdd");
            try { data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey, "")); }
            catch (ArgumentException) { data = null; }
            if (data != null && data.date == today && data.ids != null && data.ids.Length == Count) return;

            // New day: pick 3 missions of different types, seeded by the date so a reinstall on the same day matches.
            var rng = new System.Random(int.Parse(today));
            data = new SaveData { date = today, ids = new int[Count], progress = new int[Count], done = new bool[Count] };
            for (int slot = 0; slot < Count; slot++)
            {
                int pick;
                do pick = rng.Next(Pool.Length);
                while (TypeTaken(pick, slot));
                data.ids[slot] = pick;
            }
            Save();
        }

        bool TypeTaken(int pick, int filledSlots)
        {
            for (int i = 0; i < filledSlots; i++)
                if (Pool[data.ids[i]].Type == Pool[pick].Type) return true;
            return false;
        }

        /// <summary>Cumulative progress (pipes, gates, coins...).</summary>
        public void Add(MissionType type, int amount = 1)
        {
            for (int slot = 0; slot < Count; slot++)
                if (!data.done[slot] && Get(slot).Type == type) SetProgress(slot, data.progress[slot] + amount);
        }

        /// <summary>Best-in-a-run progress (score, combo).</summary>
        public void Record(MissionType type, int value)
        {
            for (int slot = 0; slot < Count; slot++)
                if (!data.done[slot] && Get(slot).Type == type && value > data.progress[slot]) SetProgress(slot, value);
        }

        void SetProgress(int slot, int value)
        {
            data.progress[slot] = value;
            if (value < Get(slot).Target) return;
            data.done[slot] = true;
            Save();
            Completed?.Invoke(Get(slot));
        }

        public void Save() => PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));

        public static void ResetAll() => PlayerPrefs.DeleteKey(SaveKey);
    }
}
