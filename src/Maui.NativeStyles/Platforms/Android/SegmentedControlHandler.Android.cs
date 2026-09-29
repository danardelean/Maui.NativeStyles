using System.Collections.Specialized;
using Android.Content;
using Android.Content.Res;
using Android.Views;
using Android.Views.Accessibility;
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
/// corners; the selected button is secondary / onSecondary and fully round. Each button is a 48 dp touch target.
/// </summary>
public class SegmentedControlHandler : ViewHandler<SegmentedControl, LinearLayout>
{
	public static readonly IPropertyMapper<SegmentedControl, SegmentedControlHandler> Mapper =
		new PropertyMapper<SegmentedControl, SegmentedControlHandler>(ViewMapper)
		{
			[nameof(SegmentedControl.Items)] = MapItems,
			[nameof(SegmentedControl.SelectedIndex)] = MapSelectedIndex,
		};

	// The buttons are 48 dp touch targets whose background is inset to the 40 dp shape (MaterialButton insets). The
	// control measures as the visible 40 dp group and its view extends TouchInset above and below that frame.
	const double ButtonHeight = 40;
	const double TouchInset = (48 - ButtonHeight) / 2;

	INotifyCollectionChanged? _observed;

	public SegmentedControlHandler() : base(Mapper)
	{
	}

	protected override LinearLayout CreatePlatformView() => new SegmentGroup(Context) { Orientation = Android.Widget.Orientation.Horizontal };

	// The buttons share the available width (weights), so the group is as wide as its container.
	public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
	{
		Size size;
		if (double.IsInfinity(widthConstraint) || Context is null)
			size = base.GetDesiredSize(widthConstraint, heightConstraint);
		else
		{
			PlatformView.Measure(
				Android.Views.View.MeasureSpec.MakeMeasureSpec((int)Context.ToPixels(widthConstraint), MeasureSpecMode.Exactly),
				Android.Views.View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
			size = new Size(widthConstraint, Context.FromPixels(PlatformView.MeasuredHeight));
		}
		return new Size(size.Width, Math.Max(0, size.Height - 2 * TouchInset));
	}

	public override void PlatformArrange(Rect frame) =>
		base.PlatformArrange(new Rect(frame.X, frame.Y - TouchInset, frame.Width, frame.Height + 2 * TouchInset));

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
		var inset = (int)context.ToPixels(TouchInset);
		var touchHeight = (int)context.ToPixels(ButtonHeight + 2 * TouchInset);
		group.RemoveAllViews();
		for (var i = 0; i < view.Items.Count; i++)
		{
			var index = i;
			var button = new SegmentButton(context)
			{
				Text = view.Items[i],
				InsetTop = inset,
				InsetBottom = inset,
				StateListAnimator = null,
			};
			button.SetAllCaps(false);
			button.SetMaxLines(1);
			button.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
			button.SetMinimumHeight(touchHeight);
			button.SetMinHeight(touchHeight);
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

	/// <summary>
	/// TalkBack: a single-selection collection of one row, as MaterialButtonToggleGroup reports itself, so the position of
	/// each button is announced.
	/// </summary>
	sealed class SegmentGroup(Context? context) : LinearLayout(context)
	{
		public override void OnInitializeAccessibilityNodeInfo(AccessibilityNodeInfo? info)
		{
			base.OnInitializeAccessibilityNodeInfo(info);
			info?.SetCollectionInfo(OperatingSystem.IsAndroidVersionAtLeast(30)
				? new AccessibilityNodeInfo.CollectionInfo(1, ChildCount, false, (int)Android.Views.Accessibility.SelectionMode.Single)
				: AccessibilityNodeInfo.CollectionInfo.Obtain(1, ChildCount, false, Android.Views.Accessibility.SelectionMode.Single));
		}
	}

	/// <summary>
	/// TalkBack: each button is a radio button, checked when selected, at its column in the group. Set after
	/// MaterialButton's own node info, which reports a plain, non-checkable button.
	/// </summary>
	sealed class SegmentButton(Context context) : MaterialButton(context)
	{
		public override void OnInitializeAccessibilityNodeInfo(AccessibilityNodeInfo? info)
		{
			base.OnInitializeAccessibilityNodeInfo(info);
			if (info is null)
				return;
			info.ClassName = "android.widget.RadioButton";
			info.Checkable = true;
			if (OperatingSystem.IsAndroidVersionAtLeast(36))
				info.CheckedState = Selected ? CheckedState.True : CheckedState.False;
			else
				info.Checked = Selected;
			var index = Parent is ViewGroup group ? group.IndexOfChild(this) : 0;
			info.SetCollectionItemInfo(OperatingSystem.IsAndroidVersionAtLeast(30)
				? new AccessibilityNodeInfo.CollectionItemInfo(0, 1, index, 1, false, Selected)
				: AccessibilityNodeInfo.CollectionItemInfo.Obtain(0, 1, index, 1, false, Selected));
		}
	}
}
