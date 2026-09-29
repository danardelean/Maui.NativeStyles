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

/// <summary>Attaches a layout listener to every activity so Shell's native chrome can be styled once it exists.</summary>
sealed class ShellChromeStyler(bool recreateOnThemeChange) : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
{
	Application? _observedApplication;
	AppTheme _theme;

	public void OnActivityResumed(Android.App.Activity activity)
	{
		ObserveTheme();
		if (recreateOnThemeChange)
			TrackRootPages(activity);
		if (activity.Window?.DecorView is not { } decor || decor.GetTag(Resource.Id.action_bar_root) is not null)
			return;
		decor.SetTag(Resource.Id.action_bar_root, "NativeStyles");
		decor.ViewTreeObserver!.GlobalLayout += (_, _) => NativeStylesExtensions.StyleShellChrome(decor, 0);
	}

	public void OnActivityCreated(Android.App.Activity activity, Android.OS.Bundle? savedInstanceState)
	{
		if (SystemColors.ApplyBrandToActivity(activity))
			return;
		if (NativeStylesExtensions.s_dynamicColors is { } options)
			DynamicColors.ApplyToActivityIfAvailable(activity, options);
	}

	/// <summary>
	/// MAUI keeps the activity across uiMode changes, so native views keep the colors resolved at creation. When the
	/// effective app theme changes, recreate the activity (Android's default behavior): MAUI re-attaches the existing
	/// window and page tree to the new activity.
	/// </summary>
	void ObserveTheme()
	{
		if (!recreateOnThemeChange || Application.Current is not { } application || ReferenceEquals(application, _observedApplication))
			return;
		_observedApplication = application;
		_theme = application.RequestedTheme;
		// RequestedThemeChanged is a weak event: the handler must be a method of this (long-lived) object, not a lambda
		// with captured locals, or it is collected together with its closure.
		application.RequestedThemeChanged += OnRequestedThemeChanged;
	}

	void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
		if (e.RequestedTheme == _theme || _observedApplication is not { } application)
			return;
		_theme = e.RequestedTheme;
		if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not { IsFinishing: false } current)
			return;
		// MAUI asks the app for a window again after the recreate, and the default template answers with a brand-new
		// page tree. Remember the live root page so it can be moved to the new window (see OnActivityStarted).
		_carriedRootPage = application.Windows.FirstOrDefault(w => ReferenceEquals(w.Handler?.PlatformView, current))?.Page;
		DisconnectOrphanedRootPages(application);
		// Let MAUI finish handling the configuration change first
		current.Window?.DecorView.Post(current.Recreate);
	}

	public void OnActivityDestroyed(Android.App.Activity activity) => SystemColors.ResetThemedContexts();
	public void OnActivityPaused(Android.App.Activity activity) { }
	public void OnActivitySaveInstanceState(Android.App.Activity activity, Android.OS.Bundle outState) { }
	Page? _carriedRootPage;
	readonly List<WeakReference<Page>> _formerRootPages = [];
	readonly System.Runtime.CompilerServices.ConditionalWeakTable<Microsoft.Maui.Controls.Window, object> _trackedWindows = new();

	/// <summary>Remembers the pages an app replaces as window root (login shell -> main shell, Shell -> TabbedPage...).</summary>
	void TrackRootPages(Android.App.Activity activity)
	{
		var window = Application.Current?.Windows.FirstOrDefault(w => ReferenceEquals(w.Handler?.PlatformView, activity));
		if (window is null || _trackedWindows.TryGetValue(window, out _))
			return;
		_trackedWindows.Add(window, window);
		window.PropertyChanging += OnWindowPropertyChanging;
	}

	void OnWindowPropertyChanging(object? sender, Microsoft.Maui.Controls.PropertyChangingEventArgs e)
	{
		if (e.PropertyName == nameof(Microsoft.Maui.Controls.Window.Page) && sender is Microsoft.Maui.Controls.Window { Page: { } replaced })
			_formerRootPages.Add(new WeakReference<Page>(replaced));
	}

	/// <summary>
	/// A root page that was replaced keeps its handlers, and a Shell among them stays subscribed to theme changes; once
	/// its activity is destroyed its renderer throws on the next theme change. Disconnect them while they are still
	/// healthy, right before the activity goes away (MAUI builds new handlers if such a page is shown again).
	/// </summary>
	void DisconnectOrphanedRootPages(Application application)
	{
		foreach (var reference in _formerRootPages)
			if (reference.TryGetTarget(out var page) && !ReferenceEquals(page, _carriedRootPage) && !application.Windows.Any(w => ReferenceEquals(w.Page, page)))
				Disconnect(page);
		_formerRootPages.Clear();
	}

	static void Disconnect(Page page)
	{
		try
		{
			page.DisconnectHandlers();
		}
		catch (Exception e)
		{
			NativeStylesLog.Warning("Disconnect:" + page.GetType().FullName, $"Could not disconnect the handlers of the replaced root page {page.GetType().FullName}.", e);
		}

		// MAUI (10.0) disposes a replaced Shell's item renderer before its fragment is destroyed; the fragment then cannot
		// unregister itself and stays in the Shell's appearance observers with its fields cleared. The Shell is still
		// subscribed to theme changes, so the next one throws a NullReferenceException inside MAUI. A Shell that is not
		// shown needs no observers (its renderers register again when it is displayed), and there is no public API to
		// remove a dead one, so the private list is cleared.
		if (page is Shell shell)
			(s_shellAppearanceObservers?.GetValue(shell) as System.Collections.IList)?.Clear();
	}

	static readonly System.Reflection.FieldInfo? s_shellAppearanceObservers =
		typeof(Shell).GetField("_appearanceObservers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

	/// <summary>False when this MAUI version has no clearable Shell._appearanceObservers list (reported at startup).</summary>
	internal static bool CanClearShellObservers => typeof(System.Collections.IList).IsAssignableFrom(s_shellAppearanceObservers?.FieldType);

	/// <summary>
	/// After a theme-change recreate: if the app built a new root page for the new window, replace it with the page that
	/// was showing, so navigation state, the selected tab and view models survive. Apps that already reuse their root
	/// page get the same instance back and nothing happens.
	/// </summary>
	public void OnActivityStarted(Android.App.Activity activity)
	{
		if (_carriedRootPage is not { } page)
			return;
		_carriedRootPage = null;
		// Not now: the page MAUI just created (a Shell hosts fragments) still has transactions pending until onStart
		// completes, and removing its container underneath them throws. Swap once the activity has finished starting.
		activity.Window?.DecorView.Post(() =>
		{
			var window = Application.Current?.Windows.FirstOrDefault(w => ReferenceEquals(w.Handler?.PlatformView, activity));
			if (window is null || ReferenceEquals(window.Page, page))
				return;
			var discarded = window.Page;
			window.Page = page;
			// The page the app's CreateWindow built for this activity is not used: release its handlers now
			if (discarded is not null)
				Disconnect(discarded);
			_formerRootPages.Clear();
		});
	}
	public void OnActivityStopped(Android.App.Activity activity) { }
}
