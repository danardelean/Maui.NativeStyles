using System.ComponentModel;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace NativeStyles;

public static partial class NativeStylesExtensions
{
	static void MapImageButtonTint(IImageButtonHandler handler, IImageButton button)
	{
		if (button is not BindableObject bindable || NativeImage.GetTintColor(bindable) is not { } tint)
			return;
		handler.PlatformView.TintColor = tint.ToPlatform();
		RenderAsTemplate(bindable, handler.PlatformView);
	}

	static void MapImageTint(IImageHandler handler, Microsoft.Maui.IImage image)
	{
		if (image is not BindableObject bindable)
			return;
		var tint = NativeImage.GetTintColor(bindable);
		handler.PlatformView.TintColor = tint?.ToPlatform();
		if (tint is not null)
			RenderAsTemplate(bindable, handler.PlatformView);
	}

	static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BindableObject, object> s_templateImages = new();

	/// <summary>
	/// Switches the image of an Image / ImageButton to template rendering, now and whenever a new source finishes
	/// loading: MAUI assigns the platform image asynchronously, when IsLoading goes back to false.
	/// </summary>
	static void RenderAsTemplate(BindableObject element, UIView platformView)
	{
		ApplyTemplateImage(platformView);
		if (s_templateImages.TryGetValue(element, out _))
			return;
		s_templateImages.Add(element, element);
		element.PropertyChanged += OnTemplateImagePropertyChanged;
	}

	static void OnTemplateImagePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		// Image.IsLoading and ImageButton.IsLoading share the property name
		if (e.PropertyName == nameof(Image.IsLoading) && sender is Image { IsLoading: false } or ImageButton { IsLoading: false }
			&& sender is Element { Handler.PlatformView: UIView platformView } element && NativeImage.GetTintColor(element) is not null)
		{
			ApplyTemplateImage(platformView);
		}
	}

	static void ApplyTemplateImage(UIView platformView)
	{
		switch (platformView)
		{
			case UIImageView view when view.Image is { RenderingMode: not UIImageRenderingMode.AlwaysTemplate } original:
				view.Image = original.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate);
				break;
			case UIButton button when button.ImageForState(UIControlState.Normal) is { RenderingMode: not UIImageRenderingMode.AlwaysTemplate } original:
				button.SetImage(original.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate), UIControlState.Normal);
				break;
		}
	}
}
