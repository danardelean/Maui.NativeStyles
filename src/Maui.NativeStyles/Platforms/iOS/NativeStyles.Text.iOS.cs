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
			handler.PlatformView.Font = WithWeight(current, w);
	}

	/// <summary>
	/// The same font (family, point size including Dynamic Type scaling, italic) at another weight. A system font is
	/// described by a usage attribute that honors the weight trait; a named font is pinned to its face by the name, so the
	/// face is looked up again in its family.
	/// </summary>
	static UIFont WithWeight(UIFont font, UIFontWeight weight)
	{
		var descriptor = font.FontDescriptor;
		// The traits attribute is replaced as a whole: carry the italic trait over
		var traits = new UIFontTraits
		{
			Weight = (float)weight.GetWeight(),
			SymbolicTrait = descriptor.SymbolicTraits & UIFontDescriptorSymbolicTraits.Italic,
		};
		if (descriptor.FontAttributes.Name is null)
			return UIFont.FromDescriptor(descriptor.CreateWithAttributes(new UIFontAttributes { Traits = traits }), 0) ?? font;

		var face = new UIFontDescriptor(new UIFontAttributes { Family = font.FamilyName, Traits = traits });
		// A family without that weight keeps its closest face; never fall back to another family
		return UIFont.FromDescriptor(face, font.PointSize) is { } weighted && weighted.FamilyName == font.FamilyName ? weighted : font;
	}
}
