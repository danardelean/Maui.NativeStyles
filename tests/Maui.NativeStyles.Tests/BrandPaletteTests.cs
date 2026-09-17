using NativeStyles;
using Xunit;

namespace NativeStyles.Tests;

[Collection("SystemColors")]
public class BrandPaletteTests : IDisposable
{
	static readonly Color Brand = Color.FromArgb("#0B7A75");
	static readonly Color BrandDark = Color.FromArgb("#3FC1B4");

	public void Dispose() => SystemColors.Brand = null;

	[Fact]
	public void Without_a_brand_the_platform_baseline_is_used()
	{
		SystemColors.Brand = null;

		Assert.Equal(SystemColors.Fallback(SystemColorRole.Accent), SystemColors.Resolve(SystemColorRole.Accent));
	}

	[Fact]
	public void The_accent_family_follows_the_brand_color()
	{
		SystemColors.Brand = new BrandPalette(Brand, BrandDark);

		Assert.Equal((Brand, BrandDark), SystemColors.Resolve(SystemColorRole.Accent));
		Assert.Equal((Brand, BrandDark), SystemColors.Resolve(SystemColorRole.OnTonalContainer));
		var tonal = SystemColors.Resolve(SystemColorRole.TonalContainer);
		Assert.Equal(0.15f, tonal.Light.Alpha, 2);
		Assert.Equal(Brand.Red, tonal.Light.Red, 3);
	}

	[Fact]
	public void The_dark_accent_defaults_to_the_light_one()
	{
		SystemColors.Brand = new BrandPalette(Brand);

		Assert.Equal((Brand, Brand), SystemColors.Resolve(SystemColorRole.Accent));
	}

	[Fact]
	public void Roles_without_a_brand_meaning_keep_the_baseline()
	{
		SystemColors.Brand = new BrandPalette(Brand);

		Assert.Equal(SystemColors.Fallback(SystemColorRole.TextPrimary), SystemColors.Resolve(SystemColorRole.TextPrimary));
		Assert.Equal(SystemColors.Fallback(SystemColorRole.Destructive), SystemColors.Resolve(SystemColorRole.Destructive));
	}

	[Fact]
	public void A_pinned_role_wins_over_the_brand_and_the_platform()
	{
		SystemColors.Brand = new BrandPalette(Brand)
			.Set(SystemColorRole.Accent, Colors.Orange, Colors.Yellow)
			.Set(SystemColorRole.Destructive, Colors.Crimson);

		Assert.Equal((Colors.Orange, Colors.Yellow), SystemColors.Resolve(SystemColorRole.Accent));
		Assert.Equal((Colors.Crimson, Colors.Crimson), SystemColors.Resolve(SystemColorRole.Destructive));
	}

	[Fact]
	public void UseNativeStyles_installs_the_brand()
	{
		MauiApp.CreateBuilder().UseNativeStyles(options => options.Brand = new BrandPalette(Brand, BrandDark));

		Assert.NotNull(SystemColors.Brand);
		Assert.Equal((Brand, BrandDark), SystemColors.Resolve(SystemColorRole.Accent));
	}
}
