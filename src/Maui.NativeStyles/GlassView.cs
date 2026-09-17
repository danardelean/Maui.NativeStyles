namespace NativeStyles;

public enum GlassStyle
{
	/// <summary>Blurs and adjusts luminosity of the background (default, for text-heavy content).</summary>
	Regular,
	/// <summary>Highly translucent, for media backgrounds. Apple recommends a 35% dark dimming layer over bright content.</summary>
	Clear,
}

/// <summary>
/// Container that floats above content with iOS 26 Liquid Glass (UIGlassEffect).
/// Falls back to a system-material blur on iOS 15–18 and to a Material 3 elevated card on Android.
/// Per Apple HIG, use it only for floating controls/navigation, never for content-layer elements.
/// </summary>
public class GlassView : ContentView
{
	public static readonly BindableProperty GlassStyleProperty =
		BindableProperty.Create(nameof(GlassStyle), typeof(GlassStyle), typeof(GlassView), GlassStyle.Regular);

	/// <summary>Corner radius in points. -1 (default) = capsule.</summary>
	public static readonly BindableProperty CornerRadiusProperty =
		BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(GlassView), -1d);

	public static readonly BindableProperty TintColorProperty =
		BindableProperty.Create(nameof(TintColor), typeof(Color), typeof(GlassView), null);

	public static readonly BindableProperty IsInteractiveProperty =
		BindableProperty.Create(nameof(IsInteractive), typeof(bool), typeof(GlassView), true);

	public GlassStyle GlassStyle
	{
		get => (GlassStyle)GetValue(GlassStyleProperty);
		set => SetValue(GlassStyleProperty, value);
	}

	public double CornerRadius
	{
		get => (double)GetValue(CornerRadiusProperty);
		set => SetValue(CornerRadiusProperty, value);
	}

	public Color? TintColor
	{
		get => (Color?)GetValue(TintColorProperty);
		set => SetValue(TintColorProperty, value);
	}

	public bool IsInteractive
	{
		get => (bool)GetValue(IsInteractiveProperty);
		set => SetValue(IsInteractiveProperty, value);
	}
}
