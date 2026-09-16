namespace MauiNativeStyle.Pages;

public partial class SelectionPage : ContentPage
{
	public SelectionPage() => InitializeComponent();

	void OnStepperChanged(object? sender, ValueChangedEventArgs e) =>
		StepperValue.Text = e.NewValue.ToString("0");
}
