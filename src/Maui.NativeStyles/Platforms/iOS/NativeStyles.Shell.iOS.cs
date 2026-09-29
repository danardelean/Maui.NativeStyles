using CoreGraphics;
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;

namespace NativeStyles;

public static partial class NativeStylesExtensions
{
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
			if (Shell.Current is { } shell)
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
			|| !page.IsSet(Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty))
		{
			return;
		}
		var mode = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page.GetLargeTitleDisplay(page);
		// Top-tab sections use an inline title (see ApplySegmentedTopTabs)
		if (shell.CurrentItem?.CurrentItem is IShellSectionController section && section.GetItems().Count > 1)
			mode = Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.LargeTitleDisplayMode.Never;
		foreach (var tabBarController in EnumerateTabBarControllers(root))
		{
			if (tabBarController.SelectedViewController is not UINavigationController navigation)
				continue;
			navigation.NavigationBar.PrefersLargeTitles = mode != Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.LargeTitleDisplayMode.Never;
			if (navigation.TopViewController is { } top)
				top.NavigationItem.LargeTitleDisplayMode = mode switch
				{
					Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.LargeTitleDisplayMode.Always => UINavigationItemLargeTitleDisplayMode.Always,
					Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.LargeTitleDisplayMode.Never => UINavigationItemLargeTitleDisplayMode.Never,
					_ => UINavigationItemLargeTitleDisplayMode.Automatic,
				};

			// UIKit leaves the bar collapsed when large titles are enabled after the content was laid out. The first time a
			// page is shown, if its bar is collapsed while the content rests at the top, expand it (never again, so a
			// position the user scrolled to is kept).
			if (expandCollapsedBar && navigation.NavigationBar.PrefersLargeTitles
				&& mode == Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.LargeTitleDisplayMode.Always
				&& !s_largeTitleExpanded.TryGetValue(page, out _)
				&& navigation.TopViewController?.View is { Window: not null } view && FindScrollView(view) is { } scrollView)
			{
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
		}
	}

	static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Page, object> s_largeTitleExpanded = new();

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
		{
			if (controller.NavigationItem?.SearchController?.SearchBar.SearchTextField is not { } field)
				continue;
			if (search.TextColor is null)
				field.TextColor = UIColor.Label;
			if (search.PlaceholderColor is null && !string.IsNullOrEmpty(search.Placeholder))
				field.AttributedPlaceholder = new NSAttributedString(search.Placeholder, foregroundColor: UIColor.PlaceholderText);
		}
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

			// A large title does not track the scroll view of a top-tab page (it is nested below MAUI's header), so content
			// would slide under the still-expanded title. Like native screens with a segmented control under the bar, use
			// an inline title here.
			if (header.ParentViewController is { } host)
				host.NavigationItem.LargeTitleDisplayMode = UINavigationItemLargeTitleDisplayMode.Never;

			overlay.BackgroundColor = (shell.CurrentPage?.BackgroundColor)?.ToPlatform() ?? UIColor.SystemBackground;
			strip.BringSubviewToFront(overlay);
			// MAUI draws a 30% black hairline below the strip (a 1pt subview kept just under its bounds); iOS 26 has none
			foreach (var subview in strip.Subviews)
				if (subview != overlay && subview.Frame.Height <= 1)
					subview.Hidden = true;
			if (control.NumberOfSegments != items.Count || Enumerable.Range(0, items.Count).Any(i => control.TitleAt(i) != items[i].Title))
			{
				control.RemoveAllSegments();
				for (var i = 0; i < items.Count; i++)
					control.InsertSegment(items[i].Title ?? string.Empty, i, false);
			}
			control.SelectedSegment = items.IndexOf(section.CurrentItem);
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

	static void ApplyTabBarMinimizeBehavior(Shell shell)
	{
		if ((shell.Handler as IPlatformViewHandler)?.ViewController is { } root)
			ApplyTabBarMinimizeBehavior(root, NativeShell.GetTabBarMinimizeBehavior(shell));
	}

	static void ApplyTabBarMinimizeBehavior(UIViewController root, TabBarMinimizeBehavior requested)
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
		foreach (var tabBarController in EnumerateTabBarControllers(root))
		{
			tabBarController.TabBarMinimizeBehavior = behavior;
			// UIKit minimizes the bar by observing the content scroll view of the visible controller; MAUI's
			// ScrollView is nested inside container views, so it must be registered explicitly.
			var visible = tabBarController.SelectedViewController is UINavigationController nav ? nav.TopViewController : tabBarController.SelectedViewController;
			if (visible?.View is { } view && FindScrollView(view) is { } scrollView)
				visible.SetContentScrollView(scrollView, NSDirectionalRectEdge.Bottom);
		}
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
