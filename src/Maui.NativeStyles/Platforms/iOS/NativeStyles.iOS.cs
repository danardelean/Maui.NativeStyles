using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;
using PlatformContentView = Microsoft.Maui.Platform.ContentView;

namespace NativeStyles;

public static partial class NativeStylesExtensions
{
	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers)
	{
		handlers.AddHandler<GlassView, GlassViewHandler>();
		handlers.AddHandler<SegmentedControl, SegmentedControlHandler>();
		// Entry: same EntryHandler and mapper, but a UITextField that supports text insets and continuous corners.
		handlers.AddHandler<Entry, NativeEntryHandler>();
	}

	static partial void RegisterPlatformMappers(NativeStylesOptions options)
	{
		// Brand: iOS brands an app through the window tint; every control that draws with the tint color follows
		// (buttons, links, selection, toolbar and tab bar items, alerts).
		if (options.Brand?.ResolveAccent() is { } accent)
		{
			var light = accent.Light.ToPlatform();
			var dark = accent.Dark.ToPlatform();
			var tint = UIColor.FromDynamicProvider(traits => traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? dark : light);
			s_brandTint = tint;
			WindowHandler.Mapper.AppendToMapping(MappingKey, (handler, _) => handler.PlatformView.TintColor = tint);
		}

		// UIButtonConfiguration (iOS 15+) / Liquid Glass (iOS 26+), driven by the NativeButton attached properties.
		// Re-applied after every MAUI mapping that would otherwise overwrite the configuration.
		ButtonHandler.Mapper.AppendToMapping(MappingKey, MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IButton.Background), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IButtonStroke.CornerRadius), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IButtonStroke.StrokeThickness), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.TextColor), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IText.Text), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IImageSourcePart.Source), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IPadding.Padding), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping("LineBreakMode", MapButtonConfiguration);

		// Entry: iOS 26 text fields are borderless rows on a filled, continuously rounded shape (Settings > Name),
		// not the legacy UITextBorderStyle.RoundedRect. NativeEntry.IsPlain drops the shape for use inside grouped cells.
		EntryHandler.Mapper.AppendToMapping(MappingKey, MapEntryChrome);

		// Editor: same filled shape, text inset like a grouped row.
		EditorHandler.Mapper.AppendToMapping(MappingKey, MapEditorChrome);

		// Pickers are UITextFields in MAUI; iOS 26 shows them as a pull-down value (Picker) or a compact pill
		// (DatePicker / TimePicker), never as a bordered text field.
		PickerHandler.Mapper.AppendToMapping(MappingKey, MapPickerChrome);
		PickerHandler.Mapper.AppendToMapping(nameof(IView.Background), MapPickerChrome);
		DatePickerHandler.Mapper.AppendToMapping(MappingKey, (h, v) => MapCompactPickerChrome(h.PlatformView, v));
		DatePickerHandler.Mapper.AppendToMapping(nameof(IView.Background), (h, v) => MapCompactPickerChrome(h.PlatformView, v));
		TimePickerHandler.Mapper.AppendToMapping(MappingKey, (h, v) => MapCompactPickerChrome(h.PlatformView, v));
		TimePickerHandler.Mapper.AppendToMapping(nameof(IView.Background), (h, v) => MapCompactPickerChrome(h.PlatformView, v));

		// NativeImage.TintColor: template rendering, re-applied when MAUI finishes loading the image source.
		ImageHandler.Mapper.AppendToMapping(MappingKey, MapImageTint);
		ImageButtonHandler.Mapper.AppendToMapping(MappingKey, MapImageButtonTint);

		// Label: MAUI FontAttributes has no Semibold; NativeText.Weight supplies the SF Pro weight.
		LabelHandler.Mapper.AppendToMapping(MappingKey, MapLabelWeight);
		LabelHandler.Mapper.AppendToMapping(nameof(ILabel.Font), MapLabelWeight);

		// NavigationPage: MAUI installs an opaque bar with a hairline; iOS 26 bars are transparent over the content
		// (the system scroll-edge effect takes care of legibility) unless the app sets a bar color.
		foreach (var key in new[] { MappingKey, NavigationPage.BarBackgroundColorProperty.PropertyName, NavigationPage.BarBackgroundProperty.PropertyName, NavigationPage.CurrentPageProperty.PropertyName, NavigationPage.IconColorProperty.PropertyName, NavigationPage.BarTextColorProperty.PropertyName })
			Microsoft.Maui.Controls.Handlers.Compatibility.NavigationRenderer.Mapper.AppendToMapping(key, MapNavigationBar);

		// TabbedPage: NativeShell.TabBarMinimizeBehavior works on it as well.
		Microsoft.Maui.Controls.Handlers.Compatibility.TabbedRenderer.Mapper.AppendToMapping(MappingKey, MapTabbedPageMinimize);
		Microsoft.Maui.Controls.Handlers.Compatibility.TabbedRenderer.Mapper.AppendToMapping(nameof(TabbedPage.CurrentPage), MapTabbedPageMinimize);

		// Shell: iOS 26 tab bar minimize behavior. The UITabBarController is created per ShellItem after navigation,
		// so the value is applied on every Navigated event.
		Microsoft.Maui.Controls.Handlers.Compatibility.ShellRenderer.Mapper.AppendToMapping(MappingKey, (handler, shell) =>
		{
			if (shell is not Shell s || !OperatingSystem.IsIOSVersionAtLeast(26))
				return;
			s.Navigated -= OnShellNavigated;
			s.Navigated += OnShellNavigated;
			ObserveAppTheme();
			ApplyTabBarMinimizeBehavior(s);
		});
	}

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
		shell.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () => ApplyLargeTitles(shell, expandCollapsedBar: true));
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

	static UIColor? s_brandTint;

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

	static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageButton, object> s_tintedButtons = new();

	static void MapImageButtonTint(IImageButtonHandler handler, IImageButton button)
	{
		if (button is not BindableObject bindable)
			return;
		var view = handler.PlatformView;
		var tint = NativeImage.GetTintColor(bindable);
		if (tint is null)
			return;
		view.TintColor = tint.ToPlatform();
		ApplyTemplate(view);

		// MAUI assigns the button image when the source finishes loading (IsLoading goes back to false)
		if (button is ImageButton element && !s_tintedButtons.TryGetValue(element, out _))
		{
			s_tintedButtons.Add(element, element);
			element.PropertyChanged += (sender, e) =>
			{
				if (e.PropertyName == ImageButton.IsLoadingProperty.PropertyName && sender is ImageButton { IsLoading: false, Handler: IImageButtonHandler current }
					&& NativeImage.GetTintColor((ImageButton)sender) is not null)
				{
					ApplyTemplate(current.PlatformView);
				}
			};
		}

		static void ApplyTemplate(UIButton target)
		{
			if (target.ImageForState(UIControlState.Normal) is { RenderingMode: not UIImageRenderingMode.AlwaysTemplate } original)
				target.SetImage(original.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate), UIControlState.Normal);
		}
	}

	static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Image, object> s_tintedImages = new();

	static void MapImageTint(IImageHandler handler, Microsoft.Maui.IImage image)
	{
		if (image is not BindableObject bindable)
			return;
		var view = handler.PlatformView;
		var tint = NativeImage.GetTintColor(bindable);
		view.TintColor = tint?.ToPlatform();
		if (tint is null)
			return;
		ApplyTemplate(view);

		// MAUI assigns UIImageView.Image when the source finishes loading (IsLoading goes back to false)
		if (image is Image element && !s_tintedImages.TryGetValue(element, out _))
		{
			s_tintedImages.Add(element, element);
			element.PropertyChanged += (sender, e) =>
			{
				if (e.PropertyName == Image.IsLoadingProperty.PropertyName && sender is Image { IsLoading: false, Handler: IImageHandler current }
					&& NativeImage.GetTintColor((Image)sender) is not null)
				{
					ApplyTemplate(current.PlatformView);
				}
			};
		}

		static void ApplyTemplate(UIImageView target)
		{
			if (target.Image is { RenderingMode: not UIImageRenderingMode.AlwaysTemplate } original)
				target.Image = original.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
		}
	}

	/// <summary>Corner radius of iOS 26 grouped shapes; a 52pt single-row field therefore reads as a capsule.</summary>
	const float FieldCornerRadius = 26;

	/// <summary>Leading/trailing content margin of an iOS 26 grouped row.</summary>
	const float FieldContentMargin = 20;

	static void MapEntryChrome(IEntryHandler handler, IEntry entry)
	{
		if (entry is not BindableObject bindable)
			return;
		var field = handler.PlatformView;
		field.BorderStyle = UITextBorderStyle.None;
		if (field is NativeTextField native)
		{
			var plain = NativeEntry.GetIsPlain(bindable);
			native.CornerRadius = plain ? 0 : FieldCornerRadius;
			native.ContentMargin = plain ? 0 : FieldContentMargin;
		}
	}

	static void MapEditorChrome(IEditorHandler handler, IEditor editor)
	{
		var view = handler.PlatformView;
		view.Layer.CornerRadius = FieldCornerRadius;
		view.Layer.CornerCurve = CoreAnimation.CACornerCurve.Continuous;
		view.ClipsToBounds = true;
		view.TextContainer.LineFragmentPadding = FieldContentMargin;
		view.TextContainerInset = new UIEdgeInsets(15, 0, 15, 0);
	}

	/// <summary>Pull-down look: no border, value text followed by the chevron.up.chevron.down glyph.</summary>
	static void MapPickerChrome(IPickerHandler handler, IPicker picker)
	{
		var field = handler.PlatformView;
		field.BorderStyle = UITextBorderStyle.None;
		field.BackgroundColor = UIColor.Clear;
		if (field.RightView is not UIImageView)
		{
			var chevrons = new UIImageView(UIImage.GetSystemImage("chevron.up.chevron.down",
				UIImageSymbolConfiguration.Create(UIFont.SystemFontOfSize(13, UIFontWeight.Semibold))))
			{
				TintColor = UIColor.SecondaryLabel,
				ContentMode = UIViewContentMode.Center,
				Frame = new CGRect(0, 0, 22, 20),
			};
			field.RightView = chevrons;
			field.RightViewMode = UITextFieldViewMode.Always;
		}
	}

	/// <summary>Compact UIDatePicker look (iOS 26): tertiarySystemFill capsule, 34 pt tall, 12 pt horizontal padding.</summary>
	static void MapCompactPickerChrome(UITextField field, IView view)
	{
		field.BorderStyle = UITextBorderStyle.None;
		field.BackgroundColor = (view.Background as SolidPaint)?.Color?.ToPlatform() ?? UIColor.TertiarySystemFill;
		field.Layer.CornerRadius = 17; // capsule for the 34 pt height set by the style
		field.ClipsToBounds = true;
		field.TextAlignment = UITextAlignment.Center;
		if (field.LeftView is null)
		{
			field.LeftView = new UIView(new CGRect(0, 0, 12, 1));
			field.LeftViewMode = UITextFieldViewMode.Always;
			field.RightView = new UIView(new CGRect(0, 0, 12, 1));
			field.RightViewMode = UITextFieldViewMode.Always;
		}
	}

	static void MapLabelWeight(ILabelHandler handler, ILabel label)
	{
		if (label is not BindableObject bindable || handler.PlatformView.Font is not { } current)
			return;

		var weight = NativeText.GetWeight(bindable) switch
		{
			TextWeight.Medium => UIFontWeight.Medium,
			TextWeight.Semibold => UIFontWeight.Semibold,
			TextWeight.Bold => UIFontWeight.Bold,
			_ => (UIFontWeight?)null,
		};
		if (weight is { } w)
			handler.PlatformView.Font = UIFont.SystemFontOfSize(current.PointSize, w)!;
	}

	static readonly UIButtonConfigurationUpdateHandler TemplateImageUpdateHandler = button =>
	{
		var image = button.Configuration?.Image ?? button.ImageForState(UIControlState.Normal);
		if (image is not { RenderingMode: not UIImageRenderingMode.AlwaysTemplate } || button.Configuration is not { } current)
			return;
		current.Image = image.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
		if (current.ImagePadding == 0)
			current.ImagePadding = 8;
		button.Configuration = current;
	};

	static void MapButtonConfiguration(IButtonHandler handler, IButton button)
	{
		if (!OperatingSystem.IsIOSVersionAtLeast(15) || button is not BindableObject bindable)
			return;

		var platformButton = handler.PlatformView;
		var glass = OperatingSystem.IsIOSVersionAtLeast(26);
		var kind = NativeButton.GetKind(bindable);
		var destructive = NativeButton.GetIsDestructive(bindable);

		var (config, prominent) = kind switch
		{
			ButtonKind.GlassProminent => (glass ? UIButtonConfiguration.ProminentGlassButtonConfiguration : UIButtonConfiguration.FilledButtonConfiguration, true),
			ButtonKind.Glass => (glass ? UIButtonConfiguration.GlassButtonConfiguration : UIButtonConfiguration.GrayButtonConfiguration, false),
			ButtonKind.Filled => (UIButtonConfiguration.FilledButtonConfiguration, true),
			ButtonKind.Tonal => (UIButtonConfiguration.TintedButtonConfiguration, false),
			ButtonKind.Outlined => (UIButtonConfiguration.GrayButtonConfiguration, false),
			_ => (UIButtonConfiguration.PlainButtonConfiguration, false),
		};

		// iOS 26: every button outside bars is a capsule.
		config.CornerStyle = UIButtonConfigurationCornerStyle.Capsule;
		config.ButtonSize = NativeButton.GetSize(bindable) switch
		{
			ControlSize.Small => UIButtonConfigurationSize.Small,
			ControlSize.Large => UIButtonConfigurationSize.Large,
			_ => UIButtonConfigurationSize.Medium,
		};

		if (button is IText text)
			config.Title = text.Text;

		if (button is ITextStyle textStyle)
		{
			var size = textStyle.Font.Size > 0 ? (nfloat)textStyle.Font.Size : UIFont.ButtonFontSize;
			var weight = textStyle.Font.Weight >= FontWeight.Bold ? UIFontWeight.Bold
				: prominent || textStyle.Font.Weight >= FontWeight.Semibold ? UIFontWeight.Semibold
				: UIFontWeight.Regular;
			var font = UIFont.SystemFontOfSize(size, weight);
			config.TitleTextAttributesTransformer = attrs =>
				new UIStringAttributes(attrs) { Font = font }.Dictionary;
		}

		// Explicit XAML colors win; otherwise the system tint, or systemRed for destructive buttons.
		var textColor = (button as ITextStyle)?.TextColor;
		var background = (button.Background as SolidPaint)?.Color;

		if (background is not null)
			config.BaseBackgroundColor = background.ToPlatform();
		else if (destructive && prominent)
			config.BaseBackgroundColor = UIColor.SystemRed;

		if (textColor is not null)
			config.BaseForegroundColor = textColor.ToPlatform();
		else if (destructive && !prominent)
			config.BaseForegroundColor = UIColor.SystemRed;

		// NativeButton.TintsImage / NativeImage.TintColor: template image in the label color (or an explicit one)
		var explicitTint = NativeImage.GetTintColor(bindable)?.ToPlatform();
		var tintsImage = explicitTint is not null || NativeButton.GetTintsImage(bindable);
		if (platformButton.CurrentImage is { } image)
		{
			config.Image = tintsImage ? image.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate) : image;
			config.ImagePadding = 8;
		}
		if (explicitTint is not null)
			config.ImageColorTransformer = _ => explicitTint;
		// MAUI assigns the image when its source finishes loading, after this mapping ran: convert it then
		platformButton.ConfigurationUpdateHandler = tintsImage ? TemplateImageUpdateHandler : null;

		// Keep MAUI's measurement and UIKit's rendering in agreement: the configuration owns the insets.
		var padding = button.Padding;
		config.ContentInsets = new NSDirectionalEdgeInsets(
			(nfloat)padding.Top, (nfloat)padding.Left, (nfloat)padding.Bottom, (nfloat)padding.Right);
		config.TitleLineBreakMode = UILineBreakMode.TailTruncation;

		// Undo what MAUI's own mappers painted so the configuration is the only source of truth.
		platformButton.BackgroundColor = UIColor.Clear;
		platformButton.Layer.CornerRadius = 0;
		platformButton.Layer.BorderWidth = 0;
		platformButton.Configuration = config;
		platformButton.TitleLabel.Lines = 1;
		button.InvalidateMeasure();
	}
}

/// <summary><see cref="EntryHandler"/> that creates a <see cref="NativeTextField"/>; mappings are unchanged.</summary>
public class NativeEntryHandler : EntryHandler
{
	protected override MauiTextField CreatePlatformView()
	{
		// The base view carries MAUI's "Done" accessory toolbar, which resolves the field through the handler.
		var template = base.CreatePlatformView();
		var field = new NativeTextField
		{
			BorderStyle = UITextBorderStyle.None,
			ClipsToBounds = true,
			InputAccessoryView = template.InputAccessoryView,
		};
		template.InputAccessoryView = null;
		return field;
	}
}

/// <summary>UITextField with a horizontal content margin and continuous rounded corners.</summary>
public class NativeTextField : MauiTextField
{
	// Distance of the clear button's center from the trailing edge in a Settings text row.
	const float ClearButtonCenterInset = 35;

	nfloat _cornerRadius;
	nfloat _contentMargin;

	public nfloat CornerRadius
	{
		get => _cornerRadius;
		set { _cornerRadius = value; SetNeedsLayout(); }
	}

	public nfloat ContentMargin
	{
		get => _contentMargin;
		set { _contentMargin = value; SetNeedsLayout(); }
	}

	public override void LayoutSubviews()
	{
		base.LayoutSubviews();
		Layer.CornerCurve = CoreAnimation.CACornerCurve.Continuous;
		Layer.CornerRadius = (nfloat)Math.Min(_cornerRadius, Bounds.Height / 2);
	}

	public override CGRect TextRect(CGRect forBounds) => Inset(base.TextRect(forBounds));

	public override CGRect EditingRect(CGRect forBounds) => Inset(base.EditingRect(forBounds));

	public override CGRect ClearButtonRect(CGRect forBounds)
	{
		var rect = base.ClearButtonRect(forBounds);
		if (_contentMargin <= 0)
			return rect;
		var leftToRight = EffectiveUserInterfaceLayoutDirection == UIUserInterfaceLayoutDirection.LeftToRight;
		var centerX = leftToRight ? forBounds.Right - ClearButtonCenterInset : forBounds.Left + ClearButtonCenterInset;
		return new CGRect(centerX - rect.Width / 2, rect.Y, rect.Width, rect.Height);
	}

	CGRect Inset(CGRect rect)
	{
		if (_contentMargin <= 0)
			return rect;
		var width = (nfloat)Math.Max(0, rect.Width - 2 * _contentMargin);
		return new CGRect(rect.X + _contentMargin, rect.Y, width, rect.Height);
	}
}

/// <summary>Hosts a UIGlassEffect (iOS 26) or a system-material blur behind the MAUI content.</summary>
public class GlassViewHandler : ContentViewHandler
{
	public static readonly IPropertyMapper<GlassView, GlassViewHandler> GlassMapper =
		new PropertyMapper<GlassView, GlassViewHandler>(ContentViewHandler.Mapper)
		{
			[nameof(GlassView.GlassStyle)] = MapGlass,
			[nameof(GlassView.CornerRadius)] = MapGlass,
			[nameof(GlassView.TintColor)] = MapGlass,
			[nameof(GlassView.IsInteractive)] = MapGlass,
		};

	public GlassViewHandler() : base(GlassMapper)
	{
	}

	protected override PlatformContentView CreatePlatformView()
	{
		_ = VirtualView ?? throw new InvalidOperationException($"{nameof(VirtualView)} must be set.");
		return new GlassContentView { CrossPlatformLayout = VirtualView };
	}

	static void MapGlass(GlassViewHandler handler, GlassView view)
	{
		if (handler.PlatformView is GlassContentView glassView)
			glassView.Update(view);
	}
}

public class GlassContentView : PlatformContentView
{
	readonly UIVisualEffectView _effectView = new();
	double _cornerRadius = -1;

	public void Update(GlassView view)
	{
		_cornerRadius = view.CornerRadius;
		if (OperatingSystem.IsIOSVersionAtLeast(26))
		{
			var effect = UIGlassEffect.Create(view.GlassStyle == GlassStyle.Clear ? UIGlassEffectStyle.Clear : UIGlassEffectStyle.Regular);
			effect.Interactive = view.IsInteractive;
			effect.TintColor = view.TintColor?.ToPlatform();
			_effectView.Effect = effect;
			// Liquid Glass ignores Layer.CornerRadius: shape must come from UICornerConfiguration.
			_effectView.CornerConfiguration = _cornerRadius < 0
				? UICornerConfiguration.CreateCapsule()
				: UICornerConfiguration.CreateUniformCorners(UICornerRadius.CreateFixed((nfloat)_cornerRadius));
		}
		else
		{
			_effectView.Effect = UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemMaterial);
			_effectView.ClipsToBounds = true;
		}
		SetNeedsLayout();
	}

	public override void LayoutSubviews()
	{
		base.LayoutSubviews();

		// MAUI's ContentViewHandler clears all subviews when Content changes: keep the glass at the back.
		if (_effectView.Superview != this)
			InsertSubview(_effectView, 0);
		_effectView.Frame = Bounds;

		var radius = _cornerRadius < 0 ? Math.Min(Bounds.Width, Bounds.Height) / 2 : _cornerRadius;
		if (!OperatingSystem.IsIOSVersionAtLeast(26))
			_effectView.Layer.CornerRadius = (nfloat)radius;
		Layer.CornerRadius = (nfloat)radius;
		ClipsToBounds = true;
	}
}
