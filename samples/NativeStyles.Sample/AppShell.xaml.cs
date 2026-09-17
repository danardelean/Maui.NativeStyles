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
}
