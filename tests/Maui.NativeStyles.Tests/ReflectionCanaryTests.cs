using System.Collections;
using System.Reflection;
using Xunit;

namespace NativeStyles.Tests;

/// <summary>
/// The library reaches a few MAUI internals through reflection. A MAUI update that renames or reshapes them would not
/// break the build, only degrade behavior at runtime, so these tests pin the exact shapes the library relies on.
/// A failure here means the reflection code (and the matching MAUI version note in the README) needs revisiting.
/// The Android-only targets (EntryHandler2 and friends) are covered by tests/Maui.NativeStyles.DeviceTests.
/// </summary>
public class ReflectionCanaryTests
{
	// Same lookup as SystemColors.AppThemeBindingFactory
	static Type? AppThemeBindingType => typeof(BindingBase).Assembly.GetType("Microsoft.Maui.Controls.AppThemeBinding");

	[Fact]
	public void AppThemeBinding_exists_in_the_Controls_assembly()
	{
		var type = AppThemeBindingType;

		Assert.NotNull(type);
		Assert.True(typeof(BindingBase).IsAssignableFrom(type), "AppThemeBinding must still derive from BindingBase");
		Assert.False(type.IsAbstract);
	}

	[Fact]
	public void AppThemeBinding_has_a_public_parameterless_constructor()
	{
		var constructor = AppThemeBindingType?.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);

		Assert.NotNull(constructor);
	}

	[Theory]
	[InlineData("Light")]
	[InlineData("Dark")]
	public void AppThemeBinding_has_a_settable_theme_property(string name)
	{
		// GetProperty(name) without binding flags is what the library uses: public instance or static
		var property = AppThemeBindingType?.GetProperty(name);

		Assert.NotNull(property);
		Assert.True(property.CanWrite, $"{name} must be settable");
		Assert.NotNull(property.GetSetMethod());
		Assert.True(property.PropertyType.IsAssignableFrom(typeof(Color)), $"{name} must accept a Color");
	}

	[Fact]
	public void AppThemeBinding_round_trips_light_and_dark_values()
	{
		var type = AppThemeBindingType!;
		var binding = Activator.CreateInstance(type)!;

		type.GetProperty("Light")!.SetValue(binding, Colors.Red);
		type.GetProperty("Dark")!.SetValue(binding, Colors.Blue);

		Assert.Equal(Colors.Red, type.GetProperty("Light")!.GetValue(binding));
		Assert.Equal(Colors.Blue, type.GetProperty("Dark")!.GetValue(binding));
	}

	// Same lookup as ShellChromeStyler (Android), which clears the list on a replaced Shell
	static FieldInfo? AppearanceObserversField =>
		typeof(Shell).GetField("_appearanceObservers", BindingFlags.Instance | BindingFlags.NonPublic);

	[Fact]
	public void Shell_appearance_observers_field_exists()
	{
		Assert.NotNull(AppearanceObserversField);
	}

	[Fact]
	public void Shell_appearance_observers_is_a_clearable_list_on_a_new_shell()
	{
		var observers = AppearanceObserversField?.GetValue(new Shell());

		var list = Assert.IsAssignableFrom<IList>(observers);
		Assert.False(list.IsReadOnly);
		list.Clear();
	}
}
