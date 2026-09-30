using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using NativeStyles.Sample.Automation;

namespace NativeStyles.Sample;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// nativestyles:// automation links (Automation/AutomationLinks.cs); not BROWSABLE, so web pages cannot open them
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault }, DataScheme = AutomationLinks.Scheme)]
public class MainActivity : MauiAppCompatActivity
{
}
