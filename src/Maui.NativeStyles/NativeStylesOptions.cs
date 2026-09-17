namespace NativeStyles;

public class NativeStylesOptions
{
	/// <summary>
	/// Android 12+: apply Material You dynamic colors (derived from the wallpaper) to every activity and resolve
	/// <c>{native:SystemColor}</c> through them. Off by default so the Material 3 baseline palette is used.
	/// </summary>
	public bool AndroidDynamicColors { get; set; }
}
