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
	static void MapButtonIconTint(IButtonHandler handler, IButton button)
	{
		if (button is not BindableObject bindable || handler.PlatformView is not Google.Android.Material.Button.MaterialButton platformButton)
			return;
		var explicitTint = NativeImage.GetTintColor(bindable);
		if (explicitTint is null && !NativeButton.GetTintsImage(bindable))
		{
			if (platformButton.IconTintMode == PorterDuff.Mode.SrcIn)
			{
				platformButton.IconTintMode = PorterDuff.Mode.Add;
				platformButton.IconTint = Android.Content.Res.ColorStateList.ValueOf(AColor.Transparent);
			}
			return;
		}
		platformButton.IconTintMode = PorterDuff.Mode.SrcIn;
		platformButton.IconTint = explicitTint is not null
			? Android.Content.Res.ColorStateList.ValueOf(explicitTint.ToPlatform())
			: platformButton.TextColors; // enabled / disabled label colors
	}

	static void MapDestructiveText(IButtonHandler handler, IButton button)
	{
		if (button is Button b && NativeButton.GetIsDestructive(b))
			DestructiveButtons.Attach(b);
	}

	// M3 outlined icon buttons: a 40 dp outline inside a 48 dp touch target.
	const double StepperButtonSize = 40;
	const double StepperTouchInset = (48 - StepperButtonSize) / 2;

	static void MapStepperButtons(IStepperHandler handler, IStepper stepper)
	{
		if (handler.PlatformView is not ViewGroup group || handler.MauiContext?.Context is not { } context)
			return;
		var (outline, _) = SystemColors.Resolve(SystemColorRole.Separator);
		var (primary, primaryDark) = SystemColors.Resolve(SystemColorRole.Accent);
		var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
		var inset = (int)context.ToPixels(StepperTouchInset);
		var size = (int)context.ToPixels(StepperButtonSize + 2 * StepperTouchInset);
		for (var i = 0; i < group.ChildCount; i++)
		{
			if (group.GetChildAt(i) is not AButton button)
				continue;
			var drawable = new GradientDrawable();
			drawable.SetColor(AColor.Transparent);
			drawable.SetStroke((int)context.ToPixels(1), (dark ? SystemColors.Resolve(SystemColorRole.Separator).Dark : outline).ToPlatform());
			drawable.SetCornerRadius(context.ToPixels(20));
			button.Background = new InsetDrawable(drawable, inset);
			button.SetTextColor((dark ? primaryDark : primary).ToPlatform());
			button.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
			button.SetMinimumWidth(size); button.SetMinWidth(size);
			button.SetMinimumHeight(size); button.SetMinHeight(size);
			button.SetPadding(0, 0, 0, 0);
			if (button.LayoutParameters is ViewGroup.MarginLayoutParams lp)
			{
				lp.Width = size; lp.Height = size;
				// 8 dp between the outlines, which the two touch insets already provide
				lp.SetMargins(i == 0 ? 0 : (int)context.ToPixels(8 - 2 * StepperTouchInset), 0, 0, 0);
				button.LayoutParameters = lp;
			}
		}
		if (handler is NativeStepperHandler native)
			native.TouchInset = StepperTouchInset;
	}
}

/// <summary>
/// <see cref="StepperHandler"/> whose 40 dp buttons are 48 dp touch targets: the stepper measures as its visible buttons
/// and its view extends past that frame by the extra touch area, so layouts do not change. Mappings are unchanged.
/// </summary>
public class NativeStepperHandler : StepperHandler
{
	/// <summary>Touch area (dp) around the visible buttons, set by the native-style mapping.</summary>
	internal double TouchInset { get; set; }

	public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
	{
		var size = base.GetDesiredSize(widthConstraint, heightConstraint);
		return new Size(Math.Max(0, size.Width - 2 * TouchInset), Math.Max(0, size.Height - 2 * TouchInset));
	}

	public override void PlatformArrange(Microsoft.Maui.Graphics.Rect frame) =>
		base.PlatformArrange(new Microsoft.Maui.Graphics.Rect(
			frame.X - TouchInset, frame.Y - TouchInset, frame.Width + 2 * TouchInset, frame.Height + 2 * TouchInset));
}
