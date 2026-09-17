using Microsoft.Maui.Controls.Shapes;

namespace NativeStyles;

/// <summary>Visual role of a <see cref="NativeSwipeItem"/>.</summary>
public enum SwipeRole
{
	/// <summary>Secondary action: iOS systemGray / Material secondaryContainer (tonal).</summary>
	Default,
	/// <summary>Main action, placed last: accent / Material primary.</summary>
	Primary,
	/// <summary>Destructive action: systemRed / Material error.</summary>
	Destructive,
}

/// <summary>
/// Swipe action drawn like the platform: iOS 26 shows separated, continuously rounded buttons; Material 3 Expressive
/// lists reveal round tonal/filled icon buttons. MAUI's <see cref="SwipeItem"/> always draws square color blocks.
/// Shows the icon when there is one, the text otherwise (the text is always the accessibility description).
/// </summary>
public class NativeSwipeItem : SwipeItemView
{
	public static readonly BindableProperty TextProperty = BindableProperty.Create(
		nameof(Text), typeof(string), typeof(NativeSwipeItem), null, propertyChanged: (b, _, _) => ((NativeSwipeItem)b).Update());

	public static readonly BindableProperty IconProperty = BindableProperty.Create(
		nameof(Icon), typeof(ImageSource), typeof(NativeSwipeItem), null, propertyChanged: (b, _, _) => ((NativeSwipeItem)b).Update());

	public static readonly BindableProperty RoleProperty = BindableProperty.Create(
		nameof(Role), typeof(SwipeRole), typeof(NativeSwipeItem), SwipeRole.Default, propertyChanged: (b, _, _) => ((NativeSwipeItem)b).Update());

	readonly Border _container;
	readonly Image _icon;
	readonly Label _label;

	public NativeSwipeItem()
	{
		var ios = DeviceInfo.Platform == DevicePlatform.iOS;
		_icon = new Image { WidthRequest = 24, HeightRequest = 24, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		_label = new Label
		{
			FontSize = ios ? 15 : 14,
			LineBreakMode = LineBreakMode.TailTruncation,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
		};
		_container = new Border
		{
			// iOS 26: 26pt continuous corners (a capsule on a 52pt row), 6pt between actions.
			// Material 3: fully round 56dp buttons, 4dp between actions.
			StrokeShape = new RoundRectangle { CornerRadius = ios ? 26 : 28 },
			StrokeThickness = 0,
			Shadow = null!,
			Padding = new Thickness(ios ? 14 : 8, 0),
			Margin = ios ? new Thickness(6, 0, 0, 0) : new Thickness(4, 0, 0, 0),
			MinimumWidthRequest = ios ? 68 : 56,
			Content = new Grid { Children = { _icon, _label } },
		};
		Content = _container;
		Update();
	}

	public string? Text
	{
		get => (string?)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public ImageSource? Icon
	{
		get => (ImageSource?)GetValue(IconProperty);
		set => SetValue(IconProperty, value);
	}

	public SwipeRole Role
	{
		get => (SwipeRole)GetValue(RoleProperty);
		set => SetValue(RoleProperty, value);
	}

	void Update()
	{
		var ios = DeviceInfo.Platform == DevicePlatform.iOS;
		var (background, foreground) = Role switch
		{
			SwipeRole.Primary => (SystemColorRole.Accent, SystemColorRole.OnAccent),
			SwipeRole.Destructive => (SystemColorRole.Destructive, SystemColorRole.OnAccent),
			_ => ios ? (SystemColorRole.Gray, SystemColorRole.OnAccent) : (SystemColorRole.TonalContainer, SystemColorRole.OnTonalContainer),
		};
		_container.SetBinding(Border.BackgroundProperty, SystemColors.GetBinding(background));
		_label.SetBinding(Label.TextColorProperty, SystemColors.GetBinding(foreground));

		var hasIcon = Icon is not null;
		_icon.Source = Icon;
		_icon.IsVisible = hasIcon;
		_label.Text = Text;
		_label.IsVisible = !hasIcon;
		SemanticProperties.SetDescription(this, Text);
		// Icons follow the label color: font glyphs through FontImageSource.Color, bitmaps through NativeImage.TintColor
		if (Icon is FontImageSource { Color: null } glyph)
			glyph.SetBinding(FontImageSource.ColorProperty, SystemColors.GetBinding(foreground));
		_icon.SetBinding(NativeImage.TintColorProperty, SystemColors.GetBinding(foreground));
	}
}
