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
			.UseNativeStyles();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
