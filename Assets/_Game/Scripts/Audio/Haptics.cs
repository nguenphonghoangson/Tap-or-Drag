using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace TapOrDrag
{
    public enum HapticType { Selection, Light, Medium, Heavy, Soft, Rigid, Success, Warning, Error }

    /// <summary>
    /// Haptic feedback. iOS uses the UIKit feedback generators (Plugins/iOS/TapOrDragHaptics.mm); Android uses
    /// VibrationEffect (predefined effects on API 29+, short one-shots on 26+, Handheld.Vibrate below). No-op in the
    /// editor. Rate-limited so bursts (coins, explosions) do not blur into one long buzz. Toggle persists in PlayerPrefs.
    /// </summary>
    public static class Haptics
    {
        const string EnabledKey = "TapOrDrag.Haptics";
        const float GlobalGap = 0.03f;

        static readonly float[] MinGap = { 0.06f, 0.05f, 0.05f, 0.08f, 0.05f, 0.05f, 0.25f, 0.25f, 0.25f };
        static readonly float[] LastTime = new float[MinGap.Length];
        static float lastAny = -1f;
        static bool loaded, enabled = true;

        public static bool Enabled
        {
            get
            {
                if (!loaded)
                {
                    enabled = PlayerPrefs.GetInt(EnabledKey, 1) == 1;
                    loaded = true;
                }
                return enabled;
            }
            set
            {
                enabled = value;
                loaded = true;
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Plays a haptic; <paramref name="intensity"/> scales impact types (0..1).</summary>
        public static void Play(HapticType type, float intensity = 1f)
        {
            if (!Enabled) return;
            float now = Time.unscaledTime;
            int index = (int)type;
            if (now - LastTime[index] < MinGap[index] || now - lastAny < GlobalGap) return;
            LastTime[index] = now;
            lastAny = now;
            PlatformPlay(type, Mathf.Clamp01(intensity));
        }

#if UNITY_IOS && !UNITY_EDITOR
        static bool prepared;

        [DllImport("__Internal")] static extern void TapOrDrag_HapticsPrepare();
        [DllImport("__Internal")] static extern void TapOrDrag_HapticImpact(int style, float intensity);
        [DllImport("__Internal")] static extern void TapOrDrag_HapticSelection();
        [DllImport("__Internal")] static extern void TapOrDrag_HapticNotification(int type);

        static void PlatformPlay(HapticType type, float intensity)
        {
            if (!prepared)
            {
                TapOrDrag_HapticsPrepare();
                prepared = true;
            }
            switch (type)
            {
                case HapticType.Selection: TapOrDrag_HapticSelection(); break;
                case HapticType.Light: TapOrDrag_HapticImpact(0, intensity); break;
                case HapticType.Medium: TapOrDrag_HapticImpact(1, intensity); break;
                case HapticType.Heavy: TapOrDrag_HapticImpact(2, intensity); break;
                case HapticType.Soft: TapOrDrag_HapticImpact(3, intensity); break;
                case HapticType.Rigid: TapOrDrag_HapticImpact(4, intensity); break;
                case HapticType.Success: TapOrDrag_HapticNotification(0); break;
                case HapticType.Warning: TapOrDrag_HapticNotification(1); break;
                default: TapOrDrag_HapticNotification(2); break;
            }
        }
#elif UNITY_ANDROID && !UNITY_EDITOR
        static bool prepared;
        static AndroidJavaObject vibrator;
        static AndroidJavaClass effectClass;
        static int sdk;

        static void PlatformPlay(HapticType type, float intensity)
        {
            if (!prepared)
            {
                prepared = true;
                try
                {
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    if (sdk >= 26) effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Haptics] Android vibrator unavailable: " + e.Message);
                    vibrator = null;
                }
            }
            if (vibrator == null) return;

            try
            {
                if (sdk >= 29 && PredefinedEffect(type, out int effect))
                {
                    using (var vibration = effectClass.CallStatic<AndroidJavaObject>("createPredefined", effect))
                        vibrator.Call("vibrate", vibration);
                }
                else if (sdk >= 26)
                {
                    OneShot(type, intensity, out long ms, out int amplitude);
                    using (var vibration = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        vibrator.Call("vibrate", vibration);
                }
                else if (type == HapticType.Heavy || type == HapticType.Error)
                {
                    Handheld.Vibrate(); // old devices: only the strongest events (also makes Unity add the VIBRATE permission)
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] " + e.Message);
                vibrator = null;
            }
        }

        // VibrationEffect.EFFECT_CLICK = 0, EFFECT_DOUBLE_CLICK = 1, EFFECT_TICK = 2, EFFECT_HEAVY_CLICK = 5
        static bool PredefinedEffect(HapticType type, out int effect)
        {
            switch (type)
            {
                case HapticType.Selection: case HapticType.Light: case HapticType.Soft: effect = 2; return true;
                case HapticType.Medium: case HapticType.Rigid: effect = 0; return true;
                case HapticType.Heavy: effect = 5; return true;
                case HapticType.Success: case HapticType.Warning: effect = 1; return true;
                default: effect = 0; return false; // error: custom longer buzz below
            }
        }

        static void OneShot(HapticType type, float intensity, out long ms, out int amplitude)
        {
            switch (type)
            {
                case HapticType.Selection: ms = 8; amplitude = 60; break;
                case HapticType.Light: case HapticType.Soft: ms = 12; amplitude = 90; break;
                case HapticType.Medium: case HapticType.Rigid: ms = 18; amplitude = 150; break;
                case HapticType.Heavy: ms = 30; amplitude = 255; break;
                case HapticType.Error: ms = 90; amplitude = 255; break;
                default: ms = 40; amplitude = 180; break;
            }
            amplitude = Mathf.Clamp(Mathf.RoundToInt(amplitude * Mathf.Lerp(0.5f, 1f, intensity)), 1, 255);
        }
#else
        static void PlatformPlay(HapticType type, float intensity) { }
#endif
    }
}
