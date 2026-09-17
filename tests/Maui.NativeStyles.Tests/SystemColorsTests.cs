using NativeStyles;
using Xunit;

namespace NativeStyles.Tests;

public class SystemColorsTests
{
	public static IEnumerable<object[]> Roles() =>
		Enum.GetValues<SystemColorRole>().Select(r => new object[] { r });

	[Theory]
	[MemberData(nameof(Roles))]
	public void Every_role_has_a_fallback_pair(SystemColorRole role)
	{
		var (light, dark) = SystemColors.Fallback(role);

		Assert.NotNull(light);
		Assert.NotNull(dark);
	}

	[Theory]
	[MemberData(nameof(Roles))]
	public void Resolve_never_throws_off_device(SystemColorRole role)
	{
		var (light, dark) = SystemColors.Resolve(role);

		Assert.NotNull(light);
		Assert.NotNull(dark);
	}

	[Theory]
	[MemberData(nameof(Roles))]
	public void GetBinding_returns_a_binding(SystemColorRole role)
	{
		var binding = SystemColors.GetBinding(role);

		Assert.NotNull(binding);
		Assert.Equal("AppThemeBinding", binding.GetType().Name);
	}

	[Fact]
	public void Text_and_background_fallbacks_contrast_in_both_themes()
	{
		var (textLight, textDark) = SystemColors.Fallback(SystemColorRole.TextPrimary);
		var (pageLight, pageDark) = SystemColors.Fallback(SystemColorRole.PageBackground);

		Assert.True(Luminance(pageLight) > Luminance(textLight), "light: text must be darker than the page");
		Assert.True(Luminance(textDark) > Luminance(pageDark), "dark: text must be lighter than the page");
	}

	[Fact]
	public void Markup_extension_provides_the_binding_without_a_service_provider()
	{
		var extension = new SystemColorExtension { Role = SystemColorRole.Accent };

		var value = ((IMarkupExtension)extension).ProvideValue(null!);

		Assert.IsAssignableFrom<BindingBase>(value);
	}

	static float Luminance(Color c) => 0.2126f * c.Red + 0.7152f * c.Green + 0.0722f * c.Blue;
}
