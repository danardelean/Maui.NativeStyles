using System.Collections.Specialized;
using Microsoft.Maui.Handlers;
using UIKit;

namespace NativeStyles;

public class SegmentedControlHandler : ViewHandler<SegmentedControl, UISegmentedControl>
{
	public static readonly IPropertyMapper<SegmentedControl, SegmentedControlHandler> Mapper =
		new PropertyMapper<SegmentedControl, SegmentedControlHandler>(ViewMapper)
		{
			[nameof(SegmentedControl.Items)] = MapItems,
			[nameof(SegmentedControl.SelectedIndex)] = MapSelectedIndex,
		};

	INotifyCollectionChanged? _observed;

	public SegmentedControlHandler() : base(Mapper)
	{
	}

	protected override UISegmentedControl CreatePlatformView() => new();

	protected override void ConnectHandler(UISegmentedControl platformView)
	{
		base.ConnectHandler(platformView);
		platformView.ValueChanged += OnValueChanged;
	}

	protected override void DisconnectHandler(UISegmentedControl platformView)
	{
		platformView.ValueChanged -= OnValueChanged;
		Observe(null);
		base.DisconnectHandler(platformView);
	}

	void OnValueChanged(object? sender, EventArgs e) => VirtualView.SelectedIndex = (int)PlatformView.SelectedSegment;

	void Observe(INotifyCollectionChanged? collection)
	{
		if (_observed is not null)
			_observed.CollectionChanged -= OnItemsChanged;
		_observed = collection;
		if (_observed is not null)
			_observed.CollectionChanged += OnItemsChanged;
	}

	void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => MapItems(this, VirtualView);

	static void MapItems(SegmentedControlHandler handler, SegmentedControl view)
	{
		handler.Observe(view.Items as INotifyCollectionChanged);
		var control = handler.PlatformView;
		control.RemoveAllSegments();
		for (var i = 0; i < view.Items.Count; i++)
			control.InsertSegment(view.Items[i], i, false);
		MapSelectedIndex(handler, view);
		view.InvalidateMeasure();
	}

	static void MapSelectedIndex(SegmentedControlHandler handler, SegmentedControl view)
	{
		if (view.SelectedIndex >= 0 && view.SelectedIndex < handler.PlatformView.NumberOfSegments)
			handler.PlatformView.SelectedSegment = view.SelectedIndex;
	}
}
