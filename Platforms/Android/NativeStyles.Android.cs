using Android.Graphics.Drawables;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace MauiNativeStyle.NativeStyles;

public static partial class NativeStylesExtensions
{
	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers)
	{
		handlers.AddHandler<GlassView, GlassViewHandler>();
	}

	static partial void RegisterPlatformMappers()
	{
		// "Destructive" combined with Text/Outlined: error-colored label instead of an error-filled container.
		// Everything else is expressed in MaterialStyles.xaml and by the Material 3 theme (UseMaterial3).
		ButtonHandler.Mapper.AppendToMapping("NativeStyle", MapDestructiveText);

		// Entry "Plain": no Material box, for fields embedded in list rows.
		// With UseMaterial3 the Entry is served by the internal EntryHandler2 (TextInputLayout), so its
		// Mapper is reached through reflection; the classic EntryHandler is covered for the non-M3 case.
		EntryHandler.Mapper.AppendToMapping("NativeStyle", (handler, entry) =>
		{
			if (entry.HasClass(Classes.Plain))
				handler.PlatformView.Background = null;
		});
		var material3EntryMapper = typeof(EntryHandler).Assembly
			.GetType("Microsoft.Maui.Handlers.EntryHandler2")?
			.GetField("Mapper", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?
			.GetValue(null) as IPropertyMapper<IEntry, IElementHandler>; // covariant cast
		material3EntryMapper?.Add("NativeStyle", (handler, entry) =>
		{
			if (!entry.HasClass(Classes.Plain))
				return;
			if (handler.PlatformView is Google.Android.Material.TextField.TextInputLayout layout)
			{
				layout.BoxBackgroundMode = Google.Android.Material.TextField.TextInputLayout.BoxBackgroundNone;
				layout.BoxStrokeWidth = 0;
				layout.BoxStrokeWidthFocused = 0;
				if (layout.EditText is { } editText)
					editText.Background = null;
			}
		});
	}

	static void MapDestructiveText(IButtonHandler handler, IButton button)
	{
		if (button is not Button b || !b.HasClass(Classes.Destructive) || !(b.HasClass(Classes.Text) || b.HasClass(Classes.Outlined)))
			return;

		// Local values win over the "Destructive" style setters (error-filled container).
		var app = Application.Current;
		b.BackgroundColor = Colors.Transparent;
		b.TextColor = app?.Resources.TryGetValue("Error", out var e) == true && e is Color error
			? new AppThemeBindingColor(error, (Color)app.Resources["ErrorDark"]).Resolve(app)
			: Color.FromArgb("#B3261E");
		if (b.HasClass(Classes.Outlined))
			b.BorderColor = b.TextColor;
	}

	readonly record struct AppThemeBindingColor(Color Light, Color Dark)
	{
		public Color Resolve(Application app) => app.RequestedTheme == AppTheme.Dark ? Dark : Light;
	}
}

/// <summary>Android counterpart of the iOS glass container: an M3 elevated surface with rounded corners.</summary>
public class GlassViewHandler : ContentViewHandler
{
	public static readonly IPropertyMapper<GlassView, GlassViewHandler> GlassMapper =
		new PropertyMapper<GlassView, GlassViewHandler>(ContentViewHandler.Mapper)
		{
			[nameof(GlassView.CornerRadius)] = MapSurface,
			[nameof(GlassView.TintColor)] = MapSurface,
			[nameof(GlassView.GlassStyle)] = MapSurface,
		};

	public GlassViewHandler() : base(GlassMapper)
	{
	}

	static void MapSurface(GlassViewHandler handler, GlassView view)
	{
		var context = handler.Context;
		var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
		// surfaceContainerLow (light) / surfaceContainerLow (dark) from the M3 baseline scheme
		var color = view.TintColor ?? (dark ? Color.FromArgb("#1D1B20") : Color.FromArgb("#F7F2FA"));

		var drawable = new GradientDrawable();
		drawable.SetColor(color.ToPlatform());
		var radiusPx = view.CornerRadius < 0 ? context.ToPixels(28) : context.ToPixels(view.CornerRadius);
		drawable.SetCornerRadius(radiusPx);

		var platformView = handler.PlatformView;
		platformView.Background = drawable;
		platformView.ClipToOutline = true;
		platformView.Elevation = context.ToPixels(3); // M3 elevation level 2
	}
}
