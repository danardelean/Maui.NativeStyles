namespace NativeStyles;

/// <summary>
/// Registers the handler mappings that XAML styles cannot express
/// (UIButtonConfiguration / Liquid Glass on iOS, Material 3 tweaks on Android).
/// The XAML resource dictionaries are merged through <see cref="NativeStyleDictionary"/>.
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
		public const string Expressive = "Expressive";         // Entry/Editor: filled 28 dp container (Android only); no-op on Border since 0.5
		public const string Gap = "Gap";                       // BoxView: alias of Separator (2 dp segment gap on Android, hairline on iOS)
	}

	/// <summary>
	/// Registers the native-style handlers and mappings, and routes the library's diagnostics to the app's logging
	/// (category <c>Maui.NativeStyles</c>).
	/// </summary>
	/// <remarks>
	/// The mappings, the brand and the Android activity callbacks are process-wide (MAUI's handler mappers are static),
	/// so they are registered once: the first call's options stay in effect for the lifetime of the process. Calling it
	/// again, from the app or a library, is safe; each builder still gets the handlers, configured with the first call's
	/// options, and a call with different options is ignored with a warning in the log.
	/// </remarks>
	public static MauiAppBuilder UseNativeStyles(this MauiAppBuilder builder, Action<NativeStylesOptions>? configure = null)
	{
		var options = new NativeStylesOptions();
		configure?.Invoke(options);

		var active = Interlocked.CompareExchange(ref s_options, options, null) ?? options;
		if (ReferenceEquals(active, options))
		{
			SystemColors.Brand = options.Brand;
			RegisterPlatformMappers(options);
		}
		else if (!active.IsEquivalentTo(options))
		{
			NativeStylesLog.Warning("options", "UseNativeStyles was called again with different options. Its mappings are process-wide and registered once, so the options of the first call stay in effect.");
		}

		// Handlers belong to the builder: register them on each builder, once
		if (!builder.Services.Any(d => !d.IsKeyedService && d.ImplementationType == typeof(NativeStylesLogInitializer)))
		{
			builder.Services.AddTransient<IMauiInitializeService, NativeStylesLogInitializer>();
			builder.ConfigureMauiHandlers(handlers => RegisterPlatformHandlers(handlers, active));
		}
		return builder;
	}

	/// <summary>The options of the first <see cref="UseNativeStyles"/> call, which are the ones in effect.</summary>
	static NativeStylesOptions? s_options;

	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers, NativeStylesOptions options);
	static partial void RegisterPlatformMappers(NativeStylesOptions options);

	/// <summary>Re-runs the native-style mapping after an attached property changed at runtime.</summary>
	internal static void Refresh(BindableObject bindable)
	{
		if (bindable is Element { Handler: { } handler })
			handler.UpdateValue(MappingKey);
	}
}
