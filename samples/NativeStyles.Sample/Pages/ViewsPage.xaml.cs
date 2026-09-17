namespace NativeStyles.Sample.Pages;

public record CardItem(string Title, string Body);

public partial class ViewsPage : ContentPage
{
	public IList<CardItem> Cards { get; } =
	[
		new("Liquid Glass", "Floating navigation and controls above the content layer."),
		new("Material 3", "Tonal surfaces, pill buttons and an active indicator."),
		new("One markup", "The same XAML renders native on both platforms."),
	];

	public IList<string> Messages { get; } = ["Weekly report", "Invoice #1042", "Lunch on Friday?"];

	public ViewsPage()
	{
		InitializeComponent();
		BindingContext = this;
	}

	async void OnRefreshing(object? sender, EventArgs e)
	{
		await Task.Delay(1200);
		RefreshLabel.Text = $"Refreshed at {DateTime.Now:T}.";
		Refresh.IsRefreshing = false;
	}
}
