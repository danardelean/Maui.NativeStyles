namespace NativeStyles;

/// <summary>WCAG 2 contrast math, used to pick readable text colors for brand-colored containers.</summary>
internal static class ColorContrast
{
	/// <summary>
	/// Minimum ratio for large text and UI components (WCAG 2 AA). Button titles are large, semibold text; white falls
	/// below 3:1 about where Material switches to a dark foreground (tone 60).
	/// </summary>
	public const double LargeTextMinimum = 3;

	/// <summary>Relative luminance of the sRGB color (0 = black, 1 = white); alpha is ignored.</summary>
	public static double RelativeLuminance(Color color) =>
		0.2126 * Linear(color.Red) + 0.7152 * Linear(color.Green) + 0.0722 * Linear(color.Blue);

	/// <summary>Contrast ratio between two colors, from 1 (same luminance) to 21 (black on white).</summary>
	public static double Ratio(Color a, Color b)
	{
		var (la, lb) = (RelativeLuminance(a), RelativeLuminance(b));
		return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
	}

	/// <summary>The candidate with the higher contrast against <paramref name="background"/> (the first one on a tie).</summary>
	public static Color HigherContrast(Color background, Color first, Color second) =>
		Ratio(background, first) >= Ratio(background, second) ? first : second;

	/// <summary>
	/// Text color for a container filled with <paramref name="background"/>: white, what both platforms draw on their
	/// accent color, while it keeps <see cref="LargeTextMinimum"/>; otherwise whichever of white and black contrasts more.
	/// Picking the higher contrast unconditionally would turn mid-tone accents such as iOS systemBlue to black titles,
	/// as WCAG 2 luminance favors black on them.
	/// </summary>
	public static Color OnColor(Color background) =>
		Ratio(background, Colors.White) >= LargeTextMinimum ? Colors.White : HigherContrast(background, Colors.Black, Colors.White);

	static double Linear(float channel) =>
		channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
