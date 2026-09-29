#if ANDROID
using System.Reflection;
using Android.Content.Res;
using Android.Views;
using Android.Widget;
using Google.Android.Material.Button;
using Google.Android.Material.Color;
using Google.Android.Material.TextField;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace NativeStyles.DeviceTests;

/// <summary>
/// Canary for the reflection in NativeStyles.Android.cs (HookMaterial3Mapper): with UseMaterial3 the input controls are
/// served by internal *Handler2 classes whose public static Mapper the library extends. A MAUI update that renames or
/// removes them silently drops the native styling, so fail loudly here.
/// </summary>
public class AndroidMaterial3HandlerTests
{
	[Theory]
	[InlineData("EntryHandler2", typeof(IEntry))]
	[InlineData("EditorHandler2", typeof(IEditor))]
	[InlineData("SearchBarHandler2", typeof(ISearchBar))]
	[InlineData("PickerHandler2", typeof(IPicker))]
	[InlineData("DatePickerHandler2", typeof(IDatePicker))]
	[InlineData("TimePickerHandler2", typeof(ITimePicker))]
	public void Internal_handler_exposes_a_public_static_mapper_carrying_the_native_style(string name, Type viewType)
	{
		var type = typeof(EntryHandler).Assembly.GetType("Microsoft.Maui.Handlers." + name);
		Assert.NotNull(type);

		var mapper = type.GetField("Mapper", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
		Assert.NotNull(mapper);

		// The library casts it to IPropertyMapper<TView, IElementHandler> (covariant) before adding its keys
		Assert.True(typeof(IPropertyMapper<,>).MakeGenericType(viewType, typeof(IElementHandler)).IsInstanceOfType(mapper),
			$"{name}.Mapper is a {mapper.GetType()}");
		Assert.Contains(NativeStylesExtensions.MappingKey, ((IPropertyMapper)mapper).GetKeys());
	}

	[Fact]
	public async Task Entry_is_served_by_EntryHandler2_as_a_TextInputLayout()
	{
		var (handler, view) = await UI.Run(() =>
		{
			var h = new Entry().ToHandler(UI.Context);
			return (h.GetType().Name, h.PlatformView?.GetType());
		});

		Assert.Equal("EntryHandler2", handler);
		Assert.True(typeof(TextInputLayout).IsAssignableFrom(view), $"platform view is {view}");
	}
}

public class AndroidEntryTests
{
	[Fact]
	public async Task Contained_entry_is_a_filled_box_with_28dp_corners_and_no_stroke()
	{
		var (mode, corners, expected, stroke) = await UI.Run(() =>
		{
			var entry = new Entry();
			NativeEntry.SetIsContained(entry, true);
			var layout = UI.PlatformView<TextInputLayout>(entry);
			float[] radii = [layout.BoxCornerRadiusTopStart, layout.BoxCornerRadiusTopEnd, layout.BoxCornerRadiusBottomStart, layout.BoxCornerRadiusBottomEnd];
			return (layout.BoxBackgroundMode, radii, layout.Context!.ToPixels(28), layout.BoxStrokeWidth);
		});

		Assert.Equal(TextInputLayout.BoxBackgroundFilled, mode);
		Assert.All(corners, radius => Assert.Equal(expected, radius, 1));
		Assert.Equal(0, stroke);
	}

	[Fact]
	public async Task Default_entry_keeps_the_Material3_outlined_box()
	{
		var mode = await UI.Run(() => UI.PlatformView<TextInputLayout>(new Entry()).BoxBackgroundMode);

		Assert.Equal(TextInputLayout.BoxBackgroundOutline, mode);
	}

	[Fact]
	public async Task Plain_entry_has_no_box()
	{
		var (mode, stroke) = await UI.Run(() =>
		{
			var entry = new Entry();
			NativeEntry.SetIsPlain(entry, true);
			var layout = UI.PlatformView<TextInputLayout>(entry);
			return (layout.BoxBackgroundMode, layout.BoxStrokeWidth);
		});

		Assert.Equal(TextInputLayout.BoxBackgroundNone, mode);
		Assert.Equal(0, stroke);
	}
}

public class AndroidSegmentedControlTests
{
	[Fact]
	public async Task One_MaterialButton_per_item_and_the_selection_is_kept()
	{
		var (count, texts, selected) = await UI.Run(() =>
		{
			var control = new SegmentedControl { Items = { "Day", "Week", "Month" }, SelectedIndex = 1 };
			var group = UI.PlatformView<LinearLayout>(control);
			control.Items.Add("Year"); // observable items: the group is rebuilt
			var buttons = Enumerable.Range(0, group.ChildCount).Select(group.GetChildAt).OfType<MaterialButton>().ToList();
			return (group.ChildCount, buttons.Select(b => b.Text ?? "").ToArray(), buttons.FindIndex(b => b.Selected));
		});

		Assert.Equal(4, count);
		Assert.Equal(["Day", "Week", "Month", "Year"], texts);
		Assert.Equal(1, selected);
	}

	[Fact]
	public async Task Tapping_a_segment_updates_the_view()
	{
		var index = await UI.Run(() =>
		{
			var control = new SegmentedControl { Items = { "Day", "Week", "Month" } };
			var group = UI.PlatformView<LinearLayout>(control);
			group.GetChildAt(2)!.PerformClick();
			return control.SelectedIndex;
		});

		Assert.Equal(2, index);
	}
}

public class AndroidSystemColorTests
{
	// The documented mapping (SystemColorRole XML docs), written out independently of the library
	static int Attribute(SystemColorRole role) => role switch
	{
		SystemColorRole.Accent => Resource.Attribute.colorPrimary,
		SystemColorRole.OnAccent => Resource.Attribute.colorOnPrimary,
		SystemColorRole.Destructive => Resource.Attribute.colorError,
		SystemColorRole.TextPrimary => Resource.Attribute.colorOnSurface,
		SystemColorRole.TextSecondary or SystemColorRole.Placeholder => Resource.Attribute.colorOnSurfaceVariant,
		SystemColorRole.TextTertiary or SystemColorRole.Gray => Resource.Attribute.colorOutline,
		SystemColorRole.Separator => Resource.Attribute.colorOutlineVariant,
		SystemColorRole.PageBackground => Resource.Attribute.colorSurface,
		SystemColorRole.GroupedBackground => Resource.Attribute.colorSurfaceContainer,
		SystemColorRole.CardBackground => Resource.Attribute.colorSurfaceContainerLow,
		SystemColorRole.GroupContainer => Resource.Attribute.colorSurfaceBright,
		SystemColorRole.Fill => Resource.Attribute.colorSurfaceContainerHighest,
		SystemColorRole.SecondaryFill => Resource.Attribute.colorSurfaceContainerHigh,
		SystemColorRole.TonalContainer => Resource.Attribute.colorSecondaryContainer,
		SystemColorRole.OnTonalContainer => Resource.Attribute.colorOnSecondaryContainer,
		_ => 0, // Success and Warning have no Material role
	};

	/// <summary>The attribute as the MAUI Material 3 theme resolves it in a day or night configuration.</summary>
	static Color ThemeColor(int attribute, bool dark)
	{
		var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity!;
		var configuration = new Configuration(activity.Resources!.Configuration!);
		configuration.UiMode = (configuration.UiMode & ~UiMode.NightMask) | (dark ? UiMode.NightYes : UiMode.NightNo);
		var context = new ContextThemeWrapper(activity.CreateConfigurationContext(configuration), Resource.Style.Maui_Material3_Theme_NoActionBar);
		var color = new Android.Graphics.Color(MaterialColors.GetColor(context, attribute, 0));
		return Color.FromRgba(color.R, color.G, color.B, color.A);
	}

	[Theory]
	[MemberData(nameof(UI.Roles), MemberType = typeof(UI))]
	public async Task Resolves_the_Material3_theme_attribute_for_day_and_night(SystemColorRole role)
	{
		Assert.Null(SystemColors.Brand);
		var attribute = Attribute(role);
		if (attribute == 0)
		{
			// No Material role: the static value is the documented answer
			Assert.Equal(SystemColors.Fallback(role), SystemColors.Resolve(role));
			return;
		}

		var (expected, actual) = await UI.Run(() => ((ThemeColor(attribute, dark: false), ThemeColor(attribute, dark: true)), SystemColors.Resolve(role)));

		Assert.Equal(expected, actual);
	}

	[Fact]
	public async Task Light_and_dark_values_differ_for_the_page_background()
	{
		var (light, dark) = await UI.Run(() => SystemColors.Resolve(SystemColorRole.PageBackground));

		Assert.NotEqual(light, dark);
		Assert.True(light.GetLuminosity() > dark.GetLuminosity());
	}

	[Theory]
	[MemberData(nameof(UI.Roles), MemberType = typeof(UI))]
	public async Task The_platform_answers_instead_of_the_static_fallback(SystemColorRole role)
	{
		if (Attribute(role) == 0)
			return;
		var resolvePlatform = typeof(SystemColors).GetMethod("ResolvePlatform", BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(resolvePlatform);

		var platform = await UI.Run(() => resolvePlatform.Invoke(null, [role]));

		Assert.NotNull(platform);
	}
}
#endif
