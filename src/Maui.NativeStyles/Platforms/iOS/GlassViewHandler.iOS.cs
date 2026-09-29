using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;
using PlatformContentView = Microsoft.Maui.Platform.ContentView;

namespace NativeStyles;

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
