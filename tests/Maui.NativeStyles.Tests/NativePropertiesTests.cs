using NativeStyles;
using Xunit;

namespace NativeStyles.Tests;

public class NativePropertiesTests
{
	[Fact]
	public void Button_defaults_are_platform_automatic()
	{
		var button = new Button();

		Assert.Equal(ButtonKind.Automatic, NativeButton.GetKind(button));
		Assert.False(NativeButton.GetIsDestructive(button));
		Assert.Equal(ControlSize.Default, NativeButton.GetSize(button));
	}

	[Fact]
	public void Attached_properties_round_trip()
	{
		var button = new Button();
		var label = new Label();
		var entry = new Entry();
		var shell = new Shell();

		NativeButton.SetKind(button, ButtonKind.GlassProminent);
		NativeButton.SetIsDestructive(button, true);
		NativeButton.SetSize(button, ControlSize.Large);
		NativeText.SetWeight(label, TextWeight.Semibold);
		NativeEntry.SetIsPlain(entry, true);
		NativeShell.SetTabBarMinimizeBehavior(shell, TabBarMinimizeBehavior.OnScrollDown);

		Assert.Equal(ButtonKind.GlassProminent, NativeButton.GetKind(button));
		Assert.True(NativeButton.GetIsDestructive(button));
		Assert.Equal(ControlSize.Large, NativeButton.GetSize(button));
		Assert.Equal(TextWeight.Semibold, NativeText.GetWeight(label));
		Assert.True(NativeEntry.GetIsPlain(entry));
		Assert.Equal(TabBarMinimizeBehavior.OnScrollDown, NativeShell.GetTabBarMinimizeBehavior(shell));
	}

	[Fact]
	public void Changing_a_property_without_a_handler_does_not_throw()
	{
		var button = new Button();

		var exception = Record.Exception(() => NativeButton.SetKind(button, ButtonKind.Filled));

		Assert.Null(exception);
	}

	[Theory]
	[InlineData(NativeStylesExtensions.Classes.Filled, nameof(ButtonKind.Filled))]
	[InlineData(NativeStylesExtensions.Classes.Tonal, nameof(ButtonKind.Tonal))]
	[InlineData(NativeStylesExtensions.Classes.Outlined, nameof(ButtonKind.Outlined))]
	[InlineData(NativeStylesExtensions.Classes.Text, nameof(ButtonKind.Text))]
	[InlineData(NativeStylesExtensions.Classes.Glass, nameof(ButtonKind.Glass))]
	[InlineData(NativeStylesExtensions.Classes.GlassProminent, nameof(ButtonKind.GlassProminent))]
	public void StyleClass_names_match_button_kinds(string styleClass, string kind)
	{
		Assert.Equal(kind, styleClass);
		Assert.True(Enum.TryParse<ButtonKind>(styleClass, out _));
	}

	[Fact]
	public void Glass_view_defaults_to_a_capsule()
	{
		var view = new GlassView();

		Assert.Equal(GlassStyle.Regular, view.GlassStyle);
		Assert.Equal(-1d, view.CornerRadius);
		Assert.True(view.IsInteractive);
		Assert.Null(view.TintColor);
	}
}
