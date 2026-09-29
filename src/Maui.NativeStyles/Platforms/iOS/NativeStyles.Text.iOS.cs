using Microsoft.Maui.Handlers;
using UIKit;

namespace NativeStyles;

public static partial class NativeStylesExtensions
{
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
}
