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

	static void MapButtonConfiguration(IButtonHandler handler, IButton button)
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

		if (button is ITextStyle textStyle)
		{
			var size = textStyle.Font.Size > 0 ? (nfloat)textStyle.Font.Size : UIFont.ButtonFontSize;
			var weight = textStyle.Font.Weight >= FontWeight.Bold ? UIFontWeight.Bold
				: prominent || textStyle.Font.Weight >= FontWeight.Semibold ? UIFontWeight.Semibold
				: UIFontWeight.Regular;
			var font = UIFont.SystemFontOfSize(size, weight);
			config.TitleTextAttributesTransformer = attrs =>
				new UIStringAttributes(attrs) { Font = font }.Dictionary;
		}

		// Explicit XAML colors win; otherwise the system tint, or systemRed for destructive buttons.
		var textColor = (button as ITextStyle)?.TextColor;
		var background = (button.Background as SolidPaint)?.Color;

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
}
