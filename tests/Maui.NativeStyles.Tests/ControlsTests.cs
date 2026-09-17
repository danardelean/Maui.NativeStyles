using NativeStyles;
using Xunit;

namespace NativeStyles.Tests;

public class ControlsTests
{
	[Fact]
	public void SegmentedControl_raises_SelectionChanged_with_old_and_new_index()
	{
		var control = new SegmentedControl { Items = { "Day", "Week", "Month" } };
		SelectedIndexChangedEventArgs? args = null;
		control.SelectionChanged += (_, e) => args = e;

		control.SelectedIndex = 2;

		Assert.Equal(3, control.Items.Count);
		Assert.NotNull(args);
		Assert.Equal(0, args.OldIndex);
		Assert.Equal(2, args.NewIndex);
	}

	[Fact]
	public void SegmentedControl_instances_do_not_share_items()
	{
		var first = new SegmentedControl { Items = { "A" } };
		var second = new SegmentedControl();

		Assert.Single(first.Items);
		Assert.Empty(second.Items);
	}

	[Fact]
	public void NativeImage_tint_is_null_by_default_and_settable()
	{
		var image = new Image();
		Assert.Null(NativeImage.GetTintColor(image));

		NativeImage.SetTintColor(image, Colors.Red);

		Assert.Equal(Colors.Red, NativeImage.GetTintColor(image));
	}
}
