#if IOS
using System.Reflection;
using Microsoft.Maui.Platform;
using UIKit;

namespace NativeStyles.DeviceTests;

public class iOSButtonTests
{
	public static IEnumerable<object[]> Kinds() => Enum.GetValues<ButtonKind>().Select(kind => new object[] { kind });

	[Theory]
	[MemberData(nameof(Kinds))]
	public async Task Every_kind_is_a_capsule_button_configuration(ButtonKind kind)
	{
		var (hasConfiguration, corner, title) = await UI.Run(() =>
		{
			var button = new Button { Text = "Continue" };
			NativeButton.SetKind(button, kind);
			var configuration = UI.PlatformView<UIButton>(button).Configuration;
			return (configuration is not null, configuration?.CornerStyle, configuration?.Title);
		});

		Assert.True(hasConfiguration, "the button has no UIButtonConfiguration");
		Assert.Equal(UIButtonConfigurationCornerStyle.Capsule, corner);
		Assert.Equal("Continue", title);
	}

	[Theory]
	[InlineData(ControlSize.Small, UIButtonConfigurationSize.Small)]
	[InlineData(ControlSize.Default, UIButtonConfigurationSize.Medium)]
	[InlineData(ControlSize.Large, UIButtonConfigurationSize.Large)]
	public async Task Size_maps_to_the_configuration_size(ControlSize size, UIButtonConfigurationSize expected)
	{
		var actual = await UI.Run(() =>
		{
			var button = new Button { Text = "Continue" };
			NativeButton.SetKind(button, ButtonKind.Filled);
			NativeButton.SetSize(button, size);
			return UI.PlatformView<UIButton>(button).Configuration?.ButtonSize;
		});

		Assert.Equal(expected, actual);
	}

	[Fact]
	public async Task Destructive_text_button_uses_system_red()
	{
		var red = await UI.Run(() =>
		{
			var button = new Button { Text = "Remove" };
			NativeButton.SetKind(button, ButtonKind.Text);
			NativeButton.SetIsDestructive(button, true);
			return UI.PlatformView<UIButton>(button).Configuration?.BaseForegroundColor?.IsEqual(UIColor.SystemRed);
		});

		Assert.True(red);
	}

	[Fact]
	public async Task Title_font_transformer_does_not_write_into_the_attributes_it_receives()
	{
		var (mutated, hasFont) = await UI.Run(() =>
		{
			var button = new Button { Text = "Continue" };
			NativeButton.SetKind(button, ButtonKind.Filled);
			var transformer = UI.PlatformView<UIButton>(button).Configuration?.TitleTextAttributesTransformer
				?? throw new InvalidOperationException("no title attributes transformer");
			var input = new Foundation.NSMutableDictionary();
			var output = transformer(input);
			return (input.ContainsKey(UIStringAttributeKey.Font), output.ContainsKey(UIStringAttributeKey.Font));
		});

		// UIKit may hand the transformer an immutable (frozen) dictionary: writing into it throws an Objective-C
		// exception through managed frames and terminates the app (seen with the test runner's own buttons).
		Assert.False(mutated, "the transformer must return a new dictionary instead of setting the font on its input");
		Assert.True(hasFont);
	}

	[Fact]
	public async Task Changing_attached_properties_at_runtime_rebuilds_the_configuration()
	{
		var (redBefore, redAfter, size, corner) = await UI.Run(() =>
		{
			var button = new Button { Text = "Remove" };
			NativeButton.SetKind(button, ButtonKind.Text);
			var native = UI.PlatformView<UIButton>(button);
			var before = native.Configuration?.BaseForegroundColor?.IsEqual(UIColor.SystemRed) == true;
			// NativeStylesExtensions.Refresh re-runs the mapping on the live handler
			NativeButton.SetIsDestructive(button, true);
			NativeButton.SetSize(button, ControlSize.Large);
			var configuration = native.Configuration;
			return (before, configuration?.BaseForegroundColor?.IsEqual(UIColor.SystemRed) == true, configuration?.ButtonSize, configuration?.CornerStyle);
		});

		Assert.False(redBefore);
		Assert.True(redAfter, "destructive after the change");
		Assert.Equal(UIButtonConfigurationSize.Large, size);
		Assert.Equal(UIButtonConfigurationCornerStyle.Capsule, corner);
	}
}

public class iOSEntryTests
{
	[Fact]
	public async Task Entry_is_a_NativeTextField_with_the_grouped_shape()
	{
		var (type, border, radius, margin) = await UI.Run(() =>
		{
			var field = UI.PlatformView<UITextField>(new Entry());
			var native = field as NativeTextField;
			return (field.GetType(), field.BorderStyle, (double?)native?.CornerRadius, (double?)native?.ContentMargin);
		});

		Assert.Equal(typeof(NativeTextField), type);
		Assert.Equal(UITextBorderStyle.None, border);
		Assert.Equal(26, radius);
		Assert.Equal(20, margin);
	}

	[Theory]
	[InlineData(52, 26)] // the standard row height: a capsule
	[InlineData(40, 20)] // shorter fields stay capsules
	[InlineData(120, 26)]
	public async Task Layer_radius_is_capped_at_half_the_height(double height, double expected)
	{
		var radius = await UI.Run(() =>
		{
			var field = UI.PlatformView<UITextField>(new Entry());
			field.Frame = new CoreGraphics.CGRect(0, 0, 300, height);
			field.LayoutIfNeeded();
			return (double)field.Layer.CornerRadius;
		});

		Assert.Equal(expected, radius, 3);
	}

	[Fact]
	public async Task Plain_entry_has_no_shape()
	{
		var (radius, margin) = await UI.Run(() =>
		{
			var entry = new Entry();
			NativeEntry.SetIsPlain(entry, true);
			var native = (NativeTextField)UI.PlatformView<UITextField>(entry);
			return ((double)native.CornerRadius, (double)native.ContentMargin);
		});

		Assert.Equal(0, radius);
		Assert.Equal(0, margin);
	}
}

public class iOSSegmentedControlTests
{
	[Fact]
	public async Task One_native_segment_per_item_and_the_selection_is_kept()
	{
		var (count, titles, selected) = await UI.Run(() =>
		{
			var control = new SegmentedControl { Items = { "Day", "Week", "Month" }, SelectedIndex = 1 };
			var native = UI.PlatformView<UISegmentedControl>(control);
			control.Items.Add("Year"); // observable items: the control is rebuilt
			var n = (int)native.NumberOfSegments;
			return (n, Enumerable.Range(0, n).Select(i => native.TitleAt(i) ?? "").ToArray(), (int)native.SelectedSegment);
		});

		Assert.Equal(4, count);
		Assert.Equal(["Day", "Week", "Month", "Year"], titles);
		Assert.Equal(1, selected);
	}

	[Fact]
	public async Task Native_selection_updates_the_view()
	{
		var index = await UI.Run(() =>
		{
			var control = new SegmentedControl { Items = { "Day", "Week", "Month" } };
			var native = UI.PlatformView<UISegmentedControl>(control);
			native.SelectedSegment = 2;
			native.SendActionForControlEvents(UIControlEvent.ValueChanged);
			return control.SelectedIndex;
		});

		Assert.Equal(2, index);
	}
}

public class iOSSystemColorTests
{
	static readonly UITraitCollection Light = UITraitCollection.FromUserInterfaceStyle(UIUserInterfaceStyle.Light);
	static readonly UITraitCollection Dark = UITraitCollection.FromUserInterfaceStyle(UIUserInterfaceStyle.Dark);

	// The documented mapping (SystemColorRole XML docs), written out independently of the library
	static UIColor Expected(SystemColorRole role) => role switch
	{
		SystemColorRole.Accent or SystemColorRole.OnTonalContainer => UIColor.SystemBlue,
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
		SystemColorRole.CardBackground or SystemColorRole.GroupContainer => UIColor.SecondarySystemGroupedBackground,
		SystemColorRole.Fill => UIColor.SystemFill,
		SystemColorRole.SecondaryFill => UIColor.TertiarySystemFill,
		SystemColorRole.TonalContainer => UIColor.SystemBlue.ColorWithAlpha(0.15f),
		SystemColorRole.Gray => UIColor.SystemGray,
		_ => throw new ArgumentOutOfRangeException(nameof(role), role, "new role: add it to the test"),
	};

	[Theory]
	[MemberData(nameof(UI.Roles), MemberType = typeof(UI))]
	public async Task Resolves_the_UIKit_system_color_for_both_appearances(SystemColorRole role)
	{
		Assert.Null(SystemColors.Brand);

		var (expected, actual) = await UI.Run(() =>
		{
			var color = Expected(role);
			return ((color.GetResolvedColor(Light).ToColor(), color.GetResolvedColor(Dark).ToColor()), SystemColors.Resolve(role));
		});

		Assert.Equal(expected, actual);
	}

	[Theory]
	[MemberData(nameof(UI.Roles), MemberType = typeof(UI))]
	public async Task The_platform_answers_instead_of_the_static_fallback(SystemColorRole role)
	{
		var resolvePlatform = typeof(SystemColors).GetMethod("ResolvePlatform", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(resolvePlatform);

		var platform = await UI.Run(() => resolvePlatform.Invoke(null, [role]));

		Assert.NotNull(platform);
	}
}
#endif
