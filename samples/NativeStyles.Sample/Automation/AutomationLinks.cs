using Microsoft.Maui.LifecycleEvents;

namespace NativeStyles.Sample.Automation;

/// <summary>
/// Deterministic navigation for scripts/visual-check.py and manual checks, through a custom URL scheme:
/// <c>nativestyles://page/{name}?theme=light|dark|system</c> and <c>nativestyles://theme/{light|dark|system}</c>.
/// A link can only switch between the sample's own pages and set <see cref="Application.UserAppTheme"/>, so it is
/// harmless when something else opens it; normal launches carry no link and are unaffected.
/// Android: VIEW intent (intent filter on MainActivity, not BROWSABLE so web pages cannot open it).
/// iOS: CFBundleURLTypes in Info.plist, or the <c>-NativeStylesLink &lt;link&gt;</c> launch argument: SpringBoard asks
/// for confirmation before opening a custom scheme (simctl openurl included), a launch argument does not.
/// </summary>
public static class AutomationLinks
{
	public const string Scheme = "nativestyles";

	/// <summary>iOS launch argument (NSUserDefaults argument domain) carrying a link.</summary>
	public const string LaunchArgument = "NativeStylesLink";

	/// <summary>Page names accepted by <c>nativestyles://page/{name}</c> and the Shell route each one opens.</summary>
	public static readonly IReadOnlyDictionary<string, string> Pages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["buttons"] = "//controls/buttons",
		["inputs"] = "//controls/inputs",
		["selection"] = "//controls/selection",
		["feedback"] = "//controls/feedback",
		["lists"] = "//controls/content/lists",
		["views"] = "//controls/content/views",
		["typography"] = "//controls/content/typography",
		// The Buttons page with the flyout open
		["flyout"] = "//controls/buttons",
	};

	public static MauiAppBuilder UseAutomationLinks(this MauiAppBuilder builder)
	{
		builder.ConfigureLifecycleEvents(events =>
		{
#if ANDROID
			events.AddAndroid(android => android
				// A saved state means a recreate (theme change) re-delivering the launch intent: already handled
				.OnCreate((activity, state) => { if (state is null) HandleIntent(activity.Intent); })
				.OnNewIntent((_, intent) => HandleIntent(intent)));
#elif IOS
			events.AddiOS(ios => ios
				.FinishedLaunching((_, _) =>
				{
					Handle(Foundation.NSUserDefaults.StandardUserDefaults.StringForKey(LaunchArgument));
					return true;
				})
				.OpenUrl((_, url, _) => Handle(url.AbsoluteString)));
#endif
		});
		return builder;
	}

#if ANDROID
	static void HandleIntent(Android.Content.Intent? intent)
	{
		if (intent?.Action == Android.Content.Intent.ActionView && intent.DataString is { } link)
			Handle(link);
	}
#endif

	/// <summary>Parses and applies a link. Returns false when it is not an automation link.</summary>
	public static bool Handle(string? link)
	{
		if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
			return false;

		var name = uri.AbsolutePath.Trim('/');
		string? theme = uri.Host.Equals("theme", StringComparison.OrdinalIgnoreCase) ? name : Query(uri, "theme");
		string? page = uri.Host.Equals("page", StringComparison.OrdinalIgnoreCase) ? name : null;
		if (page is not null && !Pages.ContainsKey(page))
		{
			Console.WriteLine($"[automation] unknown page '{page}'");
			return false;
		}

		Console.WriteLine($"[automation] {uri}");
		// Links can arrive before the first window exists (cold start): apply them on the UI thread once it does
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			try
			{
				await ApplyAsync(page, theme);
				Console.WriteLine($"[automation] done {uri}");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[automation] failed {uri}: {e}");
			}
		});
		return true;
	}

	static async Task ApplyAsync(string? page, string? theme)
	{
		var window = await WaitAsync(() => Application.Current?.Windows.FirstOrDefault(w => w.Page?.Handler is not null));
		if (window is null || Application.Current is not { } app)
			return;

		if (theme is not null && ParseTheme(theme) is { } requested && app.UserAppTheme != requested)
		{
			var before = app.RequestedTheme;
			app.UserAppTheme = requested;
#if ANDROID
			// The library recreates the activity when the effective theme changes and moves the root page over: navigate
			// once the new activity shows it, not while the old one is being torn down.
			if (app.RequestedTheme != before)
				window = await WaitForRecreateAsync(window);
#endif
		}

		if (page is null)
			return;

		// Classic navigation (TabbedPage) may have replaced the Shell: links always land in the Shell
		if (window.Page is not AppShell shell)
		{
			shell = new AppShell();
			window.Page = shell;
			await WaitAsync(() => shell.Handler);
		}

		shell.FlyoutIsPresented = false;
		await shell.GoToAsync(Pages[page], animate: false);
		if (page.Equals("flyout", StringComparison.OrdinalIgnoreCase))
			shell.FlyoutIsPresented = true;
	}

	static AppTheme? ParseTheme(string value) => value.ToLowerInvariant() switch
	{
		"light" => AppTheme.Light,
		"dark" => AppTheme.Dark,
		"system" or "unspecified" => AppTheme.Unspecified,
		_ => null,
	};

	static string? Query(Uri uri, string key)
	{
		foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
		{
			var parts = pair.Split('=', 2);
			if (parts.Length == 2 && parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
				return Uri.UnescapeDataString(parts[1]);
		}
		return null;
	}

	static async Task<T?> WaitAsync<T>(Func<T?> probe, int timeoutMs = 10_000) where T : class
	{
		for (var waited = 0; waited < timeoutMs; waited += 100)
		{
			if (probe() is { } value)
				return value;
			await Task.Delay(100);
		}
		return probe();
	}

#if ANDROID
	static async Task<Window> WaitForRecreateAsync(Window window)
	{
		var page = window.Page;
		var activity = window.Handler?.PlatformView;
		var recreated = await WaitAsync(() => Application.Current?.Windows.FirstOrDefault(w =>
			w.Page == page && w.Handler?.PlatformView is Android.App.Activity { IsFinishing: false } current && !ReferenceEquals(current, activity)), 5_000);
		// Let the new activity finish attaching the Shell (fragment transactions) before navigating
		await Task.Delay(300);
		return recreated ?? window;
	}
#endif
}
