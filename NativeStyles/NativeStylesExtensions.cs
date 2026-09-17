namespace MauiNativeStyle.NativeStyles;

/// <summary>
/// Registers the handler mappings that XAML styles cannot express
/// (UIButtonConfiguration / Liquid Glass on iOS, Material 3 tweaks on Android).
/// The XAML resource dictionaries are merged in <see cref="App"/>.
/// </summary>
public static partial class NativeStylesExtensions
{
	/// <summary>Mapper key under which every native-style mapping is registered.</summary>
	public const string MappingKey = "NativeStyle";

	/// <summary>Well-known StyleClass names. Each one is a XAML style that sets the matching attached property.</summary>
	public static class Classes
	{
		public const string Filled = "Filled";                 // NativeButton.Kind = Filled
		public const string Tonal = "Tonal";                   // NativeButton.Kind = Tonal
		public const string Outlined = "Outlined";             // NativeButton.Kind = Outlined
		public const string Text = "Text";                     // NativeButton.Kind = Text
		public const string Glass = "Glass";                   // NativeButton.Kind = Glass
		public const string GlassProminent = "GlassProminent"; // NativeButton.Kind = GlassProminent
		public const string Destructive = "Destructive";       // NativeButton.IsDestructive = true
		public const string Small = "Small";                   // NativeButton.Size = Small
		public const string Large = "Large";                   // NativeButton.Size = Large
		public const string Plain = "Plain";                   // NativeEntry.IsPlain = true
		public const string Semibold = "Semibold";             // NativeText.Weight = Semibold
	}

	public static MauiAppBuilder UseNativeStyles(this MauiAppBuilder builder)
	{
		builder.ConfigureMauiHandlers(handlers => RegisterPlatformHandlers(handlers));
		RegisterPlatformMappers();
		return builder;
	}

	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers);
	static partial void RegisterPlatformMappers();

	/// <summary>Re-runs the native-style mapping after an attached property changed at runtime.</summary>
	internal static void Refresh(BindableObject bindable)
	{
		if (bindable is Element { Handler: { } handler })
			handler.UpdateValue(MappingKey);
	}
}
