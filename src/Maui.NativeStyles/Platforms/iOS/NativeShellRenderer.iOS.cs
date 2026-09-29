using System.ComponentModel;
using Foundation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using UIKit;

namespace NativeStyles;

/// <summary>
/// Shell renderer that applies the iOS 26 Shell adjustments from MAUI's Shell extension points, as the controllers
/// are created, appear or lay out: tab bar minimize behavior, large titles, the segmented control over the top tabs
/// and the search field colors. Registered for <see cref="Shell"/> by <c>UseNativeStyles</c> unless
/// <see cref="NativeStylesOptions.ReplaceShellRenderer"/> is false. An app with its own Shell renderer derives it from
/// this class (and calls the base implementation of the Create* methods it overrides) to keep the native styling.
/// Before iOS 26 it renders like MAUI's ShellRenderer.
/// </summary>
public class NativeShellRenderer : ShellRenderer
{
	static bool IsStyled => OperatingSystem.IsIOSVersionAtLeast(26);

	protected override IShellItemRenderer CreateShellItemRenderer(ShellItem item) =>
		IsStyled ? new NativeShellItemRenderer(this) { ShellItem = item } : base.CreateShellItemRenderer(item);

	protected override IShellSectionRenderer CreateShellSectionRenderer(ShellSection shellSection) =>
		IsStyled ? new NativeShellSectionRenderer(this) : base.CreateShellSectionRenderer(shellSection);

	protected override IShellPageRendererTracker CreatePageRendererTracker() =>
		IsStyled ? new NativeShellPageRendererTracker(this) : base.CreatePageRendererTracker();
}

/// <summary>Tab bar controller of a ShellItem.</summary>
sealed class NativeShellItemRenderer(IShellContext context) : ShellItemRenderer(context)
{
	readonly IShellContext _context = context;

	public override void ViewDidLoad()
	{
		base.ViewDidLoad();
		// Assigned once, before the tab bar is laid out (see SetTabBarMinimizeBehavior); a later change comes through the
		// ShellRenderer mapper
		NativeStylesExtensions.SetTabBarMinimizeBehavior(this, NativeShell.GetTabBarMinimizeBehavior(_context.Shell), onlyIfChanged: true);
	}

	public override void ViewWillLayoutSubviews()
	{
		// MAUI applies the displayed page's large title mode again on every layout of the tab controller
		base.ViewWillLayoutSubviews();
		if (SelectedViewController is ShellSectionRenderer { ShellSection: { } section } navigation && NativeStylesExtensions.HasTopTabs(section))
			NativeStylesExtensions.ApplySectionLargeTitle(navigation, section);
	}
}

/// <summary>Navigation controller of a ShellSection.</summary>
sealed class NativeShellSectionRenderer(IShellContext context) : ShellSectionRenderer(context)
{
	protected override IShellSectionRootRenderer CreateShellSectionRootRenderer(ShellSection shellSection, IShellContext shellContext) =>
		new NativeShellSectionRootRenderer(shellSection, shellContext);

	public override void ViewWillLayoutSubviews()
	{
		// Pages pushed on a section with top tabs keep its inline title
		base.ViewWillLayoutSubviews();
		if (ShellSection is { } section && NativeStylesExtensions.HasTopTabs(section))
			NativeStylesExtensions.ApplySectionLargeTitle(this, section);
	}

	public override void ViewDidAppear(bool animated)
	{
		base.ViewDidAppear(animated);
		// The first time a flyout item is displayed, MAUI enables the large title only after the content was laid out
		if (ShellSection is { } section)
		{
			NativeStylesExtensions.ApplySectionLargeTitle(this, section);
			if (((IShellSectionController)section).PresentedPage is { } page)
				NativeStylesExtensions.ExpandCollapsedLargeTitle(this, page);
		}
	}
}

/// <summary>Root controller of a ShellSection: hosts the ShellContent pages and, with several of them, the top tabs.</summary>
sealed class NativeShellSectionRootRenderer(ShellSection shellSection, IShellContext shellContext)
	: ShellSectionRootRenderer(shellSection, shellContext)
{
	readonly ShellSection _section = shellSection;

	protected override IShellSectionRootHeader CreateShellSectionRootHeader(IShellContext shellContext) =>
		new NativeShellSectionRootHeader(shellContext);

	public override void ViewDidLoad()
	{
		base.ViewDidLoad();
		// Top tabs: the content pages exist from here on and the bar is not laid out yet, so it starts with an inline title
		if (NavigationController is { } navigation && NativeStylesExtensions.HasTopTabs(_section))
			NativeStylesExtensions.ApplySectionLargeTitle(navigation, _section);
	}

	protected override void OnShellSectionPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		base.OnShellSectionPropertyChanged(sender, e);
		// Another top tab: MAUI has just applied the new page's large title mode, when its displayed page changed
		if (e.PropertyName == ShellSection.CurrentItemProperty.PropertyName && NavigationController is { } navigation)
			NativeStylesExtensions.ApplySectionLargeTitle(navigation, _section);
	}
}

/// <summary>MAUI's top-tabs strip, covered by a UISegmentedControl (see NativeStylesExtensions.StyleTopTabsStrip).</summary>
sealed class NativeShellSectionRootHeader(IShellContext shellContext) : ShellSectionRootHeader(shellContext)
{
	Page? _page;

	public override void ViewDidLoad()
	{
		base.ViewDidLoad();
		TrackPage();
		Style();
	}

	public override void ViewDidLayoutSubviews()
	{
		// MAUI lays out its selection bar and hairline here
		base.ViewDidLayoutSubviews();
		Style();
	}

	protected override void OnShellSectionPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		base.OnShellSectionPropertyChanged(sender, e);
		if (e.PropertyName == ShellSection.CurrentItemProperty.PropertyName)
		{
			TrackPage();
			Style();
		}
	}

	public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
	{
		var cell = base.GetCell(collectionView, indexPath);
		// Under the segmented control: VoiceOver reads the control instead
		NativeStylesExtensions.HideFromAccessibility([cell]);
		return cell;
	}

	void Style()
	{
		if (ShellSection is { } section && CollectionView is { } strip)
			NativeStylesExtensions.StyleTopTabsStrip(strip, section, _page?.BackgroundColor?.ToPlatform());
	}

	// The control sits on the page background, which changes with the app theme
	void TrackPage()
	{
		var page = (ShellSection?.CurrentItem as IShellContentController)?.Page;
		if (ReferenceEquals(page, _page))
			return;
		if (_page is not null)
			_page.PropertyChanged -= OnPagePropertyChanged;
		_page = page;
		if (_page is not null)
			_page.PropertyChanged += OnPagePropertyChanged;
	}

	void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == VisualElement.BackgroundColorProperty.PropertyName)
			Style();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _page is not null)
		{
			_page.PropertyChanged -= OnPagePropertyChanged;
			_page = null;
		}
		base.Dispose(disposing);
	}
}

/// <summary>Per-page tracker: search field colors and the content scroll view the tab bar minimizes with.</summary>
sealed class NativeShellPageRendererTracker(IShellContext context) : ShellPageRendererTracker(context)
{
	protected override void OnPageSet(Page oldPage, Page newPage)
	{
		base.OnPageSet(oldPage, newPage);
		if (oldPage is not null)
		{
			oldPage.Appearing -= OnPageAppearing;
			oldPage.Loaded -= OnPageLoaded;
		}
		if (newPage is not null)
		{
			// Subscribed after MAUI's own handlers, so they run after them
			newPage.Appearing += OnPageAppearing;
			if (!newPage.IsLoaded)
				newPage.Loaded += OnPageLoaded;
			// A pushed page, or another top tab in the section's root controller
			if (ViewController is { } controller && (newPage.Handler as IPlatformViewHandler)?.PlatformView is { } content)
				NativeStylesExtensions.RegisterContentScrollView(controller, content);
		}
		ApplySearchFieldColors();
	}

	protected override void OnPagePropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		base.OnPagePropertyChanged(sender, e);
		if (e.PropertyName == Shell.SearchHandlerProperty.PropertyName)
			ApplySearchFieldColors();
	}

	// MAUI attaches the search controller when the page appears and applies its colors again once the page is loaded
	void OnPageAppearing(object? sender, EventArgs e) => ApplySearchFieldColors();

	void OnPageLoaded(object? sender, EventArgs e)
	{
		if (sender is Page page)
			page.Loaded -= OnPageLoaded;
		ApplySearchFieldColors();
	}

	void ApplySearchFieldColors()
	{
		if (Page is { } page && Shell.GetSearchHandler(page) is { } search)
			NativeStylesExtensions.ApplySearchFieldColors(ViewController?.NavigationItem, search);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && Page is { } page)
		{
			page.Appearing -= OnPageAppearing;
			page.Loaded -= OnPageLoaded;
		}
		base.Dispose(disposing);
	}
}
