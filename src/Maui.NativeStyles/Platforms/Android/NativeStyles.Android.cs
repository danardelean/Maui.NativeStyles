using Android.Graphics;
using Android.Views;
using AButton = Android.Widget.Button;
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
	// Marks buttons whose colors were overridden by MapDestructiveText, so they can be restored.
	static readonly BindableProperty DestructiveAppliedProperty =
		BindableProperty.CreateAttached("DestructiveApplied", typeof(bool), typeof(NativeStylesExtensions), false);

	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers)
	{
		handlers.AddHandler<GlassView, GlassViewHandler>();
	}

	static partial void RegisterPlatformMappers(NativeStylesOptions options)
	{
		if (options.AndroidDynamicColors && OperatingSystem.IsAndroidVersionAtLeast(31)
			&& Android.App.Application.Context is Android.App.Application application)
		{
			// Material You: wallpaper-derived palette on every activity, and for {native:SystemColor}.
			DynamicColors.ApplyToActivitiesIfAvailable(application);
			SystemColors.DynamicColorsEnabled = true;
		}

		// Stepper: MAUI draws its own two-button LinearLayout; style them as M3 outlined icon buttons.
		StepperHandler.Mapper.AppendToMapping(MappingKey, MapStepperButtons);

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
					editText.Background = null;
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

	/// <summary>Corner radius of a contained (Material 3 Expressive) text container, in dp.</summary>
	const float ContainedCornerRadius = 28;

	static void StyleEditorContainer(IElementHandler handler, IEditor editor)
	{
		if (handler.PlatformView is not Android.Widget.EditText field || editor is not BindableObject bindable || field.Context is not { } context)
			return;
		if (NativeEntry.GetIsPlain(bindable))
		{
			field.Background = null;
			field.SetPadding(0, field.PaddingTop, 0, field.PaddingBottom);
			return;
		}

		GradientDrawable Shape(float radiusDp, int fill, float strokeDp, int stroke)
		{
			var shape = new GradientDrawable();
			shape.SetShape(ShapeType.Rectangle);
			shape.SetCornerRadius(context.ToPixels(radiusDp));
			shape.SetColor(fill);
			if (strokeDp > 0)
				shape.SetStroke((int)context.ToPixels(strokeDp), new AColor(stroke));
			return shape;
		}

		var onSurface = MaterialColors.GetColor(field, Resource.Attribute.colorOnSurface);
		if (NativeEntry.GetIsContained(bindable))
		{
			field.Background = Shape(ContainedCornerRadius, MaterialColors.GetColor(field, Resource.Attribute.colorSurfaceContainerHighest), 0, 0);
		}
		else
		{
			// Outlined text field tokens: 4 dp corners, 1 dp outline, 2 dp primary when focused, onSurface 12% when disabled.
			var states = new StateListDrawable();
			states.AddState([-Android.Resource.Attribute.StateEnabled], Shape(4, AColor.Transparent, 1, MaterialColors.CompositeARGBWithAlpha(onSurface, 31)));
			states.AddState([Android.Resource.Attribute.StateFocused], Shape(4, AColor.Transparent, 2, MaterialColors.GetColor(field, Resource.Attribute.colorPrimary)));
			states.AddState([], Shape(4, AColor.Transparent, 1, MaterialColors.GetColor(field, Resource.Attribute.colorOutline)));
			field.Background = states;
		}
		var padding = (int)context.ToPixels(16);
		field.SetPadding(padding, padding, padding, padding);
	}

	/// <summary>No underline, secondary text color, trailing tinted icon (Material list value / exposed dropdown affordance).</summary>
	static void StylePickerField(IElementHandler handler, int iconResource)
	{
		if (handler.PlatformView is not Android.Widget.TextView field || handler.MauiContext?.Context is not { } context)
			return;
		field.Background = null;
		var tint = SystemColors.Get(SystemColorRole.TextSecondary).ToPlatform();
		var icon = AndroidX.Core.Content.ContextCompat.GetDrawable(context, iconResource)?.Mutate();
		if (icon is not null)
		{
			icon.SetTint(tint);
			var size = (int)context.ToPixels(20);
			icon.SetBounds(0, 0, size, size);
			field.SetCompoundDrawablesRelative(null, null, icon, null);
			field.CompoundDrawablePadding = (int)context.ToPixels(4);
		}
		field.SetPadding(0, field.PaddingTop, 0, field.PaddingBottom);
	}

	static void MapLabelWeight(ILabelHandler handler, ILabel label)
	{
		if (label is not BindableObject bindable)
			return;
		var current = handler.PlatformView.Typeface;
		switch (NativeText.GetWeight(bindable))
		{
			case TextWeight.Medium:
			case TextWeight.Semibold:
				handler.PlatformView.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
				break;
			case TextWeight.Bold:
				handler.PlatformView.SetTypeface(current, TypefaceStyle.Bold);
				break;
		}
	}

	static void MapDestructiveText(IButtonHandler handler, IButton button)
	{
		if (button is not Button b)
			return;

		var kind = NativeButton.GetKind(b);
		var applies = NativeButton.GetIsDestructive(b) && kind is ButtonKind.Text or ButtonKind.Outlined;
		var applied = (bool)b.GetValue(DestructiveAppliedProperty);

		if (applies)
		{
			// Theme-aware local values win over the "Destructive" style setters (error-filled container).
			var (error, errorDark) = ResolveThemeColors("Error", "#B3261E", "#F2B8B5");
			b.SetAppThemeColor(Button.TextColorProperty, error, errorDark);
			b.SetValue(Button.BackgroundColorProperty, Colors.Transparent);
			if (kind == ButtonKind.Outlined)
				b.SetAppThemeColor(Button.BorderColorProperty, error, errorDark);
			b.SetValue(DestructiveAppliedProperty, true);
		}
		else if (applied)
		{
			b.RemoveBinding(Button.TextColorProperty);
			b.ClearValue(Button.TextColorProperty);
			b.ClearValue(Button.BackgroundColorProperty);
			b.RemoveBinding(Button.BorderColorProperty);
			b.ClearValue(Button.BorderColorProperty);
			b.SetValue(DestructiveAppliedProperty, false);
		}
	}

	static void MapStepperButtons(IStepperHandler handler, IStepper stepper)
	{
		if (handler.PlatformView is not ViewGroup group || handler.MauiContext?.Context is not { } context)
			return;
		var (outline, _) = SystemColors.Resolve(SystemColorRole.Separator);
		var (primary, primaryDark) = SystemColors.Resolve(SystemColorRole.Accent);
		var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
		var size = (int)context.ToPixels(40);
		for (var i = 0; i < group.ChildCount; i++)
		{
			if (group.GetChildAt(i) is not AButton button)
				continue;
			var drawable = new GradientDrawable();
			drawable.SetColor(AColor.Transparent);
			drawable.SetStroke((int)context.ToPixels(1), (dark ? SystemColors.Resolve(SystemColorRole.Separator).Dark : outline).ToPlatform());
			drawable.SetCornerRadius(context.ToPixels(20));
			button.Background = drawable;
			button.SetTextColor((dark ? primaryDark : primary).ToPlatform());
			button.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
			button.SetMinimumWidth(size); button.SetMinWidth(size);
			button.SetMinimumHeight(size); button.SetMinHeight(size);
			button.SetPadding(0, 0, 0, 0);
			if (button.LayoutParameters is ViewGroup.MarginLayoutParams lp)
			{
				lp.Width = size; lp.Height = size;
				lp.SetMargins(i == 0 ? 0 : (int)context.ToPixels(8), 0, 0, 0);
				button.LayoutParameters = lp;
			}
		}
	}

	static (Color light, Color dark) ResolveThemeColors(string key, string lightFallback, string darkFallback)
	{
		var resources = Application.Current?.Resources;
		var light = resources?.TryGetValue(key, out var l) == true && l is Color lc ? lc : Color.FromArgb(lightFallback);
		var dark = resources?.TryGetValue(key + "Dark", out var d) == true && d is Color dc ? dc : Color.FromArgb(darkFallback);
		return (light, dark);
	}
}

/// <summary>Android counterpart of the iOS glass container: an M3 elevated surface with rounded corners.</summary>
public class GlassViewHandler : ContentViewHandler
{
	public static readonly IPropertyMapper<GlassView, GlassViewHandler> GlassMapper =
		new PropertyMapper<GlassView, GlassViewHandler>(ContentViewHandler.Mapper)
		{
			[nameof(GlassView.CornerRadius)] = MapSurface,
			[nameof(GlassView.TintColor)] = MapSurface,
			[nameof(GlassView.GlassStyle)] = MapSurface,
		};

	public GlassViewHandler() : base(GlassMapper)
	{
	}

	protected override void ConnectHandler(ContentViewGroup platformView)
	{
		base.ConnectHandler(platformView);
		if (Application.Current is { } app)
			app.RequestedThemeChanged += OnThemeChanged;
	}

	protected override void DisconnectHandler(ContentViewGroup platformView)
	{
		if (Application.Current is { } app)
			app.RequestedThemeChanged -= OnThemeChanged;
		base.DisconnectHandler(platformView);
	}

	void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
		if (VirtualView is GlassView view)
			MapSurface(this, view);
	}

	static void MapSurface(GlassViewHandler handler, GlassView view)
	{
		var context = handler.Context;
		// surfaceContainerLow from the running theme (dark mode / dynamic colors aware), baseline fallback.
		var color = (view.TintColor ?? SystemColors.Get(SystemColorRole.CardBackground)).ToPlatform();

		var drawable = new GradientDrawable();
		drawable.SetColor(color);
		var radiusPx = view.CornerRadius < 0 ? context.ToPixels(28) : context.ToPixels(view.CornerRadius);
		drawable.SetCornerRadius(radiusPx);

		var platformView = handler.PlatformView;
		platformView.Background = drawable;
		platformView.ClipToOutline = true;
		platformView.Elevation = context.ToPixels(3); // M3 elevation level 2
	}
}
