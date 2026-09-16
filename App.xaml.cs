namespace MauiNativeStyle;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// One dictionary set per platform: the other platform's XAML is never compiled in.
#if IOS
		Resources.MergedDictionaries.Add(new Resources.Styles.iOS.iOSColors());
		Resources.MergedDictionaries.Add(new Resources.Styles.iOS.iOSTypography());
		Resources.MergedDictionaries.Add(new Resources.Styles.iOS.iOSStyles());
#elif ANDROID
		Resources.MergedDictionaries.Add(new Resources.Styles.Android.MaterialColors());
		Resources.MergedDictionaries.Add(new Resources.Styles.Android.MaterialTypography());
		Resources.MergedDictionaries.Add(new Resources.Styles.Android.MaterialStyles());
#endif
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
