using DeviceRunners.VisualRunners;

namespace NativeStyles.DeviceTests;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseVisualTestRunner(configuration => configuration
				// Under `dotnet test` (DeviceRunners.Testing.Targets) the CLI passes its settings through environment
				// variables: run everything at startup, stream the results back over TCP, then exit. No-op otherwise.
				.AddCliConfiguration()
				.AddConsoleResultChannel()
				.AddTestAssembly(typeof(MauiProgram).Assembly)
				.AddXunit())
			// No options: the tests check the platform defaults (no brand, no dynamic colors)
			.UseNativeStyles();
		return builder.Build();
	}
}
