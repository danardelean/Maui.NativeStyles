using CoreGraphics;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace NativeStyles;

public static partial class NativeStylesExtensions
{
	/// <summary>Corner radius of iOS 26 grouped shapes; a 52pt single-row field therefore reads as a capsule.</summary>
	const float FieldCornerRadius = 26;

	/// <summary>Leading/trailing content margin of an iOS 26 grouped row.</summary>
	const float FieldContentMargin = 20;

	static void MapEntryChrome(IEntryHandler handler, IEntry entry)
	{
		if (entry is not BindableObject bindable)
			return;
		var field = handler.PlatformView;
		field.BorderStyle = UITextBorderStyle.None;
		if (field is NativeTextField native)
		{
			var plain = NativeEntry.GetIsPlain(bindable);
			native.CornerRadius = plain ? 0 : FieldCornerRadius;
			native.ContentMargin = plain ? 0 : FieldContentMargin;
		}
	}

	static void MapEditorChrome(IEditorHandler handler, IEditor editor)
	{
		var view = handler.PlatformView;
		view.Layer.CornerRadius = FieldCornerRadius;
		view.Layer.CornerCurve = CoreAnimation.CACornerCurve.Continuous;
		view.ClipsToBounds = true;
		view.TextContainer.LineFragmentPadding = FieldContentMargin;
		view.TextContainerInset = new UIEdgeInsets(15, 0, 15, 0);
	}

	/// <summary>Pull-down look: no border, value text followed by the chevron.up.chevron.down glyph.</summary>
	static void MapPickerChrome(IPickerHandler handler, IPicker picker)
	{
		var field = handler.PlatformView;
		field.BorderStyle = UITextBorderStyle.None;
		field.BackgroundColor = UIColor.Clear;
		if (field.RightView is not UIImageView)
		{
			var chevrons = new UIImageView(UIImage.GetSystemImage("chevron.up.chevron.down",
				UIImageSymbolConfiguration.Create(UIFont.SystemFontOfSize(13, UIFontWeight.Semibold))))
			{
				TintColor = UIColor.SecondaryLabel,
				ContentMode = UIViewContentMode.Center,
				Frame = new CGRect(0, 0, 22, 20),
			};
			field.RightView = chevrons;
			field.RightViewMode = UITextFieldViewMode.Always;
		}
	}

	/// <summary>Compact UIDatePicker look (iOS 26): tertiarySystemFill capsule, 34 pt tall, 12 pt horizontal padding.</summary>
	static void MapCompactPickerChrome(UITextField field, IView view)
	{
		field.BorderStyle = UITextBorderStyle.None;
		field.BackgroundColor = (view.Background as SolidPaint)?.Color?.ToPlatform() ?? UIColor.TertiarySystemFill;
		field.Layer.CornerRadius = 17; // capsule for the 34 pt height set by the style
		field.ClipsToBounds = true;
		field.TextAlignment = UITextAlignment.Center;
		if (field.LeftView is null)
		{
			field.LeftView = new UIView(new CGRect(0, 0, 12, 1));
			field.LeftViewMode = UITextFieldViewMode.Always;
			field.RightView = new UIView(new CGRect(0, 0, 12, 1));
			field.RightViewMode = UITextFieldViewMode.Always;
		}
	}
}

/// <summary><see cref="EntryHandler"/> that creates a <see cref="NativeTextField"/>; mappings are unchanged.</summary>
public class NativeEntryHandler : EntryHandler
{
	protected override MauiTextField CreatePlatformView()
	{
		// The base view carries MAUI's "Done" accessory toolbar, which resolves the field through the handler.
		var template = base.CreatePlatformView();
		var field = new NativeTextField
		{
			BorderStyle = UITextBorderStyle.None,
			ClipsToBounds = true,
			InputAccessoryView = template.InputAccessoryView,
		};
		template.InputAccessoryView = null;
		return field;
	}
}

/// <summary>UITextField with a horizontal content margin and continuous rounded corners.</summary>
public class NativeTextField : MauiTextField
{
	// Distance of the clear button's center from the trailing edge in a Settings text row.
	const float ClearButtonCenterInset = 35;

	nfloat _cornerRadius;
	nfloat _contentMargin;

	public nfloat CornerRadius
	{
		get => _cornerRadius;
		set { _cornerRadius = value; SetNeedsLayout(); }
	}

	public nfloat ContentMargin
	{
		get => _contentMargin;
		set { _contentMargin = value; SetNeedsLayout(); }
	}

	public override void LayoutSubviews()
	{
		base.LayoutSubviews();
		Layer.CornerCurve = CoreAnimation.CACornerCurve.Continuous;
		Layer.CornerRadius = (nfloat)Math.Min(_cornerRadius, Bounds.Height / 2);
	}

	public override CGRect TextRect(CGRect forBounds) => Inset(base.TextRect(forBounds));

	public override CGRect EditingRect(CGRect forBounds) => Inset(base.EditingRect(forBounds));

	public override CGRect ClearButtonRect(CGRect forBounds)
	{
		var rect = base.ClearButtonRect(forBounds);
		if (_contentMargin <= 0)
			return rect;
		var leftToRight = EffectiveUserInterfaceLayoutDirection == UIUserInterfaceLayoutDirection.LeftToRight;
		var centerX = leftToRight ? forBounds.Right - ClearButtonCenterInset : forBounds.Left + ClearButtonCenterInset;
		return new CGRect(centerX - rect.Width / 2, rect.Y, rect.Width, rect.Height);
	}

	CGRect Inset(CGRect rect)
	{
		if (_contentMargin <= 0)
			return rect;
		var width = (nfloat)Math.Max(0, rect.Width - 2 * _contentMargin);
		return new CGRect(rect.X + _contentMargin, rect.Y, width, rect.Height);
	}
}
