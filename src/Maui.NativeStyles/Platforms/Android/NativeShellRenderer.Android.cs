using System.Collections.Specialized;
using Android.Views;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using AContext = Android.Content.Context;
using AView = Android.Views.View;
using AToolbar = AndroidX.AppCompat.Widget.Toolbar;
using BottomNavigationView = Google.Android.Material.BottomNavigation.BottomNavigationView;
using CardView = AndroidX.CardView.Widget.CardView;
using DrawerLayout = AndroidX.DrawerLayout.Widget.DrawerLayout;
using TabLayout = Google.Android.Material.Tabs.TabLayout;

namespace NativeStyles;

/// <summary>
/// Shell renderer that gives the Shell chrome the Material 3 look through MAUI's Shell extension points: the
/// navigation bar, top tabs and app bar are styled by appearance trackers right after MAUI applied the Shell colors,
/// the search bar when its view is loaded, and the flyout sheet by the flyout renderer. Registered for
/// <see cref="Shell"/> by <c>UseNativeStyles</c> unless <see cref="NativeStylesOptions.ReplaceShellRenderer"/> is
/// false. An app with its own Shell renderer derives it from this class (and calls the base implementation of the
/// Create* methods it overrides) to keep the native styling. Under a theme without the Material 3 color roles it renders
/// like MAUI's ShellRenderer.
/// </summary>
public class NativeShellRenderer : ShellRenderer
{
	public NativeShellRenderer()
	{
	}

	public NativeShellRenderer(AContext context) : base(context)
	{
	}

	protected override IShellFlyoutRenderer CreateShellFlyoutRenderer() => new NativeShellFlyoutRenderer(this, AndroidContext);

	protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem) =>
		new NativeBottomNavViewAppearanceTracker(this, shellItem);

	protected override IShellTabLayoutAppearanceTracker CreateTabLayoutAppearanceTracker(ShellSection shellSection) =>
		new NativeTabLayoutAppearanceTracker(this, shellSection);

	protected override IShellToolbarAppearanceTracker CreateToolbarAppearanceTracker() => new NativeToolbarAppearanceTracker(this);

	protected override IShellToolbarTracker CreateTrackerForToolbar(AToolbar toolbar) =>
		new NativeShellToolbarTracker(this, toolbar, ((IShellContext)this).CurrentDrawerLayout);
}

/// <summary>M3 Expressive flexible navigation bar, applied over MAUI's Shell tab bar colors.</summary>
sealed class NativeBottomNavViewAppearanceTracker(IShellContext shellContext, ShellItem shellItem)
	: ShellBottomNavViewAppearanceTracker(shellContext, shellItem)
{
	public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
	{
		base.SetAppearance(bottomView, appearance);
		Style(bottomView);
	}

	public override void ResetAppearance(BottomNavigationView bottomView)
	{
		base.ResetAppearance(bottomView);
		Style(bottomView);
	}

	// Under Material 3 the container is surfaceContainer whatever the tab bar color; MAUI's would be replaced right away,
	// so it is not created at all (its default is an animated color-reveal drawable).
	protected override void SetBackgroundColor(BottomNavigationView bottomView, Microsoft.Maui.Graphics.Color color)
	{
		if (bottomView.Context is not { } context || !new NativeStylesExtensions.ChromeColors(context).IsMaterial3)
			base.SetBackgroundColor(bottomView, color);
	}

	static void Style(BottomNavigationView bottomView)
	{
		if (bottomView.Context is { } context)
			NativeStylesExtensions.StyleNavigationBar(bottomView, new NativeStylesExtensions.ChromeColors(context), force: true);
	}
}

/// <summary>M3 primary tabs over the Shell section's app bar color.</summary>
sealed class NativeTabLayoutAppearanceTracker : ShellTabLayoutAppearanceTracker
{
	IShellSectionController? _section;
	TabLayout? _tabs;
	int _container;

	public NativeTabLayoutAppearanceTracker(IShellContext shellContext, ShellSection shellSection) : base(shellContext)
	{
		_section = shellSection;
		_section.ItemsCollectionChanged += OnItemsCollectionChanged;
	}

	public override void SetAppearance(TabLayout tabLayout, ShellAppearance appearance)
	{
		base.SetAppearance(tabLayout, appearance);
		Style(tabLayout, appearance?.BackgroundColor);
	}

	public override void ResetAppearance(TabLayout tabLayout)
	{
		base.ResetAppearance(tabLayout);
		Style(tabLayout, null);
	}

	void Style(TabLayout tabs, Microsoft.Maui.Graphics.Color? background)
	{
		if (tabs.Context is not { } context)
			return;
		var colors = new NativeStylesExtensions.ChromeColors(context);
		// Same color as the app bar above them (see NativeToolbarAppearanceTracker)
		_tabs = tabs;
		_container = background?.ToPlatform().ToArgb() ?? colors.Surface;
		NativeStylesExtensions.StyleTabs(tabs, colors, _container, force: true);
	}

	// Adding or removing a tab can switch between fixed and scrollable tabs. MAUI repopulates the TabLayout from its
	// own handler of this event, which runs after this one: restyle once it is done.
	void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => _tabs?.Post(() =>
	{
		if (_tabs is { Handle: not 0 } tabs && tabs.Context is { } context)
			NativeStylesExtensions.StyleTabs(tabs, new NativeStylesExtensions.ChromeColors(context), _container, force: true);
	});

	protected override void Dispose(bool disposing)
	{
		if (disposing && _section is not null)
			_section.ItemsCollectionChanged -= OnItemsCollectionChanged;
		_section = null;
		_tabs = null;
		base.Dispose(disposing);
	}
}

/// <summary>App bar (behind the toolbar and the status bar) in the color MAUI has just given the Shell toolbar.</summary>
sealed class NativeToolbarAppearanceTracker(IShellContext shellContext) : ShellToolbarAppearanceTracker(shellContext)
{
	public override void SetAppearance(AToolbar toolbar, IShellToolbarTracker toolbarTracker, ShellAppearance appearance)
	{
		base.SetAppearance(toolbar, toolbarTracker, appearance);
		NativeStylesExtensions.StyleShellAppBar(toolbar, appearance?.BackgroundColor);
	}

	public override void ResetAppearance(AToolbar toolbar, IShellToolbarTracker toolbarTracker)
	{
		base.ResetAppearance(toolbar, toolbarTracker);
		NativeStylesExtensions.StyleShellAppBar(toolbar, null);
	}
}

/// <summary>Shell toolbar tracker whose Shell.SearchHandler view is the Material 3 search bar.</summary>
sealed class NativeShellToolbarTracker(IShellContext shellContext, AToolbar toolbar, DrawerLayout drawerLayout)
	: ShellToolbarTracker(shellContext, toolbar, drawerLayout)
{
	protected override IShellSearchView GetSearchView(AContext context) => new NativeShellSearchView(context, ShellContext);
}

/// <summary>Shell.SearchHandler view with the Material 3 search bar container.</summary>
sealed class NativeShellSearchView(AContext context, IShellContext shellContext) : ShellSearchView(context, shellContext)
{
	// Called again with a new card when the page's SearchHandler changes
	protected override void LoadView(SearchHandler searchHandler)
	{
		base.LoadView(searchHandler);
		if (ChildCount > 0 && GetChildAt(ChildCount - 1) is CardView card)
			NativeStylesExtensions.StyleShellSearch(card);
	}
}

/// <summary>Shell flyout as an M3 modal navigation drawer sheet (see NativeStylesExtensions.StyleDrawerSheet).</summary>
sealed class NativeShellFlyoutRenderer(IShellContext shellContext, AContext context) : ShellFlyoutRenderer(shellContext, context)
{
	bool? _isMaterial3;

	bool IsMaterial3 => _isMaterial3 ??= Context is { } drawerContext && new NativeStylesExtensions.ChromeColors(drawerContext).IsMaterial3;

	protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
	{
		// The width follows the drawer's own width, known here; set before the sheet is measured, so no extra layout pass
		if (IsMaterial3 && Context is { } drawerContext && AView.MeasureSpec.GetSize(widthMeasureSpec) is var drawerWidth and > 0)
		{
			for (var i = 0; i < ChildCount; i++)
				if (GetChildAt(i) is { LayoutParameters: LayoutParams { Gravity: not (int)GravityFlags.NoGravity } layout }
					&& NativeStylesExtensions.DrawerSheetWidth(drawerContext, drawerWidth) is var width && layout.Width != width)
				{
					layout.Width = width;
				}
		}
		base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
	}

	protected override void UpdateFlyoutSize(AView flyoutView)
	{
		base.UpdateFlyoutSize(flyoutView);
		if (flyoutView is not null && IsMaterial3)
			NativeStylesExtensions.StyleDrawerSheetShape(flyoutView);
	}
}
