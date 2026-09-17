using System.Collections.Specialized;
using Android.Content.Res;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Button;
using Google.Android.Material.Color;
using Google.Android.Material.Shape;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace NativeStyles;

/// <summary>
/// Material 3 Expressive connected button group drawn with MaterialButtons (Material Components 1.12 has no
/// MaterialButtonGroup): 40 dp tonal toggle buttons sharing the width, 2 dp apart, 8 dp inner corners, fully round outer
/// corners; the selected button is secondary / onSecondary and fully round.
/// </summary>
public class SegmentedControlHandler : ViewHandler<SegmentedControl, LinearLayout>
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

	protected override LinearLayout CreatePlatformView() => new(Context) { Orientation = Android.Widget.Orientation.Horizontal };

	// The buttons share the available width (weights), so the group is as wide as its container.
	public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
	{
		if (double.IsInfinity(widthConstraint) || Context is null)
			return base.GetDesiredSize(widthConstraint, heightConstraint);
		PlatformView.Measure(
			Android.Views.View.MeasureSpec.MakeMeasureSpec((int)Context.ToPixels(widthConstraint), MeasureSpecMode.Exactly),
			Android.Views.View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
		return new Size(widthConstraint, Context.FromPixels(PlatformView.MeasuredHeight));
	}

	protected override void DisconnectHandler(LinearLayout platformView)
	{
		Observe(null);
		base.DisconnectHandler(platformView);
	}

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
		var group = handler.PlatformView;
		var context = group.Context!;
		group.RemoveAllViews();
		for (var i = 0; i < view.Items.Count; i++)
		{
			var index = i;
			var button = new MaterialButton(context)
			{
				Text = view.Items[i],
				InsetTop = 0,
				InsetBottom = 0,
				StateListAnimator = null,
			};
			button.SetAllCaps(false);
			button.SetMaxLines(1);
			button.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
			button.SetMinimumHeight((int)context.ToPixels(40));
			button.SetMinHeight((int)context.ToPixels(40));
			button.SetPadding((int)context.ToPixels(16), 0, (int)context.ToPixels(16), 0);
			button.Click += (_, _) => view.SelectedIndex = index;
			var layout = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
			if (i > 0)
				layout.MarginStart = (int)context.ToPixels(2);
			group.AddView(button, layout);
		}
		MapSelectedIndex(handler, view);
		view.InvalidateMeasure();
	}

	static void MapSelectedIndex(SegmentedControlHandler handler, SegmentedControl view)
	{
		var group = handler.PlatformView;
		var context = group.Context!;
		var full = context.ToPixels(20);
		var inner = context.ToPixels(8);
		var rtl = group.LayoutDirection == Android.Views.LayoutDirection.Rtl;
		for (var i = 0; i < group.ChildCount; i++)
		{
			if (group.GetChildAt(i) is not MaterialButton button)
				continue;
			var selected = i == view.SelectedIndex;
			var container = selected ? Resource.Attribute.colorSecondary : Resource.Attribute.colorSecondaryContainer;
			var label = selected ? Resource.Attribute.colorOnSecondary : Resource.Attribute.colorOnSecondaryContainer;
			button.BackgroundTintList = ColorStateList.ValueOf(new Android.Graphics.Color(MaterialColors.GetColor(button, container)));
			button.SetTextColor(new Android.Graphics.Color(MaterialColors.GetColor(button, label)));

			var first = i == 0;
			var last = i == group.ChildCount - 1;
			var start = selected || first ? full : inner;
			var end = selected || last ? full : inner;
			var (left, right) = rtl ? (end, start) : (start, end);
			button.ShapeAppearanceModel = new ShapeAppearanceModel.Builder()
				.SetTopLeftCornerSize(left).SetBottomLeftCornerSize(left)
				.SetTopRightCornerSize(right).SetBottomRightCornerSize(right)
				.Build();
			button.Selected = selected;
		}
	}
}
