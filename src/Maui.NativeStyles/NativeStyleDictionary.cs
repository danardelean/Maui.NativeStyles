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
	}
}
