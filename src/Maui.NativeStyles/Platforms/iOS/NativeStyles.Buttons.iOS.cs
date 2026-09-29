using System.Runtime.CompilerServices;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace NativeStyles;

public static partial class NativeStylesExtensions
{
	static readonly UIButtonConfigurationUpdateHandler TemplateImageUpdateHandler = button =>
	{
		var image = button.Configuration?.Image ?? button.ImageForState(UIControlState.Normal);
		if (image is not { RenderingMode: not UIImageRenderingMode.AlwaysTemplate } || button.Configuration is not { } current)
			return;
		current.Image = image.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
		if (current.ImagePadding == 0)
			current.ImagePadding = 8;
		button.Configuration = current;
	};

	// MAUI mappings that overwrite (or fight with) the configuration: it is rebuilt after each of them.
	static readonly string[] ButtonConfigurationKeys =
	[
		MappingKey,
		nameof(IButton.Background),
		nameof(IButtonStroke.CornerRadius),
		nameof(IButtonStroke.StrokeThickness),
		nameof(ITextStyle.TextColor),
		nameof(ITextStyle.Font),
		nameof(IText.Text),
		nameof(IImageSourcePart.Source),
		nameof(IPadding.Padding),
		"LineBreakMode",
	];

	// The key mapped last when a handler maps every property (connecting): the first build happens there.
	static string s_buttonConnectKey = MappingKey;

	// The platform view each button's configuration was last built for.
	static readonly ConditionalWeakTable<IButton, WeakReference<UIButton>> s_configuredButtons = new();

	static void RegisterButtonConfiguration()
	{
		foreach (var key in ButtonConfigurationKeys)
			ButtonHandler.Mapper.AppendToMapping(key, (handler, button) => MapButtonConfiguration(handler, button, key));
		s_buttonConnectKey = ButtonHandler.Mapper.GetKeys().Distinct().Last(ButtonConfigurationKeys.Contains);
	}

	/// <summary>
	/// Connecting a handler maps all of <see cref="ButtonConfigurationKeys"/> in one pass (twelve calls, counting the
	/// Background updates nested in BackgroundColor and BackgroundImageSource): the configuration is built once, at the
	/// pass's last key, after every MAUI mapping of the pass; later changes rebuild it once each.
	/// </summary>
	static void MapButtonConfiguration(IButtonHandler handler, IButton button, string key)
	{
		var platformButton = handler.PlatformView;
		if (!s_configuredButtons.TryGetValue(button, out var built) || !built.TryGetTarget(out var target) || target != platformButton)
		{
			if (key != s_buttonConnectKey)
				return;
			s_configuredButtons.AddOrUpdate(button, new WeakReference<UIButton>(platformButton));
		}
		ApplyButtonConfiguration(handler, button);
	}

	static void ApplyButtonConfiguration(IButtonHandler handler, IButton button)
	{
		if (!OperatingSystem.IsIOSVersionAtLeast(15) || button is not BindableObject bindable)
			return;

		var platformButton = handler.PlatformView;
		var glass = OperatingSystem.IsIOSVersionAtLeast(26);
		var kind = NativeButton.GetKind(bindable);
		var destructive = NativeButton.GetIsDestructive(bindable);

		var (config, prominent) = kind switch
		{
			ButtonKind.GlassProminent => (glass ? UIButtonConfiguration.ProminentGlassButtonConfiguration : UIButtonConfiguration.FilledButtonConfiguration, true),
			ButtonKind.Glass => (glass ? UIButtonConfiguration.GlassButtonConfiguration : UIButtonConfiguration.GrayButtonConfiguration, false),
			ButtonKind.Filled => (UIButtonConfiguration.FilledButtonConfiguration, true),
			ButtonKind.Tonal => (UIButtonConfiguration.TintedButtonConfiguration, false),
			ButtonKind.Outlined => (UIButtonConfiguration.GrayButtonConfiguration, false),
			_ => (UIButtonConfiguration.PlainButtonConfiguration, false),
		};

		// iOS 26: every button outside bars is a capsule.
		config.CornerStyle = UIButtonConfigurationCornerStyle.Capsule;
		config.ButtonSize = NativeButton.GetSize(bindable) switch
		{
			ControlSize.Small => UIButtonConfigurationSize.Small,
			ControlSize.Large => UIButtonConfigurationSize.Large,
			_ => UIButtonConfigurationSize.Medium,
		};

		if (button is IText text)
			config.Title = text.Text;

		// Explicit XAML colors win; otherwise the system tint, or systemRed for destructive buttons.
		var textColor = (button as ITextStyle)?.TextColor;
		var background = (button.Background as SolidPaint)?.Color;

		// Brand: a light accent needs dark titles on prominent buttons (UIKit keeps them white, or tinted on glass)
		var (onAccentLight, onAccentDark) = prominent && !destructive && textColor is null && background is null
			? BrandOnAccent()
			: default;
		var onAccent = onAccentLight is null && onAccentDark is null ? null : (Func<UIColor, UIColor>)(incoming =>
			UIColor.FromDynamicProvider(traits => (traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? onAccentDark : onAccentLight)
				?? incoming.GetResolvedColor(traits)));

		if (button is ITextStyle textStyle)
		{
			// The app's font manager applies FontFamily and Dynamic Type scaling, as for every other MAUI text
			var font = (handler.MauiContext?.Services.GetService(typeof(IFontManager)) as IFontManager)?.GetFont(textStyle.Font, UIFont.ButtonFontSize)
				?? UIFont.SystemFontOfSize(textStyle.Font.Size > 0 ? (nfloat)textStyle.Font.Size : UIFont.ButtonFontSize);
			UIFontWeight? weight = textStyle.Font.Weight >= FontWeight.Bold ? UIFontWeight.Bold
				: prominent || textStyle.Font.Weight >= FontWeight.Semibold ? UIFontWeight.Semibold
				: null;
			if (weight is { } w)
				font = WithWeight(font, w);
			config.TitleTextAttributesTransformer = attrs =>
			{
				var transformed = new UIStringAttributes(attrs) { Font = font };
				if (onAccent is not null && transformed.ForegroundColor is { } incoming)
					transformed.ForegroundColor = onAccent(incoming);
				return transformed.Dictionary;
			};
		}

		if (background is not null)
			config.BaseBackgroundColor = background.ToPlatform();
		else if (destructive && prominent)
			config.BaseBackgroundColor = UIColor.SystemRed;

		if (textColor is not null)
			config.BaseForegroundColor = textColor.ToPlatform();
		else if (destructive && !prominent)
			config.BaseForegroundColor = UIColor.SystemRed;

		// NativeButton.TintsImage / NativeImage.TintColor: template image in the label color (or an explicit one)
		var explicitTint = NativeImage.GetTintColor(bindable)?.ToPlatform();
		var tintsImage = explicitTint is not null || NativeButton.GetTintsImage(bindable);
		if (platformButton.CurrentImage is { } image)
		{
			config.Image = tintsImage ? image.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate) : image;
			config.ImagePadding = 8;
		}
		if (explicitTint is not null)
			config.ImageColorTransformer = _ => explicitTint;
		else if (onAccent is not null)
			config.ImageColorTransformer = incoming => onAccent(incoming);
		// MAUI assigns the image when its source finishes loading, after this mapping ran: convert it then
		platformButton.ConfigurationUpdateHandler = tintsImage ? TemplateImageUpdateHandler : null;

		// Keep MAUI's measurement and UIKit's rendering in agreement: the configuration owns the insets.
		var padding = button.Padding;
		config.ContentInsets = new NSDirectionalEdgeInsets(
			(nfloat)padding.Top, (nfloat)padding.Left, (nfloat)padding.Bottom, (nfloat)padding.Right);
		config.TitleLineBreakMode = UILineBreakMode.TailTruncation;

		// Undo what MAUI's own mappers painted so the configuration is the only source of truth.
		platformButton.BackgroundColor = UIColor.Clear;
		platformButton.Layer.CornerRadius = 0;
		platformButton.Layer.BorderWidth = 0;
		platformButton.Configuration = config;
		platformButton.TitleLabel.Lines = 1;
		button.InvalidateMeasure();
	}

	/// <summary>
	/// <see cref="SystemColorRole.OnAccent"/> of the brand, light and dark, where it is not the white UIKit already draws
	/// on the tint (null keeps UIKit's title color, including the tinted one of prominent glass).
	/// </summary>
	static (UIColor? Light, UIColor? Dark) BrandOnAccent()
	{
		if (SystemColors.Brand is null)
			return default;
		var (light, dark) = SystemColors.Resolve(SystemColorRole.OnAccent);
		return (IsWhite(light) ? null : light.ToPlatform(), IsWhite(dark) ? null : dark.ToPlatform());

		static bool IsWhite(Color color) => color is { Red: >= 1f, Green: >= 1f, Blue: >= 1f };
	}
}
