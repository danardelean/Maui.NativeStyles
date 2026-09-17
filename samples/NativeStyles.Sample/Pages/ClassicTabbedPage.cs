namespace NativeStyles.Sample.Pages;

/// <summary>TabbedPage + NavigationPage, for apps that do not use Shell.</summary>
public class ClassicTabbedPage : TabbedPage
{
	public ClassicTabbedPage(bool topTabs = false)
	{
		// The library style places the Android bar at the bottom. MAUI's SetToolbarPlacement() throws when the value
		// was already set (here: by the style), so a different placement has to be assigned through SetValue.
		if (topTabs)
			SetValue(Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage.ToolbarPlacementProperty,
				Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.ToolbarPlacement.Top);
		Children.Add(new NavigationPage(new ClassicHomePage()) { Title = "Home", IconImageSource = "tab_lists.svg" });
		Children.Add(new NavigationPage(new SelectionPage()) { Title = "Selection", IconImageSource = "tab_selection.svg" });
		Children.Add(new NavigationPage(new TypographyPage()) { Title = "Type", IconImageSource = "tab_typography.svg" });
	}
}

public class ClassicHomePage : ContentPage
{
	public ClassicHomePage()
	{
		Title = "Classic";
		StyleClass = ["Grouped"];
		ToolbarItems.Add(new ToolbarItem("Shell", "tab_buttons.svg", () =>
		{
			if (Application.Current?.Windows.FirstOrDefault() is { } window)
				window.Page = new AppShell();
		}));
		ToolbarItems.Add(new ToolbarItem { Text = "Refresh", Order = ToolbarItemOrder.Secondary });
		ToolbarItems.Add(new ToolbarItem { Text = "Settings", Order = ToolbarItemOrder.Secondary });

		var open = new Button { Text = "Push a page", StyleClass = ["Filled"] };
		open.Clicked += async (_, _) => await Navigation.PushAsync(new ListsPage());
		Content = new VerticalStackLayout
		{
			Padding = new Thickness(20, 24),
			Spacing = 16,
			Children =
			{
				new Label { Text = "TabbedPage with a NavigationPage per tab. The toolbar has a primary item and two secondary items (overflow menu)." },
				open,
			},
		};
	}
}
