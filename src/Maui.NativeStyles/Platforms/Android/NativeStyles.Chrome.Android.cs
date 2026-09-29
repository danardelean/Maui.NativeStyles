using Android.Graphics;
using Android.Views;
using AView = Android.Views.View;
using Google.Android.Material.Color;
using Color = Microsoft.Maui.Graphics.Color;
using Android.Graphics.Drawables;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using AContext = Android.Content.Context;
using AColor = Android.Graphics.Color;
using AToolbar = AndroidX.AppCompat.Widget.Toolbar;
using AppBarLayout = Google.Android.Material.AppBar.AppBarLayout;
using BottomNavigationView = Google.Android.Material.BottomNavigation.BottomNavigationView;
using DrawerLayout = AndroidX.DrawerLayout.Widget.DrawerLayout;
using TabLayout = Google.Android.Material.Tabs.TabLayout;

namespace NativeStyles;

public static partial class NativeStylesExtensions
{
	/// <summary>
	/// Layout pass of an activity (see ShellChromeStyler): styles the chrome that has no MAUI extension point
	/// (TabbedPage tabs and navigation bar, NavigationPage app bar, FlyoutPage drawer) and the Shell of apps that do
	/// not render it with <see cref="NativeShellRenderer"/>. NativeShellRenderer styles its chrome from its trackers,
	/// so a window showing such a Shell is not walked at all.
	/// </summary>
	internal static void StyleWindowChrome(Android.App.Activity activity, AView decor)
	{
		if (RootPageOf(activity) is Shell { Handler: NativeShellRenderer })
			return;
		ObserveChromeTheme();
		if (CachedChromeColors(decor).IsMaterial3)
			StyleShellChrome(decor, 0);
	}

	static Page? RootPageOf(Android.App.Activity activity)
	{
		foreach (var window in Application.Current?.Windows ?? [])
			if (ReferenceEquals(window.Handler?.PlatformView, activity))
				return window.Page;
		return null;
	}

	/// <summary>Finds Shell's BottomNavigationView and toolbar search card; MAUI content is not traversed.</summary>
	internal static void StyleShellChrome(AView view, int depth)
	{
		switch (view)
		{
			case BottomNavigationView navigation:
				StyleNavigationBar(navigation, CachedChromeColors(navigation));
				return;
			case TabLayout tabs:
				StyleTabs(tabs, CachedChromeColors(tabs));
				return;
			case DrawerLayout drawer:
				StyleDrawerSheet(drawer);
				break;
			case AppBarLayout appBar:
				StyleAppBar(appBar, CachedChromeColors(appBar));
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

	/// <summary>
	/// Theme roles used by the chrome, each resolved on first use (MaterialColors.GetColor is a JNI theme lookup). A
	/// role the theme does not define resolves to its Material 3 baseline value instead of throwing. The layout listener
	/// keeps one per context so a layout pass does not look the theme up again; the Shell trackers, which run only when
	/// something changed, use a fresh one.
	/// </summary>
	internal sealed class ChromeColors(AContext context)
	{
		bool? _isMaterial3;
		int? _surface, _surfaceContainer, _surfaceContainerHigh, _primary, _secondary, _secondaryContainer, _onSecondaryContainer,
			_onSurfaceVariant, _outlineVariant, _baselineNavigationBarHeight;

		/// <summary>
		/// The chrome is restyled under a Material 3 theme only. Without the Material 3 roles (an app that does not use
		/// Material 3, such as one on Maui.MainTheme) it keeps MAUI's chrome.
		/// </summary>
		public bool IsMaterial3 => _isMaterial3 ??= context.Theme?.ResolveAttribute(Resource.Attribute.colorSurfaceContainer, new Android.Util.TypedValue(), true) == true;

		public int Surface => _surface ??= Role(Resource.Attribute.colorSurface, 0xFFFEF7FF);
		public int SurfaceContainer => _surfaceContainer ??= Role(Resource.Attribute.colorSurfaceContainer, 0xFFF3EDF7);
		public int SurfaceContainerHigh => _surfaceContainerHigh ??= Role(Resource.Attribute.colorSurfaceContainerHigh, 0xFFECE6F0);
		public int Primary => _primary ??= Role(Resource.Attribute.colorPrimary, 0xFF6750A4);
		public int Secondary => _secondary ??= Role(Resource.Attribute.colorSecondary, 0xFF625B71);
		public int SecondaryContainer => _secondaryContainer ??= Role(Resource.Attribute.colorSecondaryContainer, 0xFFE8DEF8);
		public int OnSecondaryContainer => _onSecondaryContainer ??= Role(Resource.Attribute.colorOnSecondaryContainer, 0xFF4A4458);
		public int OnSurfaceVariant => _onSurfaceVariant ??= Role(Resource.Attribute.colorOnSurfaceVariant, 0xFF49454F);
		public int OutlineVariant => _outlineVariant ??= Role(Resource.Attribute.colorOutlineVariant, 0xFFCAC4D0);
		// TabbedPage reserves this height below its content (MAUI reads m3_bottom_nav_min_height)
		public int BaselineNavigationBarHeight => _baselineNavigationBarHeight ??= context.Resources!.GetDimensionPixelSize(Resource.Dimension.m3_bottom_nav_min_height);

		// The Material 3 baseline (light scheme) value when the theme does not define the role
		int Role(int attribute, uint baseline) => MaterialColors.GetColor(context, attribute, unchecked((int)baseline));
	}

	static System.Runtime.CompilerServices.ConditionalWeakTable<AContext, ChromeColors> s_chromeColors = new();
	static WeakReference<Application>? s_chromeThemeApplication;

	static ChromeColors CachedChromeColors(AView view)
	{
		var context = view.Context!;
		if (!s_chromeColors.TryGetValue(context, out var colors))
			s_chromeColors.Add(context, colors = new ChromeColors(context));
		return colors;
	}

	/// <summary>An activity that handles the uiMode change itself resolves its theme colors anew: drop the cached ones.</summary>
	static void ObserveChromeTheme()
	{
		if (Application.Current is not { } application
			|| (s_chromeThemeApplication is not null && s_chromeThemeApplication.TryGetTarget(out var observed) && ReferenceEquals(observed, application)))
		{
			return;
		}
		s_chromeThemeApplication = new WeakReference<Application>(application);
		// RequestedThemeChanged is a weak event: a static method has no target that could be collected
		application.RequestedThemeChanged += OnChromeThemeChanged;
	}

	static void OnChromeThemeChanged(object? sender, AppThemeChangedEventArgs e) => s_chromeColors = new();

	/// <summary>
	/// M3 Expressive flexible navigation bar: 64 dp, 56x32 dp indicator, secondary active label. <c>force</c>: restyle
	/// even if the bar looks styled already (a Shell tracker has just applied MAUI's colors).
	/// </summary>
	internal static void StyleNavigationBar(BottomNavigationView bar, ChromeColors colors, bool force = false)
	{
		if (bar.Context is not { } context || !colors.IsMaterial3)
			return;
		// TabbedPage reserves the baseline 80 dp below its content (MAUI reads m3_bottom_nav_min_height); follow the bar.
		var barHeight = (int)context.ToPixels(64);
		if (bar.RootView?.FindViewById(Microsoft.Maui.Resource.Id.navigationlayout_content) is { LayoutParameters: ViewGroup.MarginLayoutParams content } contentView
			&& content.BottomMargin == colors.BaselineNavigationBarHeight)
		{
			content.BottomMargin = barHeight;
			contentView.RequestLayout();
		}

		var indicatorWidth = (int)context.ToPixels(56);
		if (!force && bar.ItemActiveIndicatorWidth == indicatorWidth && bar.ItemTextColor == s_navigationLabelColors)
			return;

		Android.Content.Res.ColorStateList Checked(int checkedColor, int color) => new(
			[[Android.Resource.Attribute.StateChecked], []], [checkedColor, color]);

		var onSurfaceVariant = colors.OnSurfaceVariant;
		s_navigationLabelColors = Checked(colors.Secondary, onSurfaceVariant);
		bar.ItemTextColor = s_navigationLabelColors;
		bar.ItemIconTintList = Checked(colors.OnSecondaryContainer, onSurfaceVariant);
		bar.ItemActiveIndicatorColor = Android.Content.Res.ColorStateList.ValueOf(new AColor(colors.SecondaryContainer));
		bar.ItemActiveIndicatorWidth = indicatorWidth;
		bar.ItemActiveIndicatorHeight = (int)context.ToPixels(32);
		bar.ItemPaddingTop = (int)context.ToPixels(6);
		bar.ItemPaddingBottom = (int)context.ToPixels(6);
		bar.ActiveIndicatorLabelPadding = (int)context.ToPixels(4);
		bar.SetMinimumHeight(barHeight);
		bar.SetBackgroundColor(new AColor(colors.SurfaceContainer));
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
	/// <c>container</c>: color behind the tabs, by default the one StyleAppBar recorded on the app bar above them;
	/// <c>force</c>: restyle even if the tabs look styled already (a Shell tracker has just applied MAUI's colors).
	/// </summary>
	internal static void StyleTabs(TabLayout tabs, ChromeColors colors, int? container = null, bool force = false)
	{
		if (tabs.Context is not { } context || !colors.IsMaterial3)
			return;
		var primary = colors.Primary;
		var mode = tabs.TabCount > 4 ? TabLayout.ModeScrollable : TabLayout.ModeFixed;

		// Same color as the app bar above (StyleAppBar records it); a TabbedPage hosts the tabs in a primary-colored strip
		if (container is null)
		{
			container = colors.Surface;
			for (var parent = tabs.Parent; parent is not null; parent = parent.Parent)
				if (parent is AppBarLayout appBar && appBar.GetTag(Resource.Id.nativestyles_app_bar_color) is Java.Lang.Integer recorded)
				{
					container = recorded.IntValue();
					break;
				}
		}

		if (!force
			&& tabs.TabTextColors?.GetColorForState([Android.Resource.Attribute.StateSelected], AColor.Transparent) == primary
			&& tabs.TabMode == mode
			&& (tabs.GetTag(Resource.Id.nativestyles_tabs_container_color) as Java.Lang.Integer)?.IntValue() == container)
		{
			return;
		}

		var onSurfaceVariant = colors.OnSurfaceVariant;
		tabs.SetTabTextColors(onSurfaceVariant, primary);
		tabs.TabIconTint = new Android.Content.Res.ColorStateList(
			[[Android.Resource.Attribute.StateSelected], []], [primary, onSurfaceVariant]);
		tabs.SetSelectedTabIndicatorColor(primary);
		tabs.TabMode = mode;
		tabs.TabGravity = TabLayout.GravityFill;
		tabs.SetTag(Resource.Id.nativestyles_tabs_container_color, Java.Lang.Integer.ValueOf(container.Value));

		// Container color with the 1 dp outlineVariant divider inside it (bottom edge)
		var background = new ColorDrawable(new AColor(container.Value));
		if (OperatingSystem.IsAndroidVersionAtLeast(23))
		{
			var layers = new LayerDrawable([background, new ColorDrawable(new AColor(colors.OutlineVariant))]);
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
	static void StyleAppBar(AppBarLayout appBar, ChromeColors colors)
	{
		int color;
		var colorToolbar = false;
		if (Shell.Current is { } shell)
		{
			var requested = (shell.CurrentPage is { } page ? Shell.GetBackgroundColor(page) : null) ?? Shell.GetBackgroundColor(shell);
			color = requested?.ToPlatform().ToArgb() ?? colors.Surface;
		}
		else
		{
			// NavigationPage: flat app bar in the color of the page below it, unless the app chose a bar color
			var (navigation, leaf) = CurrentNavigation(Application.Current?.Windows.FirstOrDefault()?.Page);
			if (navigation is null)
				return;
			color = (navigation.BarBackgroundColor ?? leaf?.BackgroundColor)?.ToPlatform().ToArgb() ?? colors.Surface;
			colorToolbar = navigation.BarBackgroundColor is null;
		}
		ApplyAppBarColor(appBar, color, colors, colorToolbar);
	}

	/// <summary>
	/// Shell app bar from its toolbar appearance tracker: the color of the ShellAppearance MAUI has just applied to
	/// the toolbar (the Shell.BackgroundColor nearest to the page), colorSurface when there is none.
	/// </summary>
	internal static void StyleShellAppBar(AToolbar toolbar, Color? requested)
	{
		if (toolbar.Parent is not AppBarLayout appBar || appBar.Context is not { } context)
			return;
		var colors = new ChromeColors(context);
		if (colors.IsMaterial3)
			ApplyAppBarColor(appBar, requested?.ToPlatform().ToArgb() ?? colors.Surface, colors, colorToolbar: false);
	}

	static void ApplyAppBarColor(AppBarLayout appBar, int color, ChromeColors colors, bool colorToolbar)
	{
		// Trailing icons are onSurfaceVariant on a themed (surface) app bar; MAUI tints them like the navigation icon.
		if (IsThemedAppBarColor(color, colors))
			for (var i = 0; i < appBar.ChildCount; i++)
				if (appBar.GetChildAt(i) is AToolbar bar)
					TintTrailingIcons(bar, colors.OnSurfaceVariant);

		if (appBar.GetTag(Resource.Id.nativestyles_app_bar_color) is Java.Lang.Integer applied && applied.IntValue() == color)
			return;
		for (var i = 0; colorToolbar && i < appBar.ChildCount; i++)
			if (appBar.GetChildAt(i) is AToolbar toolbar)
				toolbar.SetBackgroundColor(new AColor(color));
		appBar.SetTag(Resource.Id.nativestyles_app_bar_color, Java.Lang.Integer.ValueOf(color));
		appBar.SetBackgroundColor(new AColor(color));
		appBar.SetStatusBarForegroundColor(color);
	}

	static bool IsThemedAppBarColor(int color, ChromeColors colors) => color == colors.Surface || color == colors.SurfaceContainer;

	/// <summary>
	/// ToolbarHandler rebuilds the menu, loads the item icons and recolors the overflow icon whenever the toolbar items
	/// or the icon color change: tint them again if they sit on an app bar styled with a themed color.
	/// </summary>
	static void MapToolbarTrailingIcons(IToolbarHandler handler, IToolbar toolbar)
	{
		if (handler.PlatformView is not AToolbar bar || bar.Parent is not AppBarLayout appBar || appBar.Context is not { } context
			|| appBar.GetTag(Resource.Id.nativestyles_app_bar_color) is not Java.Lang.Integer color)
		{
			return;
		}
		var colors = new ChromeColors(context);
		if (!IsThemedAppBarColor(color.IntValue(), colors))
			return;
		// MAUI has just set its own color filter on the overflow icon
		bar.SetTag(Resource.Id.nativestyles_overflow_tint, null);
		TintTrailingIcons(bar, colors.OnSurfaceVariant);
	}

	static void TintTrailingIcons(AToolbar toolbar, int color)
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast(26) || toolbar.Menu is not { } menu)
			return;
		// An icon that is still loading takes the item's tint when it arrives
		for (var i = 0; i < menu.Size(); i++)
			if (menu.GetItem(i) is { } item && item.IconTintList?.DefaultColor != color)
				item.SetIconTintList(Android.Content.Res.ColorStateList.ValueOf(new AColor(color)));
		if (toolbar.OverflowIcon is { } overflow && (toolbar.GetTag(Resource.Id.nativestyles_overflow_tint) as Java.Lang.Integer)?.IntValue() != color)
		{
			// MAUI colors the overflow glyph with a color filter, which takes precedence over a tint
			overflow.SetColorFilter(new PorterDuffColorFilter(new AColor(color), PorterDuff.Mode.SrcIn!));
			toolbar.SetTag(Resource.Id.nativestyles_overflow_tint, Java.Lang.Integer.ValueOf(color));
		}
	}

	/// <summary>
	/// Shell flyout / FlyoutPage sheet as an M3 modal navigation drawer: at most 360 dp wide, leaving 56 dp of scrim,
	/// with 16 dp corners on the trailing side.
	/// </summary>
	static void StyleDrawerSheet(DrawerLayout drawer)
	{
		if (drawer.Context is not { } context || drawer.Width <= 0)
			return;
		for (var i = 0; i < drawer.ChildCount; i++)
		{
			if (drawer.GetChildAt(i) is not { LayoutParameters: DrawerLayout.LayoutParams { Gravity: not (int)GravityFlags.NoGravity } layout } sheet)
				continue;
			var width = DrawerSheetWidth(context, drawer.Width);
			if (layout.Width != width)
			{
				layout.Width = width;
				sheet.LayoutParameters = layout;
			}
			StyleDrawerSheetShape(sheet);
		}
	}

	internal static int DrawerSheetWidth(AContext context, int drawerWidth) =>
		Math.Min((int)context.ToPixels(360), drawerWidth - (int)context.ToPixels(56));

	internal static void StyleDrawerSheetShape(AView sheet)
	{
		if (sheet.Context is not { } context || sheet.OutlineProvider is TrailingCornersOutline)
			return;
		sheet.OutlineProvider = new TrailingCornersOutline(context.ToPixels(16));
		sheet.ClipToOutline = true;
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
	internal static void StyleShellSearch(AndroidX.CardView.Widget.CardView card)
	{
		if (card.Context is not { } context || card.CardElevation == 0 || new ChromeColors(context) is not { IsMaterial3: true } colors)
			return;
		card.CardElevation = 0;
		card.Radius = context.ToPixels(28);
		card.SetCardBackgroundColor(colors.SurfaceContainerHigh);
	}
}
