// UIScene life-cycle bridge for Unity 2022.3 built with the iOS 27 SDK.
//
// Apps built with the iOS 27 SDK must adopt the scene-based life cycle, otherwise UIKit refuses to launch them
// ("UIScene life cycle is required for apps built with this SDK"). Unity 2022.3's iOS trampoline only implements the
// app-delegate life cycle, so this file:
//   1. subclasses UnityAppController and defers the engine start from didFinishLaunching (no window scene is
//      connected yet at that point) to the first sceneDidBecomeActive. That reuses Unity's own deferred-init path in
//      applicationDidBecomeActive (the one used when the app is launched in the background), and by then the scene
//      is foreground-active, so UnityAppController's pickStartupWindowScene attaches the window to it;
//   2. provides the scene delegate, which forwards scene events to the matching UnityAppController callbacks
//      (all of them already guard on _unityAppReady).
// Info.plist gets the matching UIApplicationSceneManifest from Assets/_Game/Scripts/Editor/IosSceneLifecyclePostBuild.cs.
// Remove both files once the project moves to a Unity version with native UIScene support.

#import <UIKit/UIKit.h>
#include "UnityAppController.h"

@interface TapOrDragSceneDelegate : UIResponder<UIWindowSceneDelegate>
@property (strong, nonatomic) UIWindow* window;
@end

@implementation TapOrDragSceneDelegate

- (void)attachUnityWindowToScene:(UIScene*)scene
{
    if (![scene isKindOfClass: [UIWindowScene class]])
        return;
    UIWindow* unityWindow = GetAppController().window;
    if (unityWindow == nil)
        return;
    if (unityWindow.windowScene != scene)
        unityWindow.windowScene = (UIWindowScene*)scene;
    self.window = unityWindow;
}

- (void)scene:(UIScene*)scene willConnectToSession:(UISceneSession*)session options:(UISceneConnectionOptions*)connectionOptions
{
    ::printf("-> scene:willConnectToSession:\n");
    [self attachUnityWindowToScene: scene];
}

- (void)sceneWillEnterForeground:(UIScene*)scene
{
    [GetAppController() applicationWillEnterForeground: UIApplication.sharedApplication];
}

- (void)sceneDidBecomeActive:(UIScene*)scene
{
    // The first activation starts the engine; later ones resume it.
    [GetAppController() applicationDidBecomeActive: UIApplication.sharedApplication];
    [self attachUnityWindowToScene: scene];
}

- (void)sceneWillResignActive:(UIScene*)scene
{
    [GetAppController() applicationWillResignActive: UIApplication.sharedApplication];
}

- (void)sceneDidEnterBackground:(UIScene*)scene
{
    [GetAppController() applicationDidEnterBackground: UIApplication.sharedApplication];
}

@end


@interface TapOrDragAppController : UnityAppController
@end

@implementation TapOrDragAppController

- (BOOL)application:(UIApplication*)application didFinishLaunchingWithOptions:(NSDictionary*)launchOptions
{
    ::printf("-> applicationDidFinishLaunching() [UIScene life cycle: engine starts on first sceneDidBecomeActive]\n");

    // Same device setup as UnityAppController, minus initUnityWithApplication (see header comment).
    if ([UIDevice currentDevice].generatesDeviceOrientationNotifications == NO)
        [[UIDevice currentDevice] beginGeneratingDeviceOrientationNotifications];
    return YES;
}

- (UISceneConfiguration*)application:(UIApplication*)application
    configurationForConnectingSceneSession:(UISceneSession*)connectingSceneSession
    options:(UISceneConnectionOptions*)options
{
    UISceneConfiguration* configuration = [[UISceneConfiguration alloc] initWithName: @"Default Configuration"
                                                                        sessionRole: connectingSceneSession.role];
    configuration.delegateClass = [TapOrDragSceneDelegate class];
    return configuration;
}

@end

IMPL_APP_CONTROLLER_SUBCLASS(TapOrDragAppController)
