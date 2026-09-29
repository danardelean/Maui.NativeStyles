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

	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers, NativeStylesOptions options)
	{
		handlers.AddHandler<GlassView, GlassViewHandler>();
		handlers.AddHandler<SegmentedControl, SegmentedControlHandler>();
	}

	static partial void RegisterPlatformMappers(NativeStylesOptions options)
	{
		// Per-activity color setup happens in ShellChromeStyler.OnActivityCreated: MauiAppCompatActivity.OnCreate calls
		// SetTheme() (splash -> main theme) before base.OnCreate, which would wipe a theme overlay applied earlier, as
		// DynamicColors.ApplyToActivitiesIfAvailable does from onActivityPreCreated.
		DynamicColorsOptions? dynamicColors = null;
		if (options.Brand?.ResolveAccent() is { } accent)
		{
			// Brand seed: the Material 3 scheme generated from it replaces the baseline color resources of every activity
			// (Android 11+), so native widgets are branded too; {native:SystemColor} resolves from the same scheme on
			// every Android version. A brand wins over the wallpaper-based option.
			SystemColors.BrandSeed = accent.Light.ToPlatform().ToArgb();
		}
		else if (options.AndroidDynamicColors && OperatingSystem.IsAndroidVersionAtLeast(31))
		{
			// Material You: wallpaper-derived palette on every activity, and for {native:SystemColor}.
			SystemColors.DynamicColorsEnabled = true;
			dynamicColors = new DynamicColorsOptions.Builder().Build();
		}
		s_dynamicColors = dynamicColors;

		// Stepper: MAUI draws its own two-button LinearLayout; style them as M3 outlined icon buttons.
		StepperHandler.Mapper.AppendToMapping(MappingKey, MapStepperButtons);

		// NativeButton.TintsImage / NativeImage.TintColor: MAUI keeps button icons in their original colors (transparent
		// tint in Add mode); a Material button icon follows the label color.
		ButtonHandler.Mapper.AppendToMapping(MappingKey, MapButtonIconTint);
		ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.TextColor), MapButtonIconTint);

		// Destructive combined with Text/Outlined: error-colored label instead of an error-filled container.
		// Everything else is expressed in MaterialStyles.xaml and by the Material 3 theme (UseMaterial3).
		ButtonHandler.Mapper.AppendToMapping(MappingKey, MapDestructiveText);

		// Label weight: Roboto Medium for Medium/Semibold, bold for Bold.
		LabelHandler.Mapper.AppendToMapping(MappingKey, MapLabelWeight);
		LabelHandler.Mapper.AppendToMapping(nameof(ILabel.Font), MapLabelWeight);

		// Entry IsPlain: no Material box, for fields embedded in list rows.
		EntryHandler.Mapper.AppendToMapping(MappingKey, (handler, entry) =>
		{
			if (entry is BindableObject b && NativeEntry.GetIsPlain(b))
				handler.PlatformView.Background = null;
		});

		// With UseMaterial3 the input controls are served by internal *Handler2 classes (TextInputLayout / TextInputEditText);
		// their Mapper is a public static field on an internal type, reached through reflection (public in MAUI 11).
		var material3Hooks = new Material3Hooks();
		material3Hooks.Hook<IEntry>("EntryHandler2", (handler, entry) =>
		{
			if (entry is not BindableObject b || handler.PlatformView is not TextInputLayout layout)
				return;
			if (NativeEntry.GetIsPlain(b))
			{
				layout.BoxBackgroundMode = TextInputLayout.BoxBackgroundNone;
				layout.BoxStrokeWidth = 0;
				layout.BoxStrokeWidthFocused = 0;
				if (layout.EditText is { } editText)
				{
					// Value part of a list row: the row owns the 56 dp height and the padding
					editText.Background = null;
					editText.SetMinimumHeight(0);
					editText.SetMinHeight(0);
					editText.SetPadding(0, (int)layout.Context.ToPixels(6), 0, (int)layout.Context.ToPixels(6));
				}
			}
			else if (NativeEntry.GetIsContained(b))
			{
				// Material filled text field without the active indicator, fully rounded (56 dp tall -> 28 dp corners).
				var radius = layout.Context.ToPixels(ContainedCornerRadius);
				layout.BoxBackgroundMode = TextInputLayout.BoxBackgroundFilled;
				layout.SetBoxCornerRadii(radius, radius, radius, radius);
				layout.BoxStrokeWidth = 0;
				layout.BoxStrokeWidthFocused = 0;
				layout.BoxBackgroundColor = MaterialColors.GetColor(layout, Resource.Attribute.colorSurfaceContainerHighest);
			}
		});

		// Editor is a bare TextInputEditText under Material 3 (an M2-looking underline): give it the Material 3
		// outlined container, or the filled rounded one with NativeEntry.IsContained.
		material3Hooks.Hook<IEditor>("EditorHandler2", StyleEditorContainer, nameof(IView.Background));

		// SearchBar: Material 3 search bar (56 dp, fully rounded, surfaceContainerHigh) instead of an underlined field.
		material3Hooks.Hook<ISearchBar>("SearchBarHandler2", StyleSearchBar, nameof(IView.Background));

		// NativeImage.TintColor: single-color template rendering.
		ImageHandler.Mapper.AppendToMapping(MappingKey, (handler, image) =>
		{
			if (image is not BindableObject bindable)
				return;
			if (NativeImage.GetTintColor(bindable) is { } tint)
				handler.PlatformView.SetColorFilter(tint.ToPlatform(), PorterDuff.Mode.SrcIn!);
			else
				handler.PlatformView.ClearColorFilter();
		});
		ImageButtonHandler.Mapper.AppendToMapping(MappingKey, (handler, button) =>
		{
			if (button is not BindableObject bindable)
				return;
			if (NativeImage.GetTintColor(bindable) is { } tint)
				handler.PlatformView.SetColorFilter(tint.ToPlatform(), PorterDuff.Mode.SrcIn!);
			else
				handler.PlatformView.ClearColorFilter();
		});

		// Expressive segmented lists: NativeList.ItemCornerRadius draws the item container as a rounded shape.
		ViewHandler.ViewMapper.AppendToMapping(MappingKey, MapListItemShape);
		ViewHandler.ViewMapper.AppendToMapping(nameof(IView.Background), MapListItemShape);

		// Shell: flexible navigation bar metrics/colors, app bar behind the status bar and the Material 3 search bar
		// shape for Shell.SearchHandler. Shell creates these views after navigation and its Android renderer does not
		// run mapper keys on connect, so they are (re)styled from a layout listener on each activity's decor view.
		if (Android.App.Application.Context is Android.App.Application application)
			application.RegisterActivityLifecycleCallbacks(new ShellChromeStyler(options.AndroidRecreateOnThemeChange));
		else
			NativeStylesLog.Warning("ActivityCallbacks", "No Android application to register the activity callbacks with: the brand / dynamic colors and the Shell chrome styling are not applied.");
		if (options.AndroidRecreateOnThemeChange && !ShellChromeStyler.CanClearShellObservers)
			NativeStylesLog.Warning("Shell._appearanceObservers", $"Shell._appearanceObservers not found in Microsoft.Maui.Controls {MauiVersion(typeof(Shell))}: a Shell replaced as root page may throw inside MAUI on the next theme change.");

		// Pickers are bare TextInputEditTexts under Material 3 (an M2-looking underline). Material shows a selectable
		// value as plain text with a trailing affordance: menu arrow (Picker), calendar (DatePicker), clock (TimePicker).
		material3Hooks.Hook<IPicker>("PickerHandler2", (handler, _) => StylePickerField(handler, Resource.Drawable.mtrl_ic_arrow_drop_down), nameof(IView.Background));
		material3Hooks.Hook<IDatePicker>("DatePickerHandler2", (handler, _) => StylePickerField(handler, Resource.Drawable.material_ic_calendar_black_24dp), nameof(IView.Background));
		material3Hooks.Hook<ITimePicker>("TimePickerHandler2", (handler, _) => StylePickerField(handler, Resource.Drawable.ic_clock_black_24dp), nameof(IView.Background));
		material3Hooks.Report();
		// Same look on the classic (non-Material 3) handlers.
		PickerHandler.Mapper.AppendToMapping(MappingKey, (handler, _) => StylePickerField(handler, Resource.Drawable.mtrl_ic_arrow_drop_down));
		DatePickerHandler.Mapper.AppendToMapping(MappingKey, (handler, _) => StylePickerField(handler, Resource.Drawable.material_ic_calendar_black_24dp));
		TimePickerHandler.Mapper.AppendToMapping(MappingKey, (handler, _) => StylePickerField(handler, Resource.Drawable.ic_clock_black_24dp));
	}

	/// <summary>Adds native-style mappings to the internal Material 3 handlers' public static Mappers, noting which exist.</summary>
	sealed class Material3Hooks
	{
		readonly List<string> _installed = [];

		/// <summary>Adds the mapping to the handler's Mapper; a handler this MAUI version does not have is reported and skipped.</summary>
		public void Hook<TView>(string handlerTypeName, Action<IElementHandler, TView> action, params string[] extraKeys)
			where TView : IElement
		{
			var mapper = typeof(EntryHandler).Assembly
				.GetType("Microsoft.Maui.Handlers." + handlerTypeName)?
				.GetField("Mapper", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?
				.GetValue(null) as IPropertyMapper<TView, IElementHandler>; // covariant cast
			if (mapper is null)
			{
				NativeStylesLog.Warning(handlerTypeName, $"Microsoft.Maui.Handlers.{handlerTypeName}.Mapper not found in Microsoft.Maui {MauiVersion(typeof(EntryHandler))}: the Material 3 styling of that control is skipped.");
				return;
			}
			mapper.Add(MappingKey, action);
			foreach (var key in extraKeys)
				mapper.Add(key + ".NativeStyle", action); // extra keys run at connect; MAUI's own mapping for `key` stays in place
			_installed.Add(handlerTypeName);
		}

		public void Report() =>
			NativeStylesLog.Debug("Material3Hooks", $"Material 3 handler hooks installed: {(_installed.Count > 0 ? string.Join(", ", _installed) : "none")}.");
	}

	/// <summary>Version of the MAUI assembly that defines <paramref name="type"/> (e.g. 10.0.101), for diagnostics.</summary>
	static string MauiVersion(Type type) =>
		System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(type.Assembly)?.InformationalVersion.Split('+')[0]
			?? type.Assembly.GetName().Version?.ToString()
			?? "?";

	internal static DynamicColorsOptions? s_dynamicColors;
}
