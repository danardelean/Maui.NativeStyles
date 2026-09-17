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
	// Marks buttons whose colors were overridden by MapDestructiveText, so they can be restored.
	static readonly BindableProperty DestructiveAppliedProperty =
		BindableProperty.CreateAttached("DestructiveApplied", typeof(bool), typeof(NativeStylesExtensions), false);

	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers)
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
		// shape for Shell.SearchHandler. Shell creates these views after navigation and its Android renderer does not
		// run mapper keys on connect, so they are (re)styled from a layout listener on each activity's decor view.
		(Android.App.Application.Context as Android.App.Application)?.RegisterActivityLifecycleCallbacks(new ShellChromeStyler());

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

	static void StyleSearchBar(IElementHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is not TextInputLayout layout || layout.Context is not { } context)
			return;
		var radius = context.ToPixels(28);
		layout.BoxBackgroundMode = TextInputLayout.BoxBackgroundFilled;
		layout.SetBoxCornerRadii(radius, radius, radius, radius);
		layout.BoxStrokeWidth = 0;
		layout.BoxStrokeWidthFocused = 0;
		layout.BoxBackgroundColor = MaterialColors.GetColor(layout, Resource.Attribute.colorSurfaceContainerHigh);
		layout.SetMinimumHeight((int)context.ToPixels(56));
	}

	static void MapListItemShape(IViewHandler handler, IView view)
	{
		if (view is not BindableObject bindable || handler.PlatformView is not AView platformView || platformView.Context is not { } context)
			return;
		var radius = NativeList.GetItemCornerRadius(bindable);
		if (radius < 0)
			return;
		var shape = new GradientDrawable();
		shape.SetShape(ShapeType.Rectangle);
		shape.SetCornerRadius(context.ToPixels(radius));
		shape.SetColor(((view.Background as SolidPaint)?.Color ?? Colors.Transparent).ToPlatform());
		platformView.Background = shape;
		platformView.ClipToOutline = true;

		// A native radio button used as a list item: move the control to the 16 dp keyline, 12 dp before the label.
		if (OperatingSystem.IsAndroidVersionAtLeast(23)
			&& platformView is Android.Widget.CompoundButton { ButtonDrawable: { } button and not InsetDrawable } compound)
		{
			compound.SetButtonDrawable(new InsetDrawable(button, (int)context.ToPixels(10), 0, 0, 0));
			compound.SetPadding((int)context.ToPixels(10), compound.PaddingTop, (int)context.ToPixels(16), compound.PaddingBottom);
		}
	}

	/// <summary>Finds Shell's BottomNavigationView and toolbar search card; MAUI content is not traversed.</summary>
	internal static void StyleShellChrome(AView view, int depth)
	{
		switch (view)
		{
			case Google.Android.Material.BottomNavigation.BottomNavigationView navigation:
				StyleNavigationBar(navigation);
				return;
			case Google.Android.Material.Tabs.TabLayout tabs:
				StyleTabs(tabs);
				return;
			case AndroidX.DrawerLayout.Widget.DrawerLayout drawer:
				StyleDrawerSheet(drawer);
				break;
			case Google.Android.Material.AppBar.AppBarLayout appBar:
				StyleAppBar(appBar);
				break; // the toolbar inside may host the search card
			case AndroidX.CardView.Widget.CardView card when card.Parent is Microsoft.Maui.Controls.Platform.Compatibility.ShellSearchView:
				StyleShellSearch(card);
				return;
		}
		if (depth > 16 || view is not ViewGroup group || view is Microsoft.Maui.Platform.ContentViewGroup or Microsoft.Maui.Platform.LayoutViewGroup or AndroidX.RecyclerView.Widget.RecyclerView)
			return;
		for (var i = 0; i < group.ChildCount; i++)
			if (group.GetChildAt(i) is { } child)
				StyleShellChrome(child, depth + 1);
	}

	/// <summary>M3 Expressive flexible navigation bar: 64 dp, 56x32 dp indicator, secondary active label.</summary>
	static void StyleNavigationBar(Google.Android.Material.BottomNavigation.BottomNavigationView bar)
	{
		if (bar.Context is not { } context)
			return;
		// TabbedPage reserves the baseline 80 dp below its content (MAUI reads m3_bottom_nav_min_height); follow the bar.
		var barHeight = (int)context.ToPixels(64);
		if (bar.RootView?.FindViewById(Microsoft.Maui.Resource.Id.navigationlayout_content) is { LayoutParameters: ViewGroup.MarginLayoutParams content } contentView
			&& content.BottomMargin == context.Resources!.GetDimensionPixelSize(Resource.Dimension.m3_bottom_nav_min_height))
		{
			content.BottomMargin = barHeight;
			contentView.RequestLayout();
		}

		var indicatorWidth = (int)context.ToPixels(56);
		if (bar.ItemActiveIndicatorWidth == indicatorWidth && bar.ItemTextColor == s_navigationLabelColors)
			return;

		int Role(int attribute) => MaterialColors.GetColor(bar, attribute);
		Android.Content.Res.ColorStateList Checked(int checkedColor, int color) => new(
			[[Android.Resource.Attribute.StateChecked], []], [checkedColor, color]);

		var onSurfaceVariant = Role(Resource.Attribute.colorOnSurfaceVariant);
		s_navigationLabelColors = Checked(Role(Resource.Attribute.colorSecondary), onSurfaceVariant);
		bar.ItemTextColor = s_navigationLabelColors;
		bar.ItemIconTintList = Checked(Role(Resource.Attribute.colorOnSecondaryContainer), onSurfaceVariant);
		bar.ItemActiveIndicatorColor = Android.Content.Res.ColorStateList.ValueOf(new AColor(Role(Resource.Attribute.colorSecondaryContainer)));
		bar.ItemActiveIndicatorWidth = indicatorWidth;
		bar.ItemActiveIndicatorHeight = (int)context.ToPixels(32);
		bar.ItemPaddingTop = (int)context.ToPixels(6);
		bar.ItemPaddingBottom = (int)context.ToPixels(6);
		bar.ActiveIndicatorLabelPadding = (int)context.ToPixels(4);
		bar.SetMinimumHeight(barHeight);
		bar.SetBackgroundColor(new AColor(Role(Resource.Attribute.colorSurfaceContainer)));
	}

	static (NavigationPage? Navigation, Page? Leaf) CurrentNavigation(Page? page)
	{
		NavigationPage? navigation = null;
		for (var depth = 0; page is not null && depth < 8; depth++)
		{
			switch (page)
			{
				case NavigationPage n: navigation = n; page = n.CurrentPage; break;
				case TabbedPage t: page = t.CurrentPage; break;
				case FlyoutPage f: page = f.Detail; break;
				default: return (navigation, page);
			}
		}
		return (navigation, page);
	}

	internal static DynamicColorsOptions? s_dynamicColors;

	static Android.Content.Res.ColorStateList? s_navigationLabelColors;

	/// <summary>
	/// M3 primary tabs (Shell top tabs, TabbedPage top tabs): primary label and label-width indicator for the active
	/// tab, onSurfaceVariant otherwise, fixed tabs sharing the width (scrollable above four), transparent over the app bar.
	/// </summary>
	static void StyleTabs(Google.Android.Material.Tabs.TabLayout tabs)
	{
		if (tabs.Context is not { } context)
			return;
		var primary = MaterialColors.GetColor(tabs, Resource.Attribute.colorPrimary);
		var mode = tabs.TabCount > 4 ? Google.Android.Material.Tabs.TabLayout.ModeScrollable : Google.Android.Material.Tabs.TabLayout.ModeFixed;

		// Same color as the app bar above (StyleAppBar records it); a TabbedPage hosts the tabs in a primary-colored strip
		var container = MaterialColors.GetColor(tabs, Resource.Attribute.colorSurface);
		for (var parent = tabs.Parent; parent is not null; parent = parent.Parent)
			if (parent is Google.Android.Material.AppBar.AppBarLayout appBar && appBar.GetTag(Resource.Id.action_bar_container) is Java.Lang.Integer recorded)
			{
				container = recorded.IntValue();
				break;
			}

		if (tabs.TabTextColors?.GetColorForState([Android.Resource.Attribute.StateSelected], AColor.Transparent) == primary
			&& tabs.TabMode == mode
			&& (tabs.GetTag(Resource.Id.action_bar_container) as Java.Lang.Integer)?.IntValue() == container)
		{
			return;
		}

		var onSurfaceVariant = MaterialColors.GetColor(tabs, Resource.Attribute.colorOnSurfaceVariant);
		tabs.SetTabTextColors(onSurfaceVariant, primary);
		tabs.TabIconTint = new Android.Content.Res.ColorStateList(
			[[Android.Resource.Attribute.StateSelected], []], [primary, onSurfaceVariant]);
		tabs.SetSelectedTabIndicatorColor(primary);
		tabs.TabMode = mode;
		tabs.TabGravity = Google.Android.Material.Tabs.TabLayout.GravityFill;
		tabs.SetTag(Resource.Id.action_bar_container, Java.Lang.Integer.ValueOf(container));

		// Container color with the 1 dp outlineVariant divider inside it (bottom edge)
		var background = new ColorDrawable(new AColor(container));
		if (OperatingSystem.IsAndroidVersionAtLeast(23))
		{
			var layers = new LayerDrawable([background, new ColorDrawable(new AColor(MaterialColors.GetColor(tabs, Resource.Attribute.colorOutlineVariant)))]);
			layers.SetLayerGravity(1, GravityFlags.Bottom);
			layers.SetLayerHeight(1, Math.Max(1, (int)context.ToPixels(1)));
			tabs.Background = layers;
		}
		else
		{
			tabs.Background = background;
		}
	}

	/// <summary>
	/// MAUI tints the Toolbar only; under edge-to-edge the AppBarLayout also covers the status bar, so it must take the
	/// page's Shell.BackgroundColor as well (or return to colorSurface when the page sets none).
	/// </summary>
	static void StyleAppBar(Google.Android.Material.AppBar.AppBarLayout appBar)
	{
		int color;
		var colorToolbar = false;
		var surface = MaterialColors.GetColor(appBar, Resource.Attribute.colorSurface);
		if (Shell.Current is { } shell)
		{
			var requested = (shell.CurrentPage is { } page ? Shell.GetBackgroundColor(page) : null) ?? Shell.GetBackgroundColor(shell);
			color = requested?.ToPlatform().ToArgb() ?? surface;
		}
		else
		{
			// NavigationPage: flat app bar in the color of the page below it, unless the app chose a bar color
			var (navigation, leaf) = CurrentNavigation(Application.Current?.Windows.FirstOrDefault()?.Page);
			if (navigation is null)
				return;
			color = (navigation.BarBackgroundColor ?? leaf?.BackgroundColor)?.ToPlatform().ToArgb() ?? surface;
			colorToolbar = navigation.BarBackgroundColor is null;
		}
		// Trailing icons are onSurfaceVariant on a themed (surface) app bar; MAUI tints them like the navigation icon.
		if (color == surface || color == MaterialColors.GetColor(appBar, Resource.Attribute.colorSurfaceContainer))
			for (var i = 0; i < appBar.ChildCount; i++)
				if (appBar.GetChildAt(i) is AndroidX.AppCompat.Widget.Toolbar bar)
					TintTrailingIcons(bar, MaterialColors.GetColor(appBar, Resource.Attribute.colorOnSurfaceVariant));

		if (appBar.GetTag(Resource.Id.action_bar_container) is Java.Lang.Integer applied && applied.IntValue() == color)
			return;
		for (var i = 0; colorToolbar && i < appBar.ChildCount; i++)
			if (appBar.GetChildAt(i) is AndroidX.AppCompat.Widget.Toolbar toolbar)
				toolbar.SetBackgroundColor(new AColor(color));
		appBar.SetTag(Resource.Id.action_bar_container, Java.Lang.Integer.ValueOf(color));
		appBar.SetBackgroundColor(new AColor(color));
		appBar.SetStatusBarForegroundColor(color);
	}

	static void TintTrailingIcons(AndroidX.AppCompat.Widget.Toolbar toolbar, int color)
	{
		if (!OperatingSystem.IsAndroidVersionAtLeast(26) || toolbar.Menu is not { } menu)
			return;
		for (var i = 0; i < menu.Size(); i++)
			if (menu.GetItem(i) is { Icon: not null } item && item.IconTintList?.DefaultColor != color)
				item.SetIconTintList(Android.Content.Res.ColorStateList.ValueOf(new AColor(color)));
		if (toolbar.OverflowIcon is { } overflow && (toolbar.GetTag(Resource.Id.action_menu_presenter) as Java.Lang.Integer)?.IntValue() != color)
		{
			// MAUI colors the overflow glyph with a color filter, which takes precedence over a tint
			overflow.SetColorFilter(new PorterDuffColorFilter(new AColor(color), PorterDuff.Mode.SrcIn!));
			toolbar.SetTag(Resource.Id.action_menu_presenter, Java.Lang.Integer.ValueOf(color));
		}
	}

	/// <summary>
	/// Shell flyout / FlyoutPage sheet as an M3 modal navigation drawer: at most 360 dp wide, leaving 56 dp of scrim,
	/// with 16 dp corners on the trailing side.
	/// </summary>
	static void StyleDrawerSheet(AndroidX.DrawerLayout.Widget.DrawerLayout drawer)
	{
		if (drawer.Context is not { } context || drawer.Width <= 0)
			return;
		for (var i = 0; i < drawer.ChildCount; i++)
		{
			if (drawer.GetChildAt(i) is not { LayoutParameters: AndroidX.DrawerLayout.Widget.DrawerLayout.LayoutParams { Gravity: not (int)GravityFlags.NoGravity } layout } sheet)
				continue;
			var width = Math.Min((int)context.ToPixels(360), drawer.Width - (int)context.ToPixels(56));
			if (layout.Width != width)
			{
				layout.Width = width;
				sheet.LayoutParameters = layout;
			}
			if (sheet.OutlineProvider is not TrailingCornersOutline)
			{
				sheet.OutlineProvider = new TrailingCornersOutline(context.ToPixels(16));
				sheet.ClipToOutline = true;
			}
		}
	}

	/// <summary>Round-rect outline that extends past the leading edge, so only the trailing corners are rounded.</summary>
	sealed class TrailingCornersOutline(float radius) : ViewOutlineProvider
	{
		public override void GetOutline(AView? view, Outline? outline)
		{
			if (view is null || outline is null)
				return;
			var r = (int)radius;
			if (view.LayoutDirection == Android.Views.LayoutDirection.Rtl)
				outline.SetRoundRect(0, 0, view.Width + r, view.Height, radius);
			else
				outline.SetRoundRect(-r, 0, view.Width, view.Height, radius);
		}
	}

	/// <summary>Shell.SearchHandler: Material 3 search bar container instead of the elevated white card.</summary>
	static void StyleShellSearch(AndroidX.CardView.Widget.CardView card)
	{
		if (card.Context is not { } context || card.CardElevation == 0)
			return;
		card.CardElevation = 0;
		card.Radius = context.ToPixels(28);
		card.SetCardBackgroundColor(MaterialColors.GetColor(card, Resource.Attribute.colorSurfaceContainerHigh));
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
		// M3 Expressive floating toolbar (standard): surfaceContainer, elevation level 3
		var color = (view.TintColor ?? SystemColors.Get(SystemColorRole.GroupedBackground)).ToPlatform();

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

/// <summary>Attaches a layout listener to every activity so Shell's native chrome can be styled once it exists.</summary>
sealed class ShellChromeStyler : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
{
	public void OnActivityResumed(Android.App.Activity activity)
	{
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
	public void OnActivityDestroyed(Android.App.Activity activity) { }
	public void OnActivityPaused(Android.App.Activity activity) { }
	public void OnActivitySaveInstanceState(Android.App.Activity activity, Android.OS.Bundle outState) { }
	public void OnActivityStarted(Android.App.Activity activity) { }
	public void OnActivityStopped(Android.App.Activity activity) { }
}
