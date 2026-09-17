namespace NativeStyles;

public class NativeStylesOptions
{
	/// <summary>
	/// Android 12+: apply Material You dynamic colors (derived from the wallpaper) to every activity and resolve
	/// <c>{native:SystemColor}</c> through them. Off by default so the Material 3 baseline palette is used.
	/// </summary>
	public bool AndroidDynamicColors { get; set; }

	/// <summary>
	/// The app's brand colors. On Android a brand seed takes precedence over <see cref="AndroidDynamicColors"/>
	/// (a branded app keeps its colors instead of following the wallpaper).
	/// </summary>
	public BrandPalette? Brand { get; set; }
}
