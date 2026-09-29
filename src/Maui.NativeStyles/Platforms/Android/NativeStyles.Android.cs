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

	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers)
	{
		handlers.AddHandler<GlassView, GlassViewHandler>();
		handlers.AddHandler<SegmentedControl, SegmentedControlHandler>();
		// Handler registrations run when the app is built, after RegisterPlatformMappers stored the option
		if (s_replaceShellRenderer)
			handlers.AddHandler<Shell, NativeShellRenderer>();
	}

	static bool s_replaceShellRenderer = true;

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
		HookMaterial3Mapper<IEntry>("EntryHandler2", (handler, entry) =>
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
		HookMaterial3Mapper<IEditor>("EditorHandler2", StyleEditorContainer, nameof(IView.Background));

		// SearchBar: Material 3 search bar (56 dp, fully rounded, surfaceContainerHigh) instead of an underlined field.
		HookMaterial3Mapper<ISearchBar>("SearchBarHandler2", StyleSearchBar, nameof(IView.Background));

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
		// shape for Shell.SearchHandler, applied by NativeShellRenderer's trackers. TabbedPage, NavigationPage and
		// FlyoutPage (and a Shell with another renderer) have no such extension point: their chrome is (re)styled from a
		// layout listener on each activity's decor view.
		s_replaceShellRenderer = options.ReplaceShellRenderer;
		(Android.App.Application.Context as Android.App.Application)?.RegisterActivityLifecycleCallbacks(new ShellChromeStyler(options.AndroidRecreateOnThemeChange));
		foreach (var key in new[] { nameof(Toolbar.ToolbarItems), nameof(Toolbar.IconColor), nameof(Toolbar.BarTextColor) })
			ToolbarHandler.Mapper.AppendToMapping<IToolbar, IToolbarHandler>(key, MapToolbarTrailingIcons);

		// Pickers are bare TextInputEditTexts under Material 3 (an M2-looking underline). Material shows a selectable
		// value as plain text with a trailing affordance: menu arrow (Picker), calendar (DatePicker), clock (TimePicker).
		HookMaterial3Mapper<IPicker>("PickerHandler2", (handler, _) => StylePickerField(handler, Resource.Drawable.mtrl_ic_arrow_drop_down), nameof(IView.Background));
		HookMaterial3Mapper<IDatePicker>("DatePickerHandler2", (handler, _) => StylePickerField(handler, Resource.Drawable.material_ic_calendar_black_24dp), nameof(IView.Background));
		HookMaterial3Mapper<ITimePicker>("TimePickerHandler2", (handler, _) => StylePickerField(handler, Resource.Drawable.ic_clock_black_24dp), nameof(IView.Background));
		// Same look on the classic (non-Material 3) handlers.
		PickerHandler.Mapper.AppendToMapping(MappingKey, (handler, _) => StylePickerField(handler, Resource.Drawable.mtrl_ic_arrow_drop_down));
		DatePickerHandler.Mapper.AppendToMapping(MappingKey, (handler, _) => StylePickerField(handler, Resource.Drawable.material_ic_calendar_black_24dp));
		TimePickerHandler.Mapper.AppendToMapping(MappingKey, (handler, _) => StylePickerField(handler, Resource.Drawable.ic_clock_black_24dp));
	}

	/// <summary>Adds a native-style mapping to an internal Material 3 handler's public static Mapper (no-op if the type is missing).</summary>
	static void HookMaterial3Mapper<TView>(string handlerTypeName, Action<IElementHandler, TView> action, params string[] extraKeys)
		where TView : IElement
	{
		var mapper = typeof(EntryHandler).Assembly
			.GetType("Microsoft.Maui.Handlers." + handlerTypeName)?
			.GetField("Mapper", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?
			.GetValue(null) as IPropertyMapper<TView, IElementHandler>; // covariant cast
		if (mapper is null)
		{
			System.Diagnostics.Debug.WriteLine($"[NativeStyles] {handlerTypeName}.Mapper not found: its native styling is skipped.");
			return;
		}
		mapper.Add(MappingKey, action);
		foreach (var key in extraKeys)
			mapper.Add(key + ".NativeStyle", action); // extra keys run at connect; MAUI's own mapping for `key` stays in place
	}

	internal static DynamicColorsOptions? s_dynamicColors;
}
