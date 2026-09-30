using Android.Graphics;
using Android.Views;
using AButton = Android.Widget.Button;
using AView = Android.Views.View;
using Google.Android.Material.Color;
using Color = Microsoft.Maui.Graphics.Color;
using Android.Graphics.Drawables;
using Google.Android.Material.TextField;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using AContext = Android.Content.Context;
using AColor = Android.Graphics.Color;

namespace NativeStyles;

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

	protected override void ConnectHandler(ContentViewGroup platformView)
	{
		base.ConnectHandler(platformView);
		if (Application.Current is { } app)
			app.RequestedThemeChanged += OnThemeChanged;
	}

	protected override void DisconnectHandler(ContentViewGroup platformView)
	{
		if (Application.Current is { } app)
			app.RequestedThemeChanged -= OnThemeChanged;
		base.DisconnectHandler(platformView);
	}

	void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
		// The event is weak and may still reach a handler whose view tree was discarded (for example the page tree MAUI
		// builds for a recreated activity before the live one is moved over): the throwing accessors must not be used.
		if (((IElementHandler)this).VirtualView is GlassView view && ((IElementHandler)this).PlatformView is not null && MauiContext?.Context is not null)
			MapSurface(this, view);
	}

	static void MapSurface(GlassViewHandler handler, GlassView view)
	{
		var context = handler.Context;
		// surfaceContainerLow from the running theme (dark mode / dynamic colors aware), baseline fallback.
		// M3 Expressive floating toolbar (standard): surfaceContainer, elevation level 3
		var color = (view.TintColor ?? SystemColors.Get(SystemColorRole.GroupedBackground)).ToPlatform();

		var drawable = new GradientDrawable();
		drawable.SetColor(color);
		var radiusPx = view.CornerRadius < 0 ? context.ToPixels(28) : context.ToPixels(view.CornerRadius);
		drawable.SetCornerRadius(radiusPx);

		var platformView = handler.PlatformView;
		platformView.Background = drawable;
		platformView.ClipToOutline = true;
		platformView.Elevation = context.ToPixels(3); // M3 elevation level 2
	}
}
