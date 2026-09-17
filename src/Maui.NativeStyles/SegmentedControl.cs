using System.Collections.ObjectModel;

namespace NativeStyles;

/// <summary>
/// Single choice among a few options. iOS: UISegmentedControl (Liquid Glass on iOS 26). Android: Material 3 Expressive
/// connected button group (tonal toggle buttons, 8 dp inner corners, the selected button becomes fully round);
/// baseline segmented buttons are no longer recommended by Material.
/// </summary>
[ContentProperty(nameof(Items))]
public class SegmentedControl : View
{
	public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
		nameof(Items), typeof(IList<string>), typeof(SegmentedControl), null,
		defaultValueCreator: _ => new ObservableCollection<string>());

	public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
		nameof(SelectedIndex), typeof(int), typeof(SegmentedControl), 0, BindingMode.TwoWay,
		propertyChanged: (b, o, n) => ((SegmentedControl)b).SelectionChanged?.Invoke(b, new SelectedIndexChangedEventArgs((int)o, (int)n)));

	/// <summary>Segment titles.</summary>
	public IList<string> Items
	{
		get => (IList<string>)GetValue(ItemsProperty);
		set => SetValue(ItemsProperty, value);
	}

	public int SelectedIndex
	{
		get => (int)GetValue(SelectedIndexProperty);
		set => SetValue(SelectedIndexProperty, value);
	}

	public event EventHandler<SelectedIndexChangedEventArgs>? SelectionChanged;
}

public sealed class SelectedIndexChangedEventArgs(int oldIndex, int newIndex) : EventArgs
{
	public int OldIndex { get; } = oldIndex;
	public int NewIndex { get; } = newIndex;
}
