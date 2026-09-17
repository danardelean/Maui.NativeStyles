using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;
using PlatformContentView = Microsoft.Maui.Platform.ContentView;

namespace MauiNativeStyle.NativeStyles;

public static partial class NativeStylesExtensions
{
	static partial void RegisterPlatformHandlers(IMauiHandlersCollection handlers)
	{
		handlers.AddHandler<GlassView, GlassViewHandler>();
	}

	static partial void RegisterPlatformMappers()
	{
		// UIButtonConfiguration (iOS 15+) / Liquid Glass (iOS 26+), driven by the NativeButton attached properties.
		// Re-applied after every MAUI mapping that would otherwise overwrite the configuration.
		ButtonHandler.Mapper.AppendToMapping(MappingKey, MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IButton.Background), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IButtonStroke.CornerRadius), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IButtonStroke.StrokeThickness), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.TextColor), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IText.Text), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IImageSourcePart.Source), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping(nameof(IPadding.Padding), MapButtonConfiguration);
		ButtonHandler.Mapper.AppendToMapping("LineBreakMode", MapButtonConfiguration);

		// Entry: MAUI already uses UITextBorderStyle.RoundedRect; NativeEntry.IsPlain removes the border (grouped cells).
		EntryHandler.Mapper.AppendToMapping(MappingKey, MapEntryBorder);

		// Label: MAUI FontAttributes has no Semibold; NativeText.Weight supplies the SF Pro weight.
		LabelHandler.Mapper.AppendToMapping(MappingKey, MapLabelWeight);
		LabelHandler.Mapper.AppendToMapping(nameof(ILabel.Font), MapLabelWeight);
	}

	static void MapEntryBorder(IEntryHandler handler, IEntry entry)
	{
		if (entry is not BindableObject bindable)
			return;
		handler.PlatformView.BorderStyle = NativeEntry.GetIsPlain(bindable)
			? UITextBorderStyle.None
			: UITextBorderStyle.RoundedRect;
	}

	static void MapLabelWeight(ILabelHandler handler, ILabel label)
	{
		if (label is not BindableObject bindable || handler.PlatformView.Font is not { } current)
			return;

		var weight = NativeText.GetWeight(bindable) switch
		{
			TextWeight.Medium => UIFontWeight.Medium,
			TextWeight.Semibold => UIFontWeight.Semibold,
			TextWeight.Bold => UIFontWeight.Bold,
			_ => (UIFontWeight?)null,
		};
		if (weight is { } w)
			handler.PlatformView.Font = UIFont.SystemFontOfSize(current.PointSize, w)!;
	}

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

		if (platformButton.CurrentImage is { } image)
		{
			config.Image = image;
			config.ImagePadding = 8;
		}

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

/// <summary>Hosts a UIGlassEffect (iOS 26) or a system-material blur behind the MAUI content.</summary>
public class GlassViewHandler : ContentViewHandler
{
	public static readonly IPropertyMapper<GlassView, GlassViewHandler> GlassMapper =
		new PropertyMapper<GlassView, GlassViewHandler>(ContentViewHandler.Mapper)
		{
			[nameof(GlassView.GlassStyle)] = MapGlass,
			[nameof(GlassView.CornerRadius)] = MapGlass,
			[nameof(GlassView.TintColor)] = MapGlass,
			[nameof(GlassView.IsInteractive)] = MapGlass,
		};

	public GlassViewHandler() : base(GlassMapper)
	{
	}

	protected override PlatformContentView CreatePlatformView()
	{
		_ = VirtualView ?? throw new InvalidOperationException($"{nameof(VirtualView)} must be set.");
		return new GlassContentView { CrossPlatformLayout = VirtualView };
	}

	static void MapGlass(GlassViewHandler handler, GlassView view)
	{
		if (handler.PlatformView is GlassContentView glassView)
			glassView.Update(view);
	}
}

public class GlassContentView : PlatformContentView
{
	readonly UIVisualEffectView _effectView = new();
	double _cornerRadius = -1;

	public void Update(GlassView view)
	{
		_cornerRadius = view.CornerRadius;
		if (OperatingSystem.IsIOSVersionAtLeast(26))
		{
			var effect = UIGlassEffect.Create(view.GlassStyle == GlassStyle.Clear ? UIGlassEffectStyle.Clear : UIGlassEffectStyle.Regular);
			effect.Interactive = view.IsInteractive;
			effect.TintColor = view.TintColor?.ToPlatform();
			_effectView.Effect = effect;
			// Liquid Glass ignores Layer.CornerRadius: shape must come from UICornerConfiguration.
			_effectView.CornerConfiguration = _cornerRadius < 0
				? UICornerConfiguration.CreateCapsule()
				: UICornerConfiguration.CreateUniformCorners(UICornerRadius.CreateFixed((nfloat)_cornerRadius));
		}
		else
		{
			_effectView.Effect = UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemMaterial);
			_effectView.ClipsToBounds = true;
		}
		SetNeedsLayout();
	}

	public override void LayoutSubviews()
	{
		base.LayoutSubviews();

		// MAUI's ContentViewHandler clears all subviews when Content changes: keep the glass at the back.
		if (_effectView.Superview != this)
			InsertSubview(_effectView, 0);
		_effectView.Frame = Bounds;

		var radius = _cornerRadius < 0 ? Math.Min(Bounds.Width, Bounds.Height) / 2 : _cornerRadius;
		if (!OperatingSystem.IsIOSVersionAtLeast(26))
			_effectView.Layer.CornerRadius = (nfloat)radius;
		Layer.CornerRadius = (nfloat)radius;
		ClipsToBounds = true;
	}
}
