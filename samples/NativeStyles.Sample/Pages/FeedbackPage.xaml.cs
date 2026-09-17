namespace NativeStyles.Sample.Pages;

public partial class FeedbackPage : ContentPage
{
	public FeedbackPage() => InitializeComponent();

	async void OnAnimateProgress(object? sender, EventArgs e)
	{
		await Progress.ProgressTo(0, 150, Easing.Linear);
		await Progress.ProgressTo(1, 1500, Easing.CubicInOut);
	}

	async void OnShowAlert(object? sender, EventArgs e) =>
		await DisplayAlertAsync("Delete photo?", "This photo will be removed from all your devices.", "Delete", "Cancel");

	async void OnShowActionSheet(object? sender, EventArgs e) =>
		await DisplayActionSheetAsync("Share with", "Cancel", "Delete", "Messages", "Mail", "AirDrop");

	async void OnShowPrompt(object? sender, EventArgs e) =>
		await DisplayPromptAsync("Rename", "Enter a new name", "OK", "Cancel", placeholder: "Name");
}
