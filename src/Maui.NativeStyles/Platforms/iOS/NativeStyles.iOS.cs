using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;
using PlatformContentView = Microsoft.Maui.Platform.ContentView;

namespace NativeStyles;
public static partial class NativeStylesExtensions
{
	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers, NativeStylesOptions options)
	{
		handlers.AddHandler<GlassView, GlassViewHandler>();
		handlers.AddHandler<SegmentedControl, SegmentedControlHandler>();

		// Entry: same EntryHandler and mapper, but a UITextField that supports text insets and continuous corners.
		// Without it MapEntryChrome styles whatever UITextField the Entry handler creates.
		if (!options.ReplaceEntryHandler)
		{
			NativeStylesLog.Debug("EntryHandler", "Entry handler left as registered (NativeStylesOptions.ReplaceEntryHandler is false): the native-style mapping shapes its UITextField.");
			return;
		}
		if (handlers.LastOrDefault(d => !d.IsKeyedService && d.ServiceType == typeof(Entry)) is { } registered
			&& registered.ImplementationType != typeof(EntryHandler) && registered.ImplementationType != typeof(NativeEntryHandler))
		{
			NativeStylesLog.Warning("EntryHandlerReplaced", $"UseNativeStyles replaces the Entry handler registered before it ({registered.ImplementationType?.FullName ?? "a factory"}) with NativeEntryHandler. Set NativeStylesOptions.ReplaceEntryHandler to false to keep it.");
		}
		handlers.AddHandler<Entry, NativeEntryHandler>();
		NativeStylesLog.Debug("EntryHandler", "Entry handler: NativeEntryHandler (NativeStylesOptions.ReplaceEntryHandler).");
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

			// UISwitch ignores the tint color (it is green by design); branding it is an explicit choice
			if (options.Brand.TintsSwitches)
			{
				foreach (var key in new[] { MappingKey, nameof(ISwitch.TrackColor), nameof(ISwitch.IsOn) })
					SwitchHandler.Mapper.AppendToMapping(key, (handler, view) =>
					{
						if (view.TrackColor is null)
							handler.PlatformView.OnTintColor = tint;
					});
			}
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
		EntryHandler.Mapper.AppendToMapping(nameof(IEntry.ClearButtonVisibility), MapEntryChrome);

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

	static UIColor? s_brandTint;
}
