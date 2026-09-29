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
#if IOS
		RunnerButtonShim.Register();
#endif
		return builder.Build();
	}
}

#if IOS
/// <summary>
/// The library's title font transformer writes into the attribute dictionary UIKit passes in, which can be immutable:
/// the runner's own buttons then crash the app at launch. Until that is fixed (see iOSButtonTests
/// .Title_font_transformer_does_not_write_into_the_attributes_it_receives), buttons that belong to the runner's pages get
/// a mutable copy. Buttons created by the tests have no parent and keep the library's transformer untouched.
/// </summary>
static class RunnerButtonShim
{
	public static void Register()
	{
		// The keys NativeStyles.Buttons.iOS.cs rebuilds the configuration on; appended after it, so this runs last
		string[] keys = [NativeStylesExtensions.MappingKey, nameof(IButton.Background), nameof(IButtonStroke.CornerRadius),
			nameof(IButtonStroke.StrokeThickness), nameof(ITextStyle.TextColor), nameof(ITextStyle.Font), nameof(IText.Text),
			nameof(IImageSourcePart.Source), nameof(IPadding.Padding), "LineBreakMode"];
		foreach (var key in keys)
			Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping(key, (handler, button) =>
			{
				if (button is not Element { Parent: not null }
					|| handler.PlatformView.Configuration is not { TitleTextAttributesTransformer: { } transformer } configuration)
					return;
				configuration.TitleTextAttributesTransformer = attributes =>
					transformer(attributes is null ? new Foundation.NSMutableDictionary() : (Foundation.NSMutableDictionary)attributes.MutableCopy());
				handler.PlatformView.Configuration = configuration;
			});
	}
}
#endif
