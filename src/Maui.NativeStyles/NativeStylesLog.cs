using Microsoft.Extensions.Logging;

namespace NativeStyles;

/// <summary>
/// Library diagnostics, written to the app's Microsoft.Extensions.Logging pipeline under the
/// <see cref="Category"/> category so they reach its providers in Release builds too (Debug.WriteLine is compiled out
/// of the package). UseNativeStyles runs while the builder is still being configured, before the app's services
/// exist: those first messages are kept and written once the MauiApp is built. Each distinct problem is reported once
/// per process, and nothing is logged on the mapping paths unless something is wrong.
/// </summary>
sealed class NativeStylesLog
{
	internal const string Category = "Maui.NativeStyles";

	// Only startup messages are expected before the logger exists; the cap guards an app that never builds its MauiApp
	const int MaxPending = 32;

	/// <summary>The log the library reports to.</summary>
	internal static NativeStylesLog Shared { get; } = new();

	readonly Lock _gate = new();
	readonly HashSet<string> _reported = new(StringComparer.Ordinal);
	List<(LogLevel Level, string Message, Exception? Exception)>? _pending = [];
	ILogger? _logger;

	internal static void Debug(string key, string message) => Shared.Report(LogLevel.Debug, key, message);

	internal static void Warning(string key, string message, Exception? exception = null) => Shared.Report(LogLevel.Warning, key, message, exception);

	/// <summary>Logs <paramref name="message"/> unless a message with the same <paramref name="key"/> was already reported.</summary>
	internal void Report(LogLevel level, string key, string message, Exception? exception = null)
	{
		ILogger? logger;
		lock (_gate)
		{
			if (!_reported.Add(key))
				return;
			logger = _logger;
			if (logger is null)
			{
				if (_pending is { Count: < MaxPending } pending)
					pending.Add((level, message, exception));
				return;
			}
		}
		Write(logger, level, message, exception);
	}

	/// <summary>Starts writing to <paramref name="logger"/> and flushes the messages reported before it existed.</summary>
	internal void Attach(ILogger logger)
	{
		List<(LogLevel Level, string Message, Exception? Exception)>? pending;
		lock (_gate)
		{
			_logger = logger;
			pending = _pending;
			_pending = null;
		}
		if (pending is null)
			return;
		foreach (var (level, message, exception) in pending)
			Write(logger, level, message, exception);
	}

	// The message is already text: pass it as the state instead of a message template, so braces in exception
	// messages or type names are not parsed as placeholders.
	static void Write(ILogger logger, LogLevel level, string message, Exception? exception) =>
		logger.Log(level, default, message, exception, static (state, _) => state);
}

/// <summary>Hands the app's logger to <see cref="NativeStylesLog"/> when the MauiApp is built.</summary>
sealed class NativeStylesLogInitializer : IMauiInitializeService
{
	public void Initialize(IServiceProvider services)
	{
		if (services.GetService(typeof(ILoggerFactory)) is ILoggerFactory factory)
			NativeStylesLog.Shared.Attach(factory.CreateLogger(NativeStylesLog.Category));
	}
}
