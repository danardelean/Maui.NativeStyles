using Foundation;

namespace NativeStyles.DeviceTests;

/// <summary>
/// UIScene lifecycle (UIApplicationSceneManifest in Info.plist): required by the iOS 27 SDK (Xcode 27), where an app
/// without a scene delegate is terminated at launch. MauiUISceneDelegate creates the window and raises the Scene* lifecycle events.
/// </summary>
[Register("SceneDelegate")]
public class SceneDelegate : MauiUISceneDelegate
{
}
