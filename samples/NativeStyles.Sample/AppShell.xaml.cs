namespace NativeStyles.Sample;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
	}

	void OnClassicNavigationClicked(object? sender, EventArgs e)
	{
		if (Application.Current?.Windows.FirstOrDefault() is { } window)
			window.Page = new Pages.ClassicTabbedPage();
	}

	// Application.UserAppTheme: on Android the library recreates the activity so native views are re-themed too
	void OnToggleThemeClicked(object? sender, EventArgs e)
	{
		FlyoutIsPresented = false;
		if (Application.Current is { } app)
			app.UserAppTheme = app.RequestedTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
	}

	void OnClassicTopTabsClicked(object? sender, EventArgs e)
	{
		if (Application.Current?.Windows.FirstOrDefault() is { } window)
			window.Page = new Pages.ClassicTabbedPage(topTabs: true);
	}
}
