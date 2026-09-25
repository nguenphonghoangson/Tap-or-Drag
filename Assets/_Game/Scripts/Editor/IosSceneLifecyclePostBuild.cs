#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace TapOrDrag.EditorTools
{
    /// <summary>
    /// iOS 27 SDK requires the UIScene life cycle. Adds the UIApplicationSceneManifest to the generated Info.plist,
    /// pointing at the scene delegate in Assets/Plugins/iOS/TapOrDragSceneLifecycle.mm (which Unity adds to the
    /// UnityFramework target automatically). Runs on every iOS build, so Append builds stay correct.
    /// </summary>
    public static class IosSceneLifecyclePostBuild
    {
        const string SceneDelegateClass = "TapOrDragSceneDelegate";
        const string NativeBridge = "Assets/Plugins/iOS/TapOrDragSceneLifecycle.mm";

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;

            if (!File.Exists(NativeBridge))
            {
                Debug.LogError("[TapOrDrag] " + NativeBridge + " is missing: the app would fail to launch with the scene manifest " +
                               "but no scene delegate. Info.plist left unchanged.");
                return;
            }

            string plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            var manifest = plist.root.CreateDict("UIApplicationSceneManifest"); // replaces any previous value
            manifest.SetBoolean("UIApplicationSupportsMultipleScenes", false);
            var configuration = manifest.CreateDict("UISceneConfigurations")
                .CreateArray("UIWindowSceneSessionRoleApplication")
                .AddDict();
            configuration.SetString("UISceneConfigurationName", "Default Configuration");
            configuration.SetString("UISceneDelegateClassName", SceneDelegateClass);

            plist.WriteToFile(plistPath);
            Debug.Log("[TapOrDrag] iOS: added UIApplicationSceneManifest (" + SceneDelegateClass + ") to Info.plist.");
        }
    }
}
#endif
