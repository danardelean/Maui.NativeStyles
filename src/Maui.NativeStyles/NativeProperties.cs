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
}
