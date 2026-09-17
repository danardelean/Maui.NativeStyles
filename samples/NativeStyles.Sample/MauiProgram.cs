using Microsoft.Extensions.Logging;
using NativeStyles;

namespace NativeStyles.Sample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseNativeStyles(options =>
			{
				// One brand color for both platforms: the iOS tint (with a brighter dark-mode variant, as Apple does
				// with its own colors) and the seed of the generated Material 3 scheme on Android.
				options.Brand = new BrandPalette(Color.FromArgb("#0B7A75"), accentDark: Color.FromArgb("#3FC1B4"))
				{
					// Android: use the exact brand color as the light-theme primary instead of Material's darker tone
					MaterialColorMatch = true,
				};
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
