namespace NativeStyles.Sample;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	// Plain template code. When the theme changes on Android the library recreates the activity and moves the current
	// root page over to the new window, so navigation state survives even though this method builds a new AppShell.
	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
