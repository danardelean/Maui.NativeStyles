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
	static void MapListItemShape(IViewHandler handler, IView view)
	{
		if (view is not BindableObject bindable || handler.PlatformView is not AView platformView || platformView.Context is not { } context)
			return;
		var radius = NativeList.GetItemCornerRadius(bindable);
		if (radius < 0)
			return;
		var shape = new GradientDrawable();
		shape.SetShape(ShapeType.Rectangle);
		shape.SetCornerRadius(context.ToPixels(radius));
		shape.SetColor(((view.Background as SolidPaint)?.Color ?? Colors.Transparent).ToPlatform());
		platformView.Background = shape;
		platformView.ClipToOutline = true;

		// A native radio button used as a list item: move the control to the 16 dp keyline, 12 dp before the label.
		if (OperatingSystem.IsAndroidVersionAtLeast(23)
			&& platformView is Android.Widget.CompoundButton { ButtonDrawable: { } button and not InsetDrawable } compound)
		{
			compound.SetButtonDrawable(new InsetDrawable(button, (int)context.ToPixels(10), 0, 0, 0));
			compound.SetPadding((int)context.ToPixels(10), compound.PaddingTop, (int)context.ToPixels(16), compound.PaddingBottom);
		}
	}

	static void MapLabelWeight(ILabelHandler handler, ILabel label)
	{
		if (label is not BindableObject bindable)
			return;
		var current = handler.PlatformView.Typeface;
		var weight = NativeText.GetWeight(bindable);
		if (weight != TextWeight.Regular && OperatingSystem.IsAndroidVersionAtLeast(28))
		{
			// The label's own family and italic style at the requested weight (Semibold = 600)
			var value = weight switch { TextWeight.Medium => 500, TextWeight.Semibold => 600, _ => 700 };
			handler.PlatformView.Typeface = Typeface.Create(current, value, current?.IsItalic == true);
			return;
		}
		switch (weight)
		{
			case TextWeight.Medium:
			case TextWeight.Semibold:
				handler.PlatformView.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
				break;
			case TextWeight.Bold:
				handler.PlatformView.SetTypeface(current, TypefaceStyle.Bold);
				break;
		}
	}
}
