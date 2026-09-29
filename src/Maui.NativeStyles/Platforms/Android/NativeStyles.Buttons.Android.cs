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
	// Marks buttons whose colors were overridden by MapDestructiveText, so they can be restored.
	static readonly BindableProperty DestructiveAppliedProperty =
		BindableProperty.CreateAttached("DestructiveApplied", typeof(bool), typeof(NativeStylesExtensions), false);

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
		if (button is not Button b)
			return;

		var kind = NativeButton.GetKind(b);
		var applies = NativeButton.GetIsDestructive(b) && kind is ButtonKind.Text or ButtonKind.Outlined;
		var applied = (bool)b.GetValue(DestructiveAppliedProperty);

		if (applies)
		{
			// Theme-aware local values win over the "Destructive" style setters (error-filled container).
			var (error, errorDark) = ResolveThemeColors("Error", "#B3261E", "#F2B8B5");
			b.SetAppThemeColor(Button.TextColorProperty, error, errorDark);
			b.SetValue(Button.BackgroundColorProperty, Colors.Transparent);
			if (kind == ButtonKind.Outlined)
				b.SetAppThemeColor(Button.BorderColorProperty, error, errorDark);
			b.SetValue(DestructiveAppliedProperty, true);
		}
		else if (applied)
		{
			b.RemoveBinding(Button.TextColorProperty);
			b.ClearValue(Button.TextColorProperty);
			b.ClearValue(Button.BackgroundColorProperty);
			b.RemoveBinding(Button.BorderColorProperty);
			b.ClearValue(Button.BorderColorProperty);
			b.SetValue(DestructiveAppliedProperty, false);
		}
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

	static (Color light, Color dark) ResolveThemeColors(string key, string lightFallback, string darkFallback)
	{
		var resources = Application.Current?.Resources;
		var light = resources?.TryGetValue(key, out var l) == true && l is Color lc ? lc : Color.FromArgb(lightFallback);
		var dark = resources?.TryGetValue(key + "Dark", out var d) == true && d is Color dc ? dc : Color.FromArgb(darkFallback);
		return (light, dark);
	}
}
