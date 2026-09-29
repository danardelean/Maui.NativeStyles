using Android.App;
using Android.Content.PM;

namespace NativeStyles.DeviceTests;

[Activity(Name = "it.mahiz.nativestyles.devicetests.MainActivity", Theme = "@style/Maui.Material3.Theme.NoActionBar", MainLauncher = true,
	ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
