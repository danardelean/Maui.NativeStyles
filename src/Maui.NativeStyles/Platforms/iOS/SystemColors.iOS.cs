using Microsoft.Maui.Platform;
using UIKit;

namespace NativeStyles;

public static partial class SystemColors
{
	static readonly UITraitCollection LightTraits = UITraitCollection.FromUserInterfaceStyle(UIUserInterfaceStyle.Light);
	static readonly UITraitCollection DarkTraits = UITraitCollection.FromUserInterfaceStyle(UIUserInterfaceStyle.Dark);

	/// <summary>UIColor dynamic system colors resolved for both appearances (they also follow Increased Contrast).</summary>
	static (Color Light, Color Dark)? ResolvePlatform(SystemColorRole role)
	{
		if (!OperatingSystem.IsIOSVersionAtLeast(13))
			return null;

		UIColor? color = role switch
		{
			SystemColorRole.Accent => UIColor.SystemBlue,
			SystemColorRole.OnAccent => UIColor.White,
			SystemColorRole.Destructive => UIColor.SystemRed,
			SystemColorRole.Success => UIColor.SystemGreen,
			SystemColorRole.Warning => UIColor.SystemOrange,
			SystemColorRole.TextPrimary => UIColor.Label,
			SystemColorRole.TextSecondary => UIColor.SecondaryLabel,
			SystemColorRole.TextTertiary => UIColor.TertiaryLabel,
			SystemColorRole.Placeholder => UIColor.PlaceholderText,
			SystemColorRole.Separator => UIColor.Separator,
			SystemColorRole.PageBackground => UIColor.SystemBackground,
			SystemColorRole.GroupedBackground => UIColor.SystemGroupedBackground,
			SystemColorRole.CardBackground => UIColor.SecondarySystemGroupedBackground,
			SystemColorRole.GroupContainer => UIColor.SecondarySystemGroupedBackground,
			SystemColorRole.Fill => UIColor.SystemFill,
			SystemColorRole.SecondaryFill => UIColor.TertiarySystemFill,
			SystemColorRole.TonalContainer => UIColor.SystemBlue.ColorWithAlpha(0.15f),
			SystemColorRole.OnTonalContainer => UIColor.SystemBlue,
			SystemColorRole.Gray => UIColor.SystemGray,
			_ => null,
		};
		if (color is null)
			return null;

		return (color.GetResolvedColor(LightTraits).ToColor()!, color.GetResolvedColor(DarkTraits).ToColor()!);
	}
}
