using CoreGraphics;
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;
using LargeTitleDisplayMode = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.LargeTitleDisplayMode;
using PageSpecific = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page;

namespace NativeStyles;

public static partial class NativeStylesExtensions
{
	/// <summary>
	/// Shell without <see cref="NativeShellRenderer"/> (another renderer, or ReplaceShellRenderer = false): its
	/// controllers are reached after each navigation instead of from renderer hooks.
	/// </summary>
	static void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		if (sender is not Shell shell)
			return;
		ApplyTabBarMinimizeBehavior(shell);
		// The page's platform view is created after Navigated fires: register its scroll view on the next loop.
		MainThread.BeginInvokeOnMainThread(() =>
		{
			ApplyTabBarMinimizeBehavior(shell);
			ApplyLargeTitles(shell);
			ApplySegmentedTopTabs(shell);
			ApplySearchFieldColors(shell);
		});
		// A section shown for the first time builds its header while its view loads, after this callback
		// A flyout item shown for the first time swaps its controllers in a little later still
		shell.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () =>
		{
			ApplyLargeTitles(shell, expandCollapsedBar: true);
			// The search controller of a page shown for the first time is attached after the earlier passes
			ApplySearchFieldColors(shell);
		});
		shell.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(60), () =>
		{
			ApplyLargeTitles(shell);
			ApplySegmentedTopTabs(shell);
			ApplySearchFieldColors(shell);
		});
	}

	static void MapTabbedPageMinimize(Microsoft.Maui.Controls.Handlers.Compatibility.TabbedRenderer renderer, TabbedPage page)
	{
		var behavior = NativeShell.GetTabBarMinimizeBehavior(page);
		ApplyTabBarMinimizeBehavior(renderer, behavior);
		// The selected page's scroll view exists only after it has been shown
		page.Dispatcher.Dispatch(() => ApplyTabBarMinimizeBehavior(renderer, behavior));
	}

	static WeakReference<Application>? s_themeObservedApplication;

	/// <summary>
	/// A few native colors are copied from MAUI values (the page background behind the top-tabs control and behind a
	/// transparent navigation bar), so they are refreshed when the app theme changes at runtime.
	/// </summary>
	static void ObserveAppTheme()
	{
		if (Application.Current is not { } application
			|| (s_themeObservedApplication is not null && s_themeObservedApplication.TryGetTarget(out var observed) && ReferenceEquals(observed, application)))
		{
			return;
		}
		s_themeObservedApplication = new WeakReference<Application>(application);
		// RequestedThemeChanged is a weak event: a static method has no target that could be collected
		application.RequestedThemeChanged += OnAppThemeChanged;
	}

	static void OnAppThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
		// After MAUI has pushed the new AppThemeBinding values
		MainThread.BeginInvokeOnMainThread(() =>
		{
			// NativeShellRenderer follows the page colors itself
			if (Shell.Current is { Handler: not NativeShellRenderer } shell)
			{
				ApplySegmentedTopTabs(shell);
				ApplySearchFieldColors(shell);
			}
			foreach (var window in Application.Current?.Windows ?? [])
				RefreshNavigationPages(window.Page, 0);
		});
	}

	static void RefreshNavigationPages(Page? page, int depth)
	{
		if (page is null || depth > 8)
			return;
		switch (page)
		{
			case NavigationPage navigation:
				navigation.Handler?.UpdateValue(MappingKey);
				RefreshNavigationPages(navigation.CurrentPage, depth + 1);
				break;
			case TabbedPage tabbed:
				foreach (var child in tabbed.Children)
					RefreshNavigationPages(child, depth + 1);
				break;
			case FlyoutPage flyout:
				RefreshNavigationPages(flyout.Detail, depth + 1);
				break;
		}
		foreach (var modal in page.Navigation?.ModalStack ?? [])
			if (!ReferenceEquals(modal, page))
				RefreshNavigationPages(modal, depth + 1);
	}

	static void MapNavigationBar(Microsoft.Maui.Controls.Handlers.Compatibility.NavigationRenderer renderer, NavigationPage page)
	{
		ObserveAppTheme();
		// MAUI assigns the bar an explicit tint (system blue) when IconColor is not set, which hides the window tint
		if (s_brandTint is not null && (page.CurrentPage is null || NavigationPage.GetIconColor(page.CurrentPage) is null))
		{
			renderer.NavigationBar.TintColor = s_brandTint;
			// On iOS 26 MAUI copies the bar tint to every bar button item when it creates them, which may have happened
			// while the bar still reported the inherited system blue
			foreach (var controller in renderer.ViewControllers ?? [])
				foreach (var item in (controller.NavigationItem.RightBarButtonItems ?? []).Concat(controller.NavigationItem.LeftBarButtonItems ?? []))
					item.TintColor = s_brandTint;
		}
		if (page.BarBackgroundColor is not null || !Brush.IsNullOrEmpty(page.BarBackground))
			return;
		var appearance = new UINavigationBarAppearance();
		if (OperatingSystem.IsIOSVersionAtLeast(26))
			appearance.ConfigureWithTransparentBackground();
		else
			appearance.ConfigureWithDefaultBackground();
		var bar = renderer.NavigationBar;
		bar.StandardAppearance = appearance;
		bar.ScrollEdgeAppearance = appearance;
		bar.CompactAppearance = appearance;
		// MAUI lays the page out below the bar, so the transparent bar shows the navigation controller's own view
		if (page.CurrentPage?.BackgroundColor is { } background && renderer.View is { } view)
			view.BackgroundColor = background.ToPlatform();
	}

	/// <summary>
	/// MAUI's Shell applies Page.LargeTitleDisplay through the tab controller's selected navigation controller, which is
	/// not set yet when a flyout item is displayed for the first time, so those pages got an inline title. Re-apply it
	/// once navigation has completed. (Top-tab pages are switched back to inline by ApplySegmentedTopTabs.)
	/// </summary>
	static void ApplyLargeTitles(Shell shell, bool expandCollapsedBar = false)
	{
		if ((shell.Handler as IPlatformViewHandler)?.ViewController is not { } root || shell.CurrentPage is not { } page
			|| !page.IsSet(PageSpecific.LargeTitleDisplayProperty))
		{
			return;
		}
		var mode = PageSpecific.GetLargeTitleDisplay(page);
		// Top-tab sections use an inline title (see ApplySegmentedTopTabs)
		if (shell.CurrentItem?.CurrentItem is IShellSectionController section && section.GetItems().Count > 1)
			mode = LargeTitleDisplayMode.Never;
		foreach (var tabBarController in EnumerateTabBarControllers(root))
		{
			if (tabBarController.SelectedViewController is not UINavigationController navigation)
				continue;
			SetLargeTitle(navigation, mode);
			if (expandCollapsedBar)
				ExpandCollapsedLargeTitle(navigation, page);
		}
	}

	static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Page, object> s_largeTitleExpanded = new();

	/// <summary>
	/// UIKit leaves the bar collapsed when large titles are enabled after the content was laid out (MAUI does so the first
	/// time a flyout item is displayed). The first time a page is shown, if its bar is collapsed while the content rests
	/// at the top, expand it (never again, so a position the user scrolled to is kept).
	/// </summary>
	internal static void ExpandCollapsedLargeTitle(UINavigationController navigation, Page page)
	{
		if (!navigation.NavigationBar.PrefersLargeTitles
			|| !page.IsSet(PageSpecific.LargeTitleDisplayProperty) || PageSpecific.GetLargeTitleDisplay(page) != LargeTitleDisplayMode.Always
			|| s_largeTitleExpanded.TryGetValue(page, out _)
			|| navigation.TopViewController?.View is not { Window: not null } view || FindScrollView(view) is not { } scrollView)
		{
			return;
		}
		s_largeTitleExpanded.Add(page, page);
		var collapsed = navigation.NavigationBar.Frame.Height < 60;
		var atRest = Math.Abs(scrollView.ContentOffset.Y + scrollView.AdjustedContentInset.Top) < 1;
		if (collapsed && atRest)
		{
			navigation.NavigationBar.SizeToFit();
			navigation.View?.SetNeedsLayout();
			navigation.View?.LayoutIfNeeded();
			scrollView.SetContentOffset(new CGPoint(scrollView.ContentOffset.X, -scrollView.AdjustedContentInset.Top), false);
		}
	}

	/// <summary>
	/// NativeShellRenderer: the large title of a Shell section's navigation bar, as MAUI applies it from
	/// Page.LargeTitleDisplay, except in a section with top tabs, which uses an inline title (a large title does not
	/// track the scroll view of a top-tab page, nested below MAUI's header). Idempotent, so every hook that may follow
	/// MAUI's own update can call it.
	/// </summary>
	internal static void ApplySectionLargeTitle(UINavigationController navigation, ShellSection section)
	{
		var topTabs = HasTopTabs(section);
		if (topTabs && navigation.ViewControllers is [var root, ..] && root.NavigationItem.LargeTitleDisplayMode != UINavigationItemLargeTitleDisplayMode.Never)
			root.NavigationItem.LargeTitleDisplayMode = UINavigationItemLargeTitleDisplayMode.Never;
		if (((IShellSectionController)section).PresentedPage is not { } page || !page.IsSet(PageSpecific.LargeTitleDisplayProperty))
			return;
		SetLargeTitle(navigation, topTabs ? LargeTitleDisplayMode.Never : PageSpecific.GetLargeTitleDisplay(page));
	}

	internal static bool HasTopTabs(ShellSection section) => ((IShellSectionController)section).GetItems().Count > 1;

	static void SetLargeTitle(UINavigationController navigation, LargeTitleDisplayMode mode)
	{
		var prefersLargeTitles = mode != LargeTitleDisplayMode.Never;
		if (navigation.NavigationBar.PrefersLargeTitles != prefersLargeTitles)
			navigation.NavigationBar.PrefersLargeTitles = prefersLargeTitles;
		var itemMode = mode switch
		{
			LargeTitleDisplayMode.Always => UINavigationItemLargeTitleDisplayMode.Always,
			LargeTitleDisplayMode.Never => UINavigationItemLargeTitleDisplayMode.Never,
			_ => UINavigationItemLargeTitleDisplayMode.Automatic,
		};
		if (navigation.TopViewController is { } top && top.NavigationItem.LargeTitleDisplayMode != itemMode)
			top.NavigationItem.LargeTitleDisplayMode = itemMode;
	}

	/// <summary>
	/// Shell.SearchHandler: MAUI builds the navigation-bar search field's placeholder and text with colors resolved once,
	/// so they keep the previous appearance after a runtime light/dark switch. Unless the handler sets its own colors,
	/// use the dynamic system colors, which follow the trait collection by themselves.
	/// </summary>
	static void ApplySearchFieldColors(Shell shell)
	{
		if ((shell.Handler as IPlatformViewHandler)?.ViewController is not { } root
			|| shell.CurrentPage is not { } page || Shell.GetSearchHandler(page) is not { } search)
		{
			return;
		}
		foreach (var controller in EnumerateControllers<UIViewController>(root))
			ApplySearchFieldColors(controller.NavigationItem, search);
	}

	internal static void ApplySearchFieldColors(UINavigationItem? item, SearchHandler search)
	{
		if (item?.SearchController?.SearchBar.SearchTextField is not { } field)
			return;
		if (search.TextColor is null)
			field.TextColor = UIColor.Label;
		if (search.PlaceholderColor is null && !string.IsNullOrEmpty(search.Placeholder))
			field.AttributedPlaceholder = new NSAttributedString(search.Placeholder, foregroundColor: UIColor.PlaceholderText);
	}

	const int TopTabsOverlayTag = 0x4E5354;

	/// <summary>
	/// Shell top tabs: MAUI draws an Android-like strip of underlined labels. iOS switches between sibling views with a
	/// UISegmentedControl, so one is laid over MAUI's header and drives ShellSection.CurrentItem.
	/// </summary>
	static void ApplySegmentedTopTabs(Shell shell)
	{
		if ((shell.Handler as IPlatformViewHandler)?.ViewController is not { } root)
			return;
		foreach (var header in EnumerateControllers<Microsoft.Maui.Controls.Platform.Compatibility.ShellSectionRootHeader>(root))
		{
			if (header.ShellSection is not { } section || header.CollectionView is not { } strip)
				continue;
			// A large title does not track the scroll view of a top-tab page (it is nested below MAUI's header), so content
			// would slide under the still-expanded title. Like native screens with a segmented control under the bar, use
			// an inline title here.
			if (header.ParentViewController is { } host)
				host.NavigationItem.LargeTitleDisplayMode = UINavigationItemLargeTitleDisplayMode.Never;
			StyleTopTabsStrip(strip, section, (shell.CurrentPage?.BackgroundColor)?.ToPlatform());
		}
	}

	/// <summary>
	/// Lays the segmented control over MAUI's top-tabs strip (a collection view) or updates it: segments and selection
	/// from the section, the page background behind it, MAUI's hairline hidden.
	/// </summary>
	internal static void StyleTopTabsStrip(UICollectionView strip, ShellSection section, UIColor? background)
	{
		var items = ((IShellSectionController)section).GetItems();
		// The overlay lives inside MAUI's strip so it follows it when the large title collapses or the device rotates
		var overlay = strip.ViewWithTag(TopTabsOverlayTag);
		UISegmentedControl control;
		if (overlay is null)
		{
			overlay = new UIView(strip.Bounds) { Tag = TopTabsOverlayTag, AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight };
			overlay.Layer.ZPosition = 10000; // MAUI's selection bar uses 9001
			control = new UISegmentedControl { TranslatesAutoresizingMaskIntoConstraints = false };
			control.ValueChanged += (_, _) =>
			{
				var current = ((IShellSectionController)section).GetItems();
				if (control.SelectedSegment >= 0 && control.SelectedSegment < current.Count)
					section.CurrentItem = current[(int)control.SelectedSegment];
			};
			overlay.AddSubview(control);
			strip.AddSubview(overlay);
			strip.ScrollEnabled = false;
			strip.ShowsHorizontalScrollIndicator = false;
			// VoiceOver: the segmented control stands for the whole strip; MAUI's cells below it stay hidden
			// (UIAccessibilityContainer.accessibilityElements, which the UIView binding does not expose)
			strip.SetValueForKey(NSArray.FromNSObjects(control), new NSString("accessibilityElements"));
			NSLayoutConstraint.ActivateConstraints(
			[
				control.LeadingAnchor.ConstraintEqualTo(overlay.LeadingAnchor, 20),
				control.TrailingAnchor.ConstraintEqualTo(overlay.TrailingAnchor, -20),
				control.CenterYAnchor.ConstraintEqualTo(overlay.CenterYAnchor),
			]);
		}
		else
		{
			control = (UISegmentedControl)overlay.Subviews[0];
		}

		overlay.BackgroundColor = background ?? UIColor.SystemBackground;
		strip.BringSubviewToFront(overlay);
		// MAUI draws a 30% black hairline below the strip (a 1pt subview kept just under its bounds); iOS 26 has none
		foreach (var subview in strip.Subviews)
			if (subview != overlay && subview.Frame.Height <= 1)
				subview.Hidden = true;
		HideFromAccessibility(strip.VisibleCells);
		if (control.NumberOfSegments != items.Count || Enumerable.Range(0, items.Count).Any(i => control.TitleAt(i) != items[i].Title))
		{
			control.RemoveAllSegments();
			for (var i = 0; i < items.Count; i++)
				control.InsertSegment(items[i].Title ?? string.Empty, i, false);
		}
		if (items.IndexOf(section.CurrentItem) is var selected && control.SelectedSegment != selected)
			control.SelectedSegment = selected;
	}

	internal static void HideFromAccessibility(IEnumerable<UIView> views)
	{
		foreach (var view in views)
		{
			view.IsAccessibilityElement = false;
			view.AccessibilityElementsHidden = true;
		}
	}

	static IEnumerable<T> EnumerateControllers<T>(UIViewController controller) where T : UIViewController
	{
		if (controller is T match)
			yield return match;
		foreach (var child in controller.ChildViewControllers)
			foreach (var found in EnumerateControllers<T>(child))
				yield return found;
	}

	static void ApplyTabBarMinimizeBehavior(Shell shell, bool onlyIfChanged = false)
	{
		if ((shell.Handler as IPlatformViewHandler)?.ViewController is { } root)
			ApplyTabBarMinimizeBehavior(root, NativeShell.GetTabBarMinimizeBehavior(shell), onlyIfChanged);
	}

	static void ApplyTabBarMinimizeBehavior(UIViewController root, TabBarMinimizeBehavior requested, bool onlyIfChanged = false)
	{
		if (!OperatingSystem.IsIOSVersionAtLeast(26))
			return;
		foreach (var tabBarController in EnumerateTabBarControllers(root))
		{
			SetTabBarMinimizeBehavior(tabBarController, requested, onlyIfChanged);
			var visible = tabBarController.SelectedViewController is UINavigationController nav ? nav.TopViewController : tabBarController.SelectedViewController;
			if (visible?.View is { } view)
				RegisterContentScrollView(visible, view);
		}
	}

	/// <summary>
	/// <c>onlyIfChanged</c>: skip the assignment when the controller already has the behavior. Assigning the same value
	/// again is not a no-op for UIKit: done before the controller is on screen, the glass tab bar then renders slightly
	/// differently.
	/// </summary>
	internal static void SetTabBarMinimizeBehavior(UITabBarController tabBarController, TabBarMinimizeBehavior requested, bool onlyIfChanged = false)
	{
		if (!OperatingSystem.IsIOSVersionAtLeast(26))
			return;
		var behavior = requested switch
		{
			TabBarMinimizeBehavior.Never => UITabBarMinimizeBehavior.Never,
			TabBarMinimizeBehavior.OnScrollDown => UITabBarMinimizeBehavior.OnScrollDown,
			TabBarMinimizeBehavior.OnScrollUp => UITabBarMinimizeBehavior.OnScrollUp,
			_ => UITabBarMinimizeBehavior.Automatic,
		};
		if (!onlyIfChanged || tabBarController.TabBarMinimizeBehavior != behavior)
			tabBarController.TabBarMinimizeBehavior = behavior;
	}

	/// <summary>
	/// UIKit minimizes the tab bar by observing the content scroll view of the visible controller; MAUI's ScrollView is
	/// nested inside container views, so it must be registered explicitly.
	/// </summary>
	internal static void RegisterContentScrollView(UIViewController controller, UIView content)
	{
		if (OperatingSystem.IsIOSVersionAtLeast(26) && FindScrollView(content) is { } scrollView)
			controller.SetContentScrollView(scrollView, NSDirectionalRectEdge.Bottom);
	}

	static UIScrollView? FindScrollView(UIView view)
	{
		if (view is UIScrollView scroll && view is not UITableView && view is not UICollectionView)
			return scroll;
		foreach (var child in view.Subviews)
			if (FindScrollView(child) is { } found)
				return found;
		return null;
	}

	static IEnumerable<UITabBarController> EnumerateTabBarControllers(UIViewController controller)
	{
		if (controller is UITabBarController tabs)
			yield return tabs;
		foreach (var child in controller.ChildViewControllers)
			foreach (var found in EnumerateTabBarControllers(child))
				yield return found;
		if (controller.PresentedViewController is { } presented)
			foreach (var found in EnumerateTabBarControllers(presented))
				yield return found;
	}
}
