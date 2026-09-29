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
	static void StyleSearchBar(IElementHandler handler, ISearchBar searchBar)
	{
		if (handler.PlatformView is not TextInputLayout layout || layout.Context is not { } context)
			return;
		var radius = context.ToPixels(28);
		layout.BoxBackgroundMode = TextInputLayout.BoxBackgroundFilled;
		layout.SetBoxCornerRadii(radius, radius, radius, radius);
		layout.BoxStrokeWidth = 0;
		layout.BoxStrokeWidthFocused = 0;
		layout.BoxBackgroundColor = MaterialColors.GetColor(layout, Resource.Attribute.colorSurfaceContainerHigh);
		layout.SetMinimumHeight((int)context.ToPixels(56));
	}

	/// <summary>Corner radius of a contained (Material 3 Expressive) text container, in dp.</summary>
	const float ContainedCornerRadius = 28;

	static void StyleEditorContainer(IElementHandler handler, IEditor editor)
	{
		if (handler.PlatformView is not Android.Widget.EditText field || editor is not BindableObject bindable || field.Context is not { } context)
			return;
		if (NativeEntry.GetIsPlain(bindable))
		{
			field.Background = null;
			field.SetPadding(0, field.PaddingTop, 0, field.PaddingBottom);
			return;
		}

		GradientDrawable Shape(float radiusDp, int fill, float strokeDp, int stroke)
		{
			var shape = new GradientDrawable();
			shape.SetShape(ShapeType.Rectangle);
			shape.SetCornerRadius(context.ToPixels(radiusDp));
			shape.SetColor(fill);
			if (strokeDp > 0)
				shape.SetStroke((int)context.ToPixels(strokeDp), new AColor(stroke));
			return shape;
		}

		var onSurface = MaterialColors.GetColor(field, Resource.Attribute.colorOnSurface);
		if (NativeEntry.GetIsContained(bindable))
		{
			field.Background = Shape(ContainedCornerRadius, MaterialColors.GetColor(field, Resource.Attribute.colorSurfaceContainerHighest), 0, 0);
		}
		else
		{
			// Outlined text field tokens: 4 dp corners, 1 dp outline, 2 dp primary when focused, onSurface 12% when disabled.
			var states = new StateListDrawable();
			states.AddState([-Android.Resource.Attribute.StateEnabled], Shape(4, AColor.Transparent, 1, MaterialColors.CompositeARGBWithAlpha(onSurface, 31)));
			states.AddState([Android.Resource.Attribute.StateFocused], Shape(4, AColor.Transparent, 2, MaterialColors.GetColor(field, Resource.Attribute.colorPrimary)));
			states.AddState([], Shape(4, AColor.Transparent, 1, MaterialColors.GetColor(field, Resource.Attribute.colorOutline)));
			field.Background = states;
		}
		var padding = (int)context.ToPixels(16);
		field.SetPadding(padding, padding, padding, padding);
	}

	/// <summary>No underline, secondary text color, trailing tinted icon (Material list value / exposed dropdown affordance).</summary>
	static void StylePickerField(IElementHandler handler, int iconResource)
	{
		if (handler.PlatformView is not Android.Widget.TextView field || handler.MauiContext?.Context is not { } context)
			return;
		field.Background = null;
		var tint = SystemColors.Get(SystemColorRole.TextSecondary).ToPlatform();
		var icon = AndroidX.Core.Content.ContextCompat.GetDrawable(context, iconResource)?.Mutate();
		if (icon is not null)
		{
			icon.SetTint(tint);
			var size = (int)context.ToPixels(20);
			icon.SetBounds(0, 0, size, size);
			field.SetCompoundDrawablesRelative(null, null, icon, null);
			field.CompoundDrawablePadding = (int)context.ToPixels(4);
		}
		field.SetPadding(0, field.PaddingTop, 0, field.PaddingBottom);
	}
}
