namespace MauiNativeStyle.NativeStyles;

/// <summary>
/// Registers the handler mappings that XAML styles cannot express
/// (UIButtonConfiguration / Liquid Glass on iOS, Material 3 tweaks on Android).
/// The XAML resource dictionaries are merged in <see cref="App"/>.
/// </summary>
public static partial class NativeStylesExtensions
{
	/// <summary>Well-known StyleClass names understood on both platforms.</summary>
	public static class Classes
	{
		public const string Filled = "Filled";           // iOS filled (tint bg)      | M3 filled
		public const string Tonal = "Tonal";             // iOS tinted (15% tint bg)  | M3 filled tonal
		public const string Outlined = "Outlined";       // iOS gray                  | M3 outlined
		public const string Text = "Text";               // iOS plain                 | M3 text
		public const string Glass = "Glass";             // iOS 26 glass              | M3 elevated
		public const string GlassProminent = "GlassProminent"; // iOS 26 prominent glass | M3 filled
		public const string Destructive = "Destructive"; // iOS systemRed             | M3 error
		public const string Small = "Small";             // iOS .small / M3 xsmall size
		public const string Large = "Large";             // iOS .large / M3 medium size
		public const string Plain = "Plain";             // Entry without border (grouped cell)
		public const string Semibold = "Semibold";       // Label weight 600 (iOS Headline/Title3)
	}

	public static MauiAppBuilder UseNativeStyles(this MauiAppBuilder builder)
	{
		builder.ConfigureMauiHandlers(handlers => RegisterPlatformHandlers(handlers));
		RegisterPlatformMappers();
		return builder;
	}

	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers);
	static partial void RegisterPlatformMappers();

	internal static bool HasClass(this IView view, string cls) =>
		view is NavigableElement e && e.StyleClass is { } classes && classes.Contains(cls);
}
