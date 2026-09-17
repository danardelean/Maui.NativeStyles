namespace NativeStyles;

public enum ButtonKind
{
	/// <summary>Platform default: plain (iOS) / filled (Android).</summary>
	Automatic,
	Filled,
	Tonal,
	Outlined,
	Text,
	Glass,
	GlassProminent,
}

public enum ControlSize
{
	Default,
	Small,
	Large,
}

public enum TextWeight
{
	Regular,
	Medium,
	Semibold,
	Bold,
}

/// <summary>
/// Typed attached properties read by the platform handlers. The shared StyleClass names
/// (Filled, Tonal, …) are XAML styles that set these properties, so both forms are equivalent:
/// <c>StyleClass="Filled"</c> or <c>native:NativeButton.Kind="Filled"</c>.
/// Changing a value at runtime re-runs the handler mapping.
/// </summary>
public static class NativeButton
{
	public static readonly BindableProperty KindProperty = BindableProperty.CreateAttached(
		"Kind", typeof(ButtonKind), typeof(NativeButton), ButtonKind.Automatic, propertyChanged: OnChanged);

	public static readonly BindableProperty IsDestructiveProperty = BindableProperty.CreateAttached(
		"IsDestructive", typeof(bool), typeof(NativeButton), false, propertyChanged: OnChanged);

	public static readonly BindableProperty SizeProperty = BindableProperty.CreateAttached(
		"Size", typeof(ControlSize), typeof(NativeButton), ControlSize.Default, propertyChanged: OnChanged);

	/// <summary>
	/// Draws the button image as a template in the label color, like an SF Symbol in a UIButton or the icon of a Material
	/// button. Off by default because MAUI button images may be full-color artwork. For a specific color use
	/// <see cref="NativeImage.TintColorProperty"/> instead.
	/// </summary>
	public static readonly BindableProperty TintsImageProperty = BindableProperty.CreateAttached(
		"TintsImage", typeof(bool), typeof(NativeButton), false, propertyChanged: OnChanged);

	public static bool GetTintsImage(BindableObject view) => (bool)view.GetValue(TintsImageProperty);
	public static void SetTintsImage(BindableObject view, bool value) => view.SetValue(TintsImageProperty, value);

	public static ButtonKind GetKind(BindableObject view) => (ButtonKind)view.GetValue(KindProperty);
	public static void SetKind(BindableObject view, ButtonKind value) => view.SetValue(KindProperty, value);

	public static bool GetIsDestructive(BindableObject view) => (bool)view.GetValue(IsDestructiveProperty);
	public static void SetIsDestructive(BindableObject view, bool value) => view.SetValue(IsDestructiveProperty, value);

	public static ControlSize GetSize(BindableObject view) => (ControlSize)view.GetValue(SizeProperty);
	public static void SetSize(BindableObject view, ControlSize value) => view.SetValue(SizeProperty, value);

	static void OnChanged(BindableObject bindable, object oldValue, object newValue) =>
		NativeStylesExtensions.Refresh(bindable);
}

public static class NativeText
{
	public static readonly BindableProperty WeightProperty = BindableProperty.CreateAttached(
		"Weight", typeof(TextWeight), typeof(NativeText), TextWeight.Regular,
		propertyChanged: (b, _, _) => NativeStylesExtensions.Refresh(b));

	public static TextWeight GetWeight(BindableObject view) => (TextWeight)view.GetValue(WeightProperty);
	public static void SetWeight(BindableObject view, TextWeight value) => view.SetValue(WeightProperty, value);
}

public static class NativeEntry
{
	/// <summary>Removes the platform border/box (iOS RoundedRect, Material outlined box) for fields inside list rows.</summary>
	public static readonly BindableProperty IsPlainProperty = BindableProperty.CreateAttached(
		"IsPlain", typeof(bool), typeof(NativeEntry), false,
		propertyChanged: (b, _, _) => NativeStylesExtensions.Refresh(b));

	public static bool GetIsPlain(BindableObject view) => (bool)view.GetValue(IsPlainProperty);
	public static void SetIsPlain(BindableObject view, bool value) => view.SetValue(IsPlainProperty, value);

	/// <summary>
	/// Borderless field inside a filled, rounded container (Material 3 Expressive containment: no outline, 28 dp corners).
	/// Applies to Entry and Editor on Android; iOS 26 fields already look like this, so it is a no-op there.
	/// </summary>
	public static readonly BindableProperty IsContainedProperty = BindableProperty.CreateAttached(
		"IsContained", typeof(bool), typeof(NativeEntry), false,
		propertyChanged: (b, _, _) => NativeStylesExtensions.Refresh(b));

	public static bool GetIsContained(BindableObject view) => (bool)view.GetValue(IsContainedProperty);
	public static void SetIsContained(BindableObject view, bool value) => view.SetValue(IsContainedProperty, value);
}

/// <summary>List item shaping for Material 3 Expressive segmented lists.</summary>
public static class NativeList
{
	/// <summary>
	/// Corner radius (dp) of a list item's own container. Android: the view background is drawn as a rounded shape
	/// (4 dp at rest, 16 dp when selected); the enclosing GroupedCell clips the outer corners to 16 dp. No effect on iOS,
	/// where the grouped section provides the shape. A negative value (default) leaves the background untouched.
	/// </summary>
	public static readonly BindableProperty ItemCornerRadiusProperty = BindableProperty.CreateAttached(
		"ItemCornerRadius", typeof(double), typeof(NativeList), -1d,
		propertyChanged: (b, _, _) => NativeStylesExtensions.Refresh(b));

	public static double GetItemCornerRadius(BindableObject view) => (double)view.GetValue(ItemCornerRadiusProperty);
	public static void SetItemCornerRadius(BindableObject view, double value) => view.SetValue(ItemCornerRadiusProperty, value);
}

/// <summary>Template-style tinting for <see cref="Image"/>, <see cref="ImageButton"/> and <see cref="Button"/> images (tintColor / color filter).</summary>
public static class NativeImage
{
	/// <summary>Draws the image as a single-color template. Null (default) keeps the original colors.</summary>
	public static readonly BindableProperty TintColorProperty = BindableProperty.CreateAttached(
		"TintColor", typeof(Color), typeof(NativeImage), null,
		propertyChanged: (b, _, _) => NativeStylesExtensions.Refresh(b));

	public static Color? GetTintColor(BindableObject view) => (Color?)view.GetValue(TintColorProperty);
	public static void SetTintColor(BindableObject view, Color? value) => view.SetValue(TintColorProperty, value);
}
