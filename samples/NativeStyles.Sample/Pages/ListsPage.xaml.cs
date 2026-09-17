namespace NativeStyles.Sample.Pages;

public record ListItem(string Title, string Detail, Color Color, bool HasSeparator = true)
{
	public string Initial => Title[..1];
}

public partial class ListsPage : ContentPage
{
	public IList<ListItem> Settings { get; } =
	[
		new("General", "", Color.FromArgb("#8E8E93")),
		new("Display", "Automatic", Color.FromArgb("#0088FF")),
		new("Sounds", "", Color.FromArgb("#FF383C")),
		new("Privacy", "", Color.FromArgb("#6155F5")),
		new("Battery", "84%", Color.FromArgb("#34C759"), HasSeparator: false),
	];

	public IList<ListItem> NoItems { get; } = [];

	public IList<ListItem> Contacts { get; } =
	[
		new("Ada Lovelace", "Mathematician", Color.FromArgb("#6750A4")),
		new("Grace Hopper", "Rear admiral", Color.FromArgb("#0088FF")),
		new("Margaret Hamilton", "Software engineer", Color.FromArgb("#34C759")),
		new("Katherine Johnson", "Physicist", Color.FromArgb("#FF8D28")),
		new("Hedy Lamarr", "Inventor", Color.FromArgb("#CB30E0")),
	];

	public ListsPage()
	{
		InitializeComponent();
		BindingContext = this;
	}
}
