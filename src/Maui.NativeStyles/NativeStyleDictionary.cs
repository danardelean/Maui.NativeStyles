namespace NativeStyles;

/// <summary>
/// The platform resource dictionary: iOS 26 (Liquid Glass) styles on iOS, Material 3 styles on Android.
/// Merge it into the application resources:
/// <code>
/// &lt;Application.Resources&gt;
///     &lt;ResourceDictionary&gt;
///         &lt;ResourceDictionary.MergedDictionaries&gt;
///             &lt;native:NativeStyleDictionary /&gt;
///         &lt;/ResourceDictionary.MergedDictionaries&gt;
///     &lt;/ResourceDictionary&gt;
/// &lt;/Application.Resources&gt;
/// </code>
/// Only the current platform's XAML is compiled into the assembly.
/// </summary>
public class NativeStyleDictionary : ResourceDictionary
{
	public NativeStyleDictionary()
	{
#if IOS
		MergedDictionaries.Add(new Resources.iOS.iOSStyles());
#elif ANDROID
		MergedDictionaries.Add(new Resources.Android.MaterialStyles());
#endif
		// The static shared keys follow the brand too (entries of this dictionary win over the merged ones)
		if (SystemColors.Brand is not null)
		{
			var accent = SystemColors.Resolve(SystemColorRole.Accent);
			this["AccentColor"] = accent.Light;
			this["AccentColorDark"] = accent.Dark;
			var destructive = SystemColors.Resolve(SystemColorRole.Destructive);
			this["DestructiveColor"] = destructive.Light;
			this["DestructiveColorDark"] = destructive.Dark;
		}
	}
}
