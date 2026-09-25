using UnityEngine;

namespace TapOrDrag
{
    /// <summary>Tiny chiptune synthesizer: square/triangle/sine/saw/noise voices with pitch sweeps, used for all SFX and the music loop.</summary>
    public static class Synth
    {
        public const int Rate = 22050;

        public enum Wave { Square, Triangle, Sine, Saw, Noise }

        public static float[] Tone(float duration, float fromHz, float toHz, Wave wave, float volume,
            float duty = 0.5f, float decay = 1f, float attack = 0.004f, float curve = 1f,
            float vibratoHz = 0f, float vibratoDepth = 0f)
        {
            int n = Mathf.Max(1, (int)(duration * Rate));
            var buf = new float[n];
            double phase = 0;
            uint seed = 0x9E3779B9u ^ (uint)n;
            float held = NextNoise(ref seed);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate, u = (float)i / n;
                float f = Mathf.Lerp(fromHz, toHz, Mathf.Pow(u, curve));
                if (vibratoHz > 0f) f *= 1f + vibratoDepth * Mathf.Sin(2f * Mathf.PI * vibratoHz * t);
                phase += f / Rate;
                if (phase >= 1.0)
                {
                    phase -= System.Math.Floor(phase);
                    if (wave == Wave.Noise) held = NextNoise(ref seed);
                }
                float p = (float)phase, s;
                switch (wave)
                {
                    case Wave.Square: s = p < duty ? 1f : -1f; break;
                    case Wave.Triangle: s = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                    case Wave.Sine: s = Mathf.Sin(2f * Mathf.PI * p); break;
                    case Wave.Saw: s = 2f * p - 1f; break;
                    default: s = held; break;
                }
                float env = Mathf.Min(1f, t / attack) * Mathf.Pow(1f - u, decay);
                buf[i] = s * env * volume;
            }
            return buf;
        }

        public static float[] Seq(params (float at, float[] data)[] parts)
        {
            int length = 0;
            foreach (var part in parts) length = Mathf.Max(length, (int)(part.at * Rate) + part.data.Length);
            var buf = new float[length];
            foreach (var part in parts) Add(buf, (int)(part.at * Rate), part.data);
            return buf;
        }

        public static float[] Arp(float noteDuration, float[] freqs, Wave wave, float volume, float duty = 0.5f, float decay = 1f)
        {
            var parts = new (float, float[])[freqs.Length];
            for (int i = 0; i < freqs.Length; i++)
                parts[i] = (i * noteDuration, Tone(noteDuration, freqs[i], freqs[i], wave, volume, duty, decay));
            return Seq(parts);
        }

        public static AudioClip Clip(string clipName, float[] data)
        {
            for (int i = 0; i < data.Length; i++) data[i] = (float)System.Math.Tanh(data[i] * 1.1f);
            var clip = AudioClip.Create(clipName, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>8-bar loop in A minor (Am F C G), 128 BPM: triangle bass, square arp, lead melody, noise drums.</summary>
        public static float[] Music()
        {
            const int bpm = 128, bars = 8;
            int step = (int)(Rate * 60f / bpm / 4f);
            float stepSec = (float)step / Rate;
            var buf = new float[step * 16 * bars];

            int[] bass = { 45, 41, 48, 43, 45, 41, 48, 40 };
            int[][] chords =
            {
                new[] { 69, 72, 76 }, new[] { 65, 69, 72 }, new[] { 67, 72, 76 }, new[] { 67, 71, 74 },
                new[] { 69, 72, 76 }, new[] { 65, 69, 72 }, new[] { 67, 72, 76 }, new[] { 68, 71, 76 },
            };
            int[][] melody =
            {
                new[] { 76, 0, 74, 76, 79, 0, 76, 74 }, new[] { 72, 0, 72, 74, 76, 0, 72, 0 },
                new[] { 67, 0, 72, 76, 79, 81, 79, 76 }, new[] { 74, 0, 71, 74, 79, 0, 0, 0 },
                new[] { 76, 0, 74, 76, 79, 0, 81, 79 }, new[] { 77, 0, 76, 74, 72, 0, 69, 0 },
                new[] { 67, 0, 72, 76, 79, 0, 76, 72 }, new[] { 71, 74, 76, 74, 71, 0, 68, 0 },
            };
            int[] arpOrder = { 0, 1, 2, 1 };

            for (int bar = 0; bar < bars; bar++)
            for (int s = 0; s < 16; s++)
            {
                int at = (bar * 16 + s) * step;
                if (s % 2 == 0)
                {
                    float f = Midi(bass[bar] + (s % 4 == 2 ? 12 : 0));
                    Add(buf, at, Tone(stepSec * 1.8f, f, f, Wave.Triangle, 0.28f, decay: 0.5f));
                    int m = melody[bar][s / 2];
                    if (m > 0)
                    {
                        float mf = Midi(m);
                        Add(buf, at, Tone(stepSec * 1.7f, mf, mf, Wave.Square, 0.075f, 0.5f, 0.7f, vibratoHz: 6f, vibratoDepth: 0.006f));
                    }
                }
                float af = Midi(chords[bar][arpOrder[s % 4]] + 12);
                Add(buf, at, Tone(stepSec * 0.95f, af, af, Wave.Square, 0.03f, 0.25f, 2f));

                if (s % 8 == 0 || s == 11) Add(buf, at, Tone(0.16f, 150f, 42f, Wave.Sine, 0.55f, decay: 1.5f, curve: 0.4f));
                if (s % 8 == 4)
                {
                    Add(buf, at, Tone(0.13f, 5000f, 3000f, Wave.Noise, 0.22f, decay: 2f));
                    Add(buf, at, Tone(0.08f, 220f, 160f, Wave.Triangle, 0.18f));
                }
                if (s % 2 == 1) Add(buf, at, Tone(0.035f, 9000f, 9000f, Wave.Noise, 0.06f, decay: 3f));
            }
            return buf;
        }

        static void Add(float[] dst, int at, float[] src)
        {
            int n = Mathf.Min(src.Length, dst.Length - at);
            for (int i = 0; i < n; i++) dst[at + i] += src[i];
        }

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        static float NextNoise(ref uint s)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return (s & 0xFFFF) / 32767.5f - 1f;
        }
    }
}
