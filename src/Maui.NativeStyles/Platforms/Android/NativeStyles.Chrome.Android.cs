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

public static partial class NativeStylesExtensions
{
	/// <summary>Finds Shell's BottomNavigationView and toolbar search card; MAUI content is not traversed.</summary>
	internal static void StyleShellChrome(AView view, int depth)
	{
		switch (view)
		{
			case Google.Android.Material.BottomNavigation.BottomNavigationView navigation:
				StyleNavigationBar(navigation);
				return;
			case Google.Android.Material.Tabs.TabLayout tabs:
				StyleTabs(tabs);
				return;
			case AndroidX.DrawerLayout.Widget.DrawerLayout drawer:
				StyleDrawerSheet(drawer);
				break;
			case Google.Android.Material.AppBar.AppBarLayout appBar:
				StyleAppBar(appBar);
				break; // the toolbar inside may host the search card
			case AndroidX.CardView.Widget.CardView card when card.Parent is Microsoft.Maui.Controls.Platform.Compatibility.ShellSearchView:
				StyleShellSearch(card);
				return;
		}
		if (depth > 16 || view is not ViewGroup group || view is Microsoft.Maui.Platform.ContentViewGroup or Microsoft.Maui.Platform.LayoutViewGroup or AndroidX.RecyclerView.Widget.RecyclerView)
			return;
		for (var i = 0; i < group.ChildCount; i++)
			if (group.GetChildAt(i) is { } child)
				StyleShellChrome(child, depth + 1);
	}

	/// <summary>M3 Expressive flexible navigation bar: 64 dp, 56x32 dp indicator, secondary active label.</summary>
	static void StyleNavigationBar(Google.Android.Material.BottomNavigation.BottomNavigationView bar)
	{
		if (bar.Context is not { } context)
			return;
		// TabbedPage reserves the baseline 80 dp below its content (MAUI reads m3_bottom_nav_min_height); follow the bar.
		var barHeight = (int)context.ToPixels(64);
		if (bar.RootView?.FindViewById(Microsoft.Maui.Resource.Id.navigationlayout_content) is { LayoutParameters: ViewGroup.MarginLayoutParams content } contentView
			&& content.BottomMargin == context.Resources!.GetDimensionPixelSize(Resource.Dimension.m3_bottom_nav_min_height))
		{
			content.BottomMargin = barHeight;
			contentView.RequestLayout();
		}

		var indicatorWidth = (int)context.ToPixels(56);
		if (bar.ItemActiveIndicatorWidth == indicatorWidth && bar.ItemTextColor == s_navigationLabelColors)
			return;

		int Role(int attribute) => MaterialColors.GetColor(bar, attribute);
		Android.Content.Res.ColorStateList Checked(int checkedColor, int color) => new(
			[[Android.Resource.Attribute.StateChecked], []], [checkedColor, color]);

		var onSurfaceVariant = Role(Resource.Attribute.colorOnSurfaceVariant);
		s_navigationLabelColors = Checked(Role(Resource.Attribute.colorSecondary), onSurfaceVariant);
		bar.ItemTextColor = s_navigationLabelColors;
		bar.ItemIconTintList = Checked(Role(Resource.Attribute.colorOnSecondaryContainer), onSurfaceVariant);
		bar.ItemActiveIndicatorColor = Android.Content.Res.ColorStateList.ValueOf(new AColor(Role(Resource.Attribute.colorSecondaryContainer)));
		bar.ItemActiveIndicatorWidth = indicatorWidth;
		bar.ItemActiveIndicatorHeight = (int)context.ToPixels(32);
		bar.ItemPaddingTop = (int)context.ToPixels(6);
		bar.ItemPaddingBottom = (int)context.ToPixels(6);
		bar.ActiveIndicatorLabelPadding = (int)context.ToPixels(4);
		bar.SetMinimumHeight(barHeight);
		bar.SetBackgroundColor(new AColor(Role(Resource.Attribute.colorSurfaceContainer)));
	}

	static (NavigationPage? Navigation, Page? Leaf) CurrentNavigation(Page? page)
	{
		NavigationPage? navigation = null;
		for (var depth = 0; page is not null && depth < 8; depth++)
		{
			switch (page)
			{
				case NavigationPage n: navigation = n; page = n.CurrentPage; break;
				case TabbedPage t: page = t.CurrentPage; break;
				case FlyoutPage f: page = f.Detail; break;
				default: return (navigation, page);
			}
		}
		return (navigation, page);
	}

	static Android.Content.Res.ColorStateList? s_navigationLabelColors;

	/// <summary>
	/// M3 primary tabs (Shell top tabs, TabbedPage top tabs): primary label and label-width indicator for the active
	/// tab, onSurfaceVariant otherwise, fixed tabs sharing the width (scrollable above four), transparent over the app bar.
	/// </summary>
	static void StyleTabs(Google.Android.Material.Tabs.TabLayout tabs)
	{
		if (tabs.Context is not { } context)
			return;
		var primary = MaterialColors.GetColor(tabs, Resource.Attribute.colorPrimary);
		var mode = tabs.TabCount > 4 ? Google.Android.Material.Tabs.TabLayout.ModeScrollable : Google.Android.Material.Tabs.TabLayout.ModeFixed;

		// Same color as the app bar above (StyleAppBar records it); a TabbedPage hosts the tabs in a primary-colored strip
		var container = MaterialColors.GetColor(tabs, Resource.Attribute.colorSurface);
		for (var parent = tabs.Parent; parent is not null; parent = parent.Parent)
			if (parent is Google.Android.Material.AppBar.AppBarLayout appBar && appBar.GetTag(Resource.Id.action_bar_container) is Java.Lang.Integer recorded)
			{
				container = recorded.IntValue();
				break;
			}

		if (tabs.TabTextColors?.GetColorForState([Android.Resource.Attribute.StateSelected], AColor.Transparent) == primary
			&& tabs.TabMode == mode
			&& (tabs.GetTag(Resource.Id.action_bar_container) as Java.Lang.Integer)?.IntValue() == container)
		{
			return;
		}

		var onSurfaceVariant = MaterialColors.GetColor(tabs, Resource.Attribute.colorOnSurfaceVariant);
		tabs.SetTabTextColors(onSurfaceVariant, primary);
		tabs.TabIconTint = new Android.Content.Res.ColorStateList(
			[[Android.Resource.Attribute.StateSelected], []], [primary, onSurfaceVariant]);
		tabs.SetSelectedTabIndicatorColor(primary);
		tabs.TabMode = mode;
		tabs.TabGravity = Google.Android.Material.Tabs.TabLayout.GravityFill;
		tabs.SetTag(Resource.Id.action_bar_container, Java.Lang.Integer.ValueOf(container));

		// Container color with the 1 dp outlineVariant divider inside it (bottom edge)
		var background = new ColorDrawable(new AColor(container));
		if (OperatingSystem.IsAndroidVersionAtLeast(23))
		{
			var layers = new LayerDrawable([background, new ColorDrawable(new AColor(MaterialColors.GetColor(tabs, Resource.Attribute.colorOutlineVariant)))]);
			layers.SetLayerGravity(1, GravityFlags.Bottom);
			layers.SetLayerHeight(1, Math.Max(1, (int)context.ToPixels(1)));
			tabs.Background = layers;
		}
		else
		{
			tabs.Background = background;
		}
	}

	/// <summary>
	/// MAUI tints the Toolbar only; under edge-to-edge the AppBarLayout also covers the status bar, so it must take the
	/// page's Shell.BackgroundColor as well (or return to colorSurface when the page sets none).
	/// </summary>
	static void StyleAppBar(Google.Android.Material.AppBar.AppBarLayout appBar)
	{
		int color;
		var colorToolbar = false;
		var surface = MaterialColors.GetColor(appBar, Resource.Attribute.colorSurface);
		if (Shell.Current is { } shell)
		{
			var requested = (shell.CurrentPage is { } page ? Shell.GetBackgroundColor(page) : null) ?? Shell.GetBackgroundColor(shell);
			color = requested?.ToPlatform().ToArgb() ?? surface;
		}
		else
		{
			// NavigationPage: flat app bar in the color of the page below it, unless the app chose a bar color
			var (navigation, leaf) = CurrentNavigation(Application.Current?.Windows.FirstOrDefault()?.Page);
			if (navigation is null)
				return;
			color = (navigation.BarBackgroundColor ?? leaf?.BackgroundColor)?.ToPlatform().ToArgb() ?? surface;
			colorToolbar = navigation.BarBackgroundColor is null;
		}
		// Trailing icons are onSurfaceVariant on a themed (surface) app bar; MAUI tints them like the navigation icon.
		if (color == surface || color == MaterialColors.GetColor(appBar, Resource.Attribute.colorSurfaceContainer))
			for (var i = 0; i < appBar.ChildCount; i++)
				if (appBar.GetChildAt(i) is AndroidX.AppCompat.Widget.Toolbar bar)
					TintTrailingIcons(bar, MaterialColors.GetColor(appBar, Resource.Attribute.colorOnSurfaceVariant));

		if (appBar.GetTag(Resource.Id.action_bar_container) is Java.Lang.Integer applied && applied.IntValue() == color)
			return;
		for (var i = 0; colorToolbar && i < appBar.ChildCount; i++)
			if (appBar.GetChildAt(i) is AndroidX.AppCompat.Widget.Toolbar toolbar)
				toolbar.SetBackgroundColor(new AColor(color));
		appBar.SetTag(Resource.Id.action_bar_container, Java.Lang.Integer.ValueOf(color));
		appBar.SetBackgroundColor(new AColor(color));
		appBar.SetStatusBarForegroundColor(color);
	}

	static void TintTrailingIcons(AndroidX.AppCompat.Widget.Toolbar toolbar, int color)
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast(26) || toolbar.Menu is not { } menu)
			return;
		for (var i = 0; i < menu.Size(); i++)
			if (menu.GetItem(i) is { Icon: not null } item && item.IconTintList?.DefaultColor != color)
				item.SetIconTintList(Android.Content.Res.ColorStateList.ValueOf(new AColor(color)));
		if (toolbar.OverflowIcon is { } overflow && (toolbar.GetTag(Resource.Id.action_menu_presenter) as Java.Lang.Integer)?.IntValue() != color)
		{
			// MAUI colors the overflow glyph with a color filter, which takes precedence over a tint
			overflow.SetColorFilter(new PorterDuffColorFilter(new AColor(color), PorterDuff.Mode.SrcIn!));
			toolbar.SetTag(Resource.Id.action_menu_presenter, Java.Lang.Integer.ValueOf(color));
		}
	}

	/// <summary>
	/// Shell flyout / FlyoutPage sheet as an M3 modal navigation drawer: at most 360 dp wide, leaving 56 dp of scrim,
	/// with 16 dp corners on the trailing side.
	/// </summary>
	static void StyleDrawerSheet(AndroidX.DrawerLayout.Widget.DrawerLayout drawer)
	{
		if (drawer.Context is not { } context || drawer.Width <= 0)
			return;
		for (var i = 0; i < drawer.ChildCount; i++)
		{
			if (drawer.GetChildAt(i) is not { LayoutParameters: AndroidX.DrawerLayout.Widget.DrawerLayout.LayoutParams { Gravity: not (int)GravityFlags.NoGravity } layout } sheet)
				continue;
			var width = Math.Min((int)context.ToPixels(360), drawer.Width - (int)context.ToPixels(56));
			if (layout.Width != width)
			{
				layout.Width = width;
				sheet.LayoutParameters = layout;
			}
			if (sheet.OutlineProvider is not TrailingCornersOutline)
			{
				sheet.OutlineProvider = new TrailingCornersOutline(context.ToPixels(16));
				sheet.ClipToOutline = true;
			}
		}
	}

	/// <summary>Round-rect outline that extends past the leading edge, so only the trailing corners are rounded.</summary>
	sealed class TrailingCornersOutline(float radius) : ViewOutlineProvider
	{
		public override void GetOutline(AView? view, Outline? outline)
		{
			if (view is null || outline is null)
				return;
			var r = (int)radius;
			if (view.LayoutDirection == Android.Views.LayoutDirection.Rtl)
				outline.SetRoundRect(0, 0, view.Width + r, view.Height, radius);
			else
				outline.SetRoundRect(-r, 0, view.Width, view.Height, radius);
		}
	}

	/// <summary>Shell.SearchHandler: Material 3 search bar container instead of the elevated white card.</summary>
	static void StyleShellSearch(AndroidX.CardView.Widget.CardView card)
	{
		if (card.Context is not { } context || card.CardElevation == 0)
			return;
		card.CardElevation = 0;
		card.Radius = context.ToPixels(28);
		card.SetCardBackgroundColor(MaterialColors.GetColor(card, Resource.Attribute.colorSurfaceContainerHigh));
	}
}
