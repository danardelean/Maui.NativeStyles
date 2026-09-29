using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace NativeStyles;

public static partial class NativeStylesExtensions
{
	static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageButton, object> s_tintedButtons = new();

	static void MapImageButtonTint(IImageButtonHandler handler, IImageButton button)
	{
		if (button is not BindableObject bindable)
			return;
		var view = handler.PlatformView;
		var tint = NativeImage.GetTintColor(bindable);
		if (tint is null)
			return;
		view.TintColor = tint.ToPlatform();
		ApplyTemplate(view);

		// MAUI assigns the button image when the source finishes loading (IsLoading goes back to false)
		if (button is ImageButton element && !s_tintedButtons.TryGetValue(element, out _))
		{
			s_tintedButtons.Add(element, element);
			element.PropertyChanged += (sender, e) =>
			{
				if (e.PropertyName == ImageButton.IsLoadingProperty.PropertyName && sender is ImageButton { IsLoading: false, Handler: IImageButtonHandler current }
					&& NativeImage.GetTintColor((ImageButton)sender) is not null)
				{
					ApplyTemplate(current.PlatformView);
				}
			};
		}

		static void ApplyTemplate(UIButton target)
		{
			if (target.ImageForState(UIControlState.Normal) is { RenderingMode: not UIImageRenderingMode.AlwaysTemplate } original)
				target.SetImage(original.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate), UIControlState.Normal);
		}
	}

	static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Image, object> s_tintedImages = new();

	static void MapImageTint(IImageHandler handler, Microsoft.Maui.IImage image)
	{
		if (image is not BindableObject bindable)
			return;
		var view = handler.PlatformView;
		var tint = NativeImage.GetTintColor(bindable);
		view.TintColor = tint?.ToPlatform();
		if (tint is null)
			return;
		ApplyTemplate(view);

		// MAUI assigns UIImageView.Image when the source finishes loading (IsLoading goes back to false)
		if (image is Image element && !s_tintedImages.TryGetValue(element, out _))
		{
			s_tintedImages.Add(element, element);
			element.PropertyChanged += (sender, e) =>
			{
				if (e.PropertyName == Image.IsLoadingProperty.PropertyName && sender is Image { IsLoading: false, Handler: IImageHandler current }
					&& NativeImage.GetTintColor((Image)sender) is not null)
				{
					ApplyTemplate(current.PlatformView);
				}
			};
		}

		static void ApplyTemplate(UIImageView target)
		{
			if (target.Image is { RenderingMode: not UIImageRenderingMode.AlwaysTemplate } original)
				target.Image = original.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
		}
	}
}
