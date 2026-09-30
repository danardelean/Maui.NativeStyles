using NativeStyles;
using Xunit;

namespace NativeStyles.Tests;

public class ColorContrastTests
{
	[Fact]
	public void Luminance_spans_black_to_white()
	{
		Assert.Equal(0, ColorContrast.RelativeLuminance(Colors.Black), 6);
		Assert.Equal(1, ColorContrast.RelativeLuminance(Colors.White), 6);
	}

	[Fact]
	public void Ratio_matches_the_WCAG_reference_values()
	{
		Assert.Equal(21, ColorContrast.Ratio(Colors.Black, Colors.White), 6);
		Assert.Equal(1, ColorContrast.Ratio(Colors.Teal, Colors.Teal), 6);
		// #777777 on white is the classic "just below 4.5:1" gray
		Assert.Equal(4.48, ColorContrast.Ratio(Color.FromArgb("#777777"), Colors.White), 2);
	}

	[Fact]
	public void Ratio_is_symmetric()
	{
		var a = Color.FromArgb("#0B7A75");
		var b = Color.FromArgb("#FFCC00");

		Assert.Equal(ColorContrast.Ratio(a, b), ColorContrast.Ratio(b, a), 9);
	}

	[Fact]
	public void HigherContrast_picks_the_more_readable_candidate()
	{
		Assert.Equal(Colors.Black, ColorContrast.HigherContrast(Colors.Yellow, Colors.White, Colors.Black));
		Assert.Equal(Colors.White, ColorContrast.HigherContrast(Colors.Navy, Colors.Black, Colors.White));
	}

	[Theory]
	[InlineData("#0B7A75")] // sample brand (light)
	[InlineData("#0088FF")] // iOS 26 systemBlue: WCAG 2 alone would prefer black here
	[InlineData("#007AFF")]
	[InlineData("#6750A4")] // Material baseline primary
	[InlineData("#B3261E")]
	[InlineData("#000000")]
	public void Accents_that_keep_white_readable_keep_white(string accent)
	{
		Assert.Equal(Colors.White, ColorContrast.OnColor(Color.FromArgb(accent)));
	}

	[Theory]
	[InlineData("#3FC1B4")] // sample brand (dark): white would be 2.2:1
	[InlineData("#FFCC00")]
	[InlineData("#34C759")]
	[InlineData("#D0BCFF")] // Material baseline primary (dark)
	[InlineData("#FFFFFF")]
	public void Light_accents_get_black(string accent)
	{
		var background = Color.FromArgb(accent);

		Assert.Equal(Colors.Black, ColorContrast.OnColor(background));
		Assert.True(ColorContrast.Ratio(background, Colors.Black) >= ColorContrast.LargeTextMinimum);
	}
}
