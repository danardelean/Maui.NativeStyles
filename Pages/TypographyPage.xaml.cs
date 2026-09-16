namespace MauiNativeStyle.Pages;

public partial class TypographyPage : ContentPage
{
	static readonly string[] iOSScale = ["LargeTitle", "Title1", "Title2", "Title3", "Headline", "Body", "Callout", "Subheadline", "Footnote", "Caption1", "Caption2"];
	static readonly string[] MaterialScale = ["DisplaySmall", "HeadlineLarge", "HeadlineMedium", "HeadlineSmall", "TitleLarge", "TitleMedium", "TitleSmall", "BodyLarge", "BodyMedium", "BodySmall", "LabelLarge", "LabelMedium", "LabelSmall"];

	public TypographyPage()
	{
		InitializeComponent();
		BuildScale();
		BuildColors();
	}

	void BuildScale()
	{
		var keys = DeviceInfo.Platform == DevicePlatform.iOS ? iOSScale : MaterialScale;
		foreach (var key in keys)
		{
			if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style)
				ScaleHost.Add(new Label { Text = key, Style = style });
		}
	}

	void BuildColors()
	{
		if (Application.Current is not { } app)
			return;

		var colors = new Dictionary<string, Color>();
		foreach (var dict in app.Resources.MergedDictionaries)
			foreach (var (key, value) in dict)
				if (value is Color c)
					colors[key] = c;

		foreach (var (key, light) in colors.OrderBy(k => k.Key, StringComparer.Ordinal))
		{
			if (key.EndsWith("Dark", StringComparison.Ordinal))
				continue;
			colors.TryGetValue(key + "Dark", out var dark);

			var row = new Grid { ColumnDefinitions = [new(28), new(28), new(GridLength.Star)], ColumnSpacing = 8 };
			row.Add(new BoxView { Color = light, CornerRadius = 6, HeightRequest = 28, WidthRequest = 28 }, 0);
			row.Add(new BoxView { Color = dark ?? light, CornerRadius = 6, HeightRequest = 28, WidthRequest = 28 }, 1);
			row.Add(new Label { Text = key, VerticalOptions = LayoutOptions.Center, StyleClass = ["Secondary"] }, 2);
			ColorHost.Add(row);
		}
	}
}
