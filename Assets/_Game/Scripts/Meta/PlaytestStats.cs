using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TapOrDrag
{
    /// <summary>
    /// Lightweight playtest log: one CSV line per finished run in Application.persistentDataPath.
    /// Use the editor menu "Tap Or Drag/Playtest Summary" to aggregate (deaths by cause, run length, score).
    /// </summary>
    public static class PlaytestStats
    {
        const string Header = "timestamp,skin,duration_s,score,clears,death_cause,fevers,perfects,close_calls,coins";

        public struct Run
        {
            public string Skin, DeathCause;
            public float Duration;
            public int Score, Clears, Fevers, Perfects, CloseCalls, Coins;
        }

        public static string FilePath => Path.Combine(Application.persistentDataPath, "playtest_runs.csv");

        public static void Log(Run run)
        {
            try
            {
                bool newFile = !File.Exists(FilePath);
                var line = string.Join(",",
                    System.DateTime.Now.ToString("s", CultureInfo.InvariantCulture), run.Skin,
                    run.Duration.ToString("F1", CultureInfo.InvariantCulture), run.Score, run.Clears, run.DeathCause,
                    run.Fevers, run.Perfects, run.CloseCalls, run.Coins);
                File.AppendAllText(FilePath, (newFile ? Header + "\n" : "") + line + "\n");
            }
            catch (IOException e)
            {
                Debug.LogWarning("[TapOrDrag] Could not write playtest log: " + e.Message);
            }
        }

        public static string Summary()
        {
            if (!File.Exists(FilePath)) return "[TapOrDrag] No playtest runs logged yet (" + FilePath + ").";
            var rows = File.ReadAllLines(FilePath).Skip(1).Select(l => l.Split(',')).Where(c => c.Length >= 10).ToList();
            if (rows.Count == 0) return "[TapOrDrag] Playtest log is empty.";

            float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
            var sb = new StringBuilder();
            sb.AppendLine($"[TapOrDrag] Playtest summary — {rows.Count} runs ({FilePath})");
            sb.AppendLine($"  avg run: {rows.Average(r => F(r[2])):F1}s   median score: {Median(rows.Select(r => F(r[3])))}   " +
                          $"avg clears: {rows.Average(r => F(r[4])):F1}");
            sb.AppendLine($"  per run: fevers {rows.Average(r => F(r[6])):F2}, perfects {rows.Average(r => F(r[7])):F2}, " +
                          $"close calls {rows.Average(r => F(r[8])):F2}, coins {rows.Average(r => F(r[9])):F1}");
            sb.AppendLine("  deaths by cause:");
            foreach (var g in rows.GroupBy(r => r[5]).OrderByDescending(g => g.Count()))
                sb.AppendLine($"    {g.Key,-16} {g.Count(),4}  ({g.Count() * 100f / rows.Count:F0}%)");
            sb.AppendLine("  runs by skin:");
            foreach (var g in rows.GroupBy(r => r[1]).OrderByDescending(g => g.Count()))
                sb.AppendLine($"    {g.Key,-16} {g.Count(),4}  avg score {g.Average(r => F(r[3])):F0}");
            return sb.ToString();
        }

        static float Median(IEnumerable<float> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5f;
        }
    }
}
