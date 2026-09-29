using NativeStyles;
using Xunit;

namespace NativeStyles.Tests;

// The trigger colors resolve through the static SystemColors.Brand
[Collection("SystemColors")]
public class DestructiveButtonsTests : IDisposable
{
	static readonly Color StyleText = Colors.White;
	static readonly Color StyleBackground = Colors.DarkRed;

	public DestructiveButtonsTests() => SystemColors.Brand = null;

	public void Dispose() => SystemColors.Brand = null;

	// Like the Android Destructive style class: an error-filled container
	static Button Destructive(ButtonKind kind)
	{
		var button = new Button
		{
			Style = new Style(typeof(Button))
			{
				Setters =
				{
					new Setter { Property = Button.TextColorProperty, Value = StyleText },
					new Setter { Property = VisualElement.BackgroundColorProperty, Value = StyleBackground },
				},
			},
		};
		NativeButton.SetKind(button, kind);
		NativeButton.SetIsDestructive(button, true);
		return button;
	}

	static Color Error => SystemColors.Resolve(SystemColorRole.Destructive).Light;

	[Fact]
	public void A_destructive_text_button_gets_an_error_label_on_a_transparent_container()
	{
		var button = Destructive(ButtonKind.Text);

		DestructiveButtons.Attach(button);

		Assert.Equal(Error, button.TextColor);
		Assert.Equal(Colors.Transparent, button.BackgroundColor);
		Assert.Null(button.BorderColor);
	}

	[Fact]
	public void A_destructive_outlined_button_also_gets_an_error_outline()
	{
		var button = Destructive(ButtonKind.Outlined);

		DestructiveButtons.Attach(button);

		Assert.Equal(Error, button.TextColor);
		Assert.Equal(Error, button.BorderColor);
	}

	[Fact]
	public void The_error_color_follows_the_brand()
	{
		SystemColors.Brand = new BrandPalette(Colors.Teal).Set(SystemColorRole.Destructive, Colors.Crimson);
		var button = Destructive(ButtonKind.Text);

		DestructiveButtons.Attach(button);

		Assert.Equal(Colors.Crimson, button.TextColor);
	}

	[Fact]
	public void Filled_destructive_buttons_keep_the_style_container()
	{
		var button = Destructive(ButtonKind.Filled);

		DestructiveButtons.Attach(button);

		Assert.Equal(StyleText, button.TextColor);
		Assert.Equal(StyleBackground, button.BackgroundColor);
	}

	[Fact]
	public void Turning_destructive_off_restores_what_the_app_and_the_style_set()
	{
		var button = Destructive(ButtonKind.Outlined);
		button.TextColor = Colors.Blue;
		DestructiveButtons.Attach(button);

		NativeButton.SetIsDestructive(button, false);

		Assert.Equal(Colors.Blue, button.TextColor);
		Assert.Equal(StyleBackground, button.BackgroundColor);
		Assert.Null(button.BorderColor);
	}

	[Fact]
	public void Changing_the_kind_withdraws_the_outline()
	{
		var button = Destructive(ButtonKind.Outlined);
		button.BorderColor = Colors.Green;
		DestructiveButtons.Attach(button);

		NativeButton.SetKind(button, ButtonKind.Text);

		Assert.Equal(Colors.Green, button.BorderColor);
		Assert.Equal(Error, button.TextColor);
	}

	[Fact]
	public void Attaching_twice_adds_the_triggers_once()
	{
		var button = Destructive(ButtonKind.Text);

		DestructiveButtons.Attach(button);
		DestructiveButtons.Attach(button);

		Assert.Equal(2, button.Triggers.Count);
	}
}
