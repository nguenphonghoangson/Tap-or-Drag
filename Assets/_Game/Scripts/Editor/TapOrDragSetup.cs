using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TapOrDrag.EditorTools
{
    /// <summary>
    /// One-time project setup for the prototype: creates the tunable GameConfig asset, switches the player to portrait,
    /// registers the scene in Build Settings and selects a 405x820 Game view.
    /// </summary>
    [InitializeOnLoad]
    static class TapOrDragSetup
    {
        const string GameFolder = "Assets/_Game";
        const string ConfigFolder = GameFolder + "/Resources";
        const string ConfigPath = ConfigFolder + "/GameConfig.asset";
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const int ViewWidth = 405, ViewHeight = 820;

        static TapOrDragSetup()
        {
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureConfig();
            bool changed = EnsurePortrait() | EnsureBuildScene();
            if (changed || !SessionState.GetBool("TapOrDrag.SettingsSaved", false))
            {
                SessionState.SetBool("TapOrDrag.SettingsSaved", true);
                AssetDatabase.SaveAssets();
            }
            if (!SessionState.GetBool("TapOrDrag.GameViewSet", false))
            {
                SessionState.SetBool("TapOrDrag.GameViewSet", true);
                SetGameView(false);
            }
        }

        [MenuItem("Tap Or Drag/Select Game Config")]
        static void SelectConfig()
        {
            EnsureConfig();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        }

        [MenuItem("Tap Or Drag/Game View 405x820 (Portrait)")]
        static void MenuGameView() => SetGameView(true);

        [MenuItem("Tap Or Drag/Reset Progress (best, coins, skins, missions, hangar)")]
        static void ResetBest()
        {
            foreach (var key in new[] { "TapOrDrag.Best", "TapOrDrag.GatesCleared", "TapOrDrag.Skin", "TapOrDrag.Runs", "TapOrDrag.RedGatesPassed" })
                PlayerPrefs.DeleteKey(key);
            Economy.ResetAll();
            Missions.ResetAll();
            ShipUpgrades.ResetAll();
            Inventory.ResetAll();
            PlayerPrefs.DeleteKey("TapOrDrag.BestShooter");
            PlayerPrefs.DeleteKey("TapOrDrag.BestCores");
            PlayerPrefs.Save();
            Debug.Log("[TapOrDrag] Progress reset (best score, coins, owned skins, missions, tutorial).");
        }

        [MenuItem("Tap Or Drag/Debug: +1000 Coins")]
        static void GiveCoins()
        {
            if (Application.isPlaying)
            {
                Economy.Add(1000);
                Economy.Save();
            }
            else
            {
                // Economy is not loaded outside Play mode: touch only the coin key so owned skins are not overwritten.
                PlayerPrefs.SetInt("TapOrDrag.Coins", PlayerPrefs.GetInt("TapOrDrag.Coins", 0) + 1000);
                PlayerPrefs.Save();
            }
            Debug.Log("[TapOrDrag] +1000 coins (shown after the next title screen refresh).");
        }

        [MenuItem("Tap Or Drag/Playtest Summary")]
        static void PlaytestSummary() => Debug.Log(PlaytestStats.Summary());

        [MenuItem("Tap Or Drag/Reveal Playtest Log")]
        static void RevealPlaytestLog()
        {
            if (File.Exists(PlaytestStats.FilePath)) EditorUtility.RevealInFinder(PlaytestStats.FilePath);
            else Debug.Log("[TapOrDrag] No playtest log yet: " + PlaytestStats.FilePath);
        }

        static void EnsureConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null) return;
            if (!AssetDatabase.IsValidFolder(GameFolder)) AssetDatabase.CreateFolder("Assets", "_Game");
            if (!AssetDatabase.IsValidFolder(ConfigFolder)) AssetDatabase.CreateFolder(GameFolder, "Resources");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), ConfigPath);
            AssetDatabase.SaveAssets();
        }

        static bool EnsurePortrait()
        {
            bool changed = false;
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                changed = true;
            }
            if (PlayerSettings.defaultScreenWidth != ViewWidth || PlayerSettings.defaultScreenHeight != ViewHeight)
            {
                PlayerSettings.defaultScreenWidth = ViewWidth;
                PlayerSettings.defaultScreenHeight = ViewHeight;
                changed = true;
            }
            if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed)
            {
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                changed = true;
            }
            return changed;
        }

        static bool EnsureBuildScene()
        {
            if (EditorBuildSettings.scenes.Length > 0 || !File.Exists(ScenePath)) return false;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return true;
        }

        /// <summary>Adds and selects a fixed 405x820 Game view size. Uses internal editor API, so failures are only logged.</summary>
        static void SetGameView(bool openIfMissing)
        {
            try
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var asm = typeof(Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var sizeKind = asm.GetType("UnityEditor.GameViewSizeType");
                var gameViewType = asm.GetType("UnityEditor.GameView");

                var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", all).GetValue(null);
                var group = sizesType.GetProperty("currentGroup", all).GetValue(instance);
                var groupType = group.GetType();
                var getCount = groupType.GetMethod("GetTotalCount", all);
                var getSize = groupType.GetMethod("GetGameViewSize", all);
                var widthProp = sizeType.GetProperty("width", all);
                var heightProp = sizeType.GetProperty("height", all);

                int count = (int)getCount.Invoke(group, null);
                int index = -1;
                for (int i = 0; i < count; i++)
                {
                    var size = getSize.Invoke(group, new object[] { i });
                    if ((int)widthProp.GetValue(size) == ViewWidth && (int)heightProp.GetValue(size) == ViewHeight)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    var ctor = sizeType.GetConstructor(new[] { sizeKind, typeof(int), typeof(int), typeof(string) });
                    var newSize = ctor.Invoke(new[] { Enum.Parse(sizeKind, "FixedResolution"), ViewWidth, ViewHeight, (object)"Tap Or Drag Portrait" });
                    groupType.GetMethod("AddCustomSize", all).Invoke(group, new[] { newSize });
                    sizesType.GetMethod("SaveToHDD", all)?.Invoke(instance, null);
                    index = (int)getCount.Invoke(group, null) - 1;
                }

                var views = Resources.FindObjectsOfTypeAll(gameViewType);
                if (views.Length == 0 && openIfMissing) views = new UnityEngine.Object[] { EditorWindow.GetWindow(gameViewType) };
                foreach (var view in views)
                {
                    var prop = gameViewType.GetProperty("selectedSizeIndex", all);
                    if (prop != null && prop.CanWrite) prop.SetValue(view, index);
                    else gameViewType.GetMethod("SizeSelectionCallback", all)?.Invoke(view, new object[] { index, null });
                    ((EditorWindow)view).Repaint();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[TapOrDrag] Could not set the Game view to 405x820 automatically (" + e.Message +
                                 "). Pick a portrait resolution in the Game view dropdown instead.");
            }
        }
    }
}
