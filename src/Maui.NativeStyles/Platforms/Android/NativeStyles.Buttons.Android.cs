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

	static void MapStepperButtons(IStepperHandler handler, IStepper stepper)
	{
		if (handler.PlatformView is not ViewGroup group || handler.MauiContext?.Context is not { } context)
			return;
		var (outline, _) = SystemColors.Resolve(SystemColorRole.Separator);
		var (primary, primaryDark) = SystemColors.Resolve(SystemColorRole.Accent);
		var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
		var size = (int)context.ToPixels(40);
		for (var i = 0; i < group.ChildCount; i++)
		{
			if (group.GetChildAt(i) is not AButton button)
				continue;
			var drawable = new GradientDrawable();
			drawable.SetColor(AColor.Transparent);
			drawable.SetStroke((int)context.ToPixels(1), (dark ? SystemColors.Resolve(SystemColorRole.Separator).Dark : outline).ToPlatform());
			drawable.SetCornerRadius(context.ToPixels(20));
			button.Background = drawable;
			button.SetTextColor((dark ? primaryDark : primary).ToPlatform());
			button.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
			button.SetMinimumWidth(size); button.SetMinWidth(size);
			button.SetMinimumHeight(size); button.SetMinHeight(size);
			button.SetPadding(0, 0, 0, 0);
			if (button.LayoutParameters is ViewGroup.MarginLayoutParams lp)
			{
				lp.Width = size; lp.Height = size;
				lp.SetMargins(i == 0 ? 0 : (int)context.ToPixels(8), 0, 0, 0);
				button.LayoutParameters = lp;
			}
		}
	}
}
