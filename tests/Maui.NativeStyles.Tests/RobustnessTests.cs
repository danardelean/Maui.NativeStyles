using Microsoft.Extensions.Logging;
using NativeStyles;
using Xunit;

namespace NativeStyles.Tests;

public class NativeStylesLogTests
{
	sealed class ListLogger : ILogger
	{
		public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			Entries.Add((logLevel, formatter(state, exception), exception));
	}

	[Fact]
	public void Messages_reported_before_the_logger_exists_are_flushed_in_order()
	{
		var log = new NativeStylesLog();
		var logger = new ListLogger();

		log.Report(LogLevel.Debug, "a", "first");
		log.Report(LogLevel.Warning, "b", "second");
		Assert.Empty(logger.Entries);

		log.Attach(logger);

		Assert.Equal([(LogLevel.Debug, "first", null), (LogLevel.Warning, "second", null)], logger.Entries);
	}

	[Fact]
	public void Each_problem_is_reported_once()
	{
		var log = new NativeStylesLog();
		var logger = new ListLogger();
		log.Attach(logger);
		var error = new InvalidOperationException("boom");

		log.Report(LogLevel.Warning, "hook", "missing", error);
		log.Report(LogLevel.Warning, "hook", "missing", error);

		var entry = Assert.Single(logger.Entries);
		Assert.Same(error, entry.Exception);
	}

	[Fact]
	public void Braces_in_messages_are_not_parsed_as_placeholders()
	{
		var log = new NativeStylesLog();
		var logger = new ListLogger();
		log.Attach(logger);

		log.Report(LogLevel.Warning, "braces", "{native:SystemColor} {0}");

		Assert.Equal("{native:SystemColor} {0}", Assert.Single(logger.Entries).Message);
	}

	[Fact]
	public void Early_messages_are_capped_when_no_logger_is_ever_attached()
	{
		var log = new NativeStylesLog();
		for (var i = 0; i < 100; i++)
			log.Report(LogLevel.Debug, i.ToString(), i.ToString());
		var logger = new ListLogger();

		log.Attach(logger);

		Assert.InRange(logger.Entries.Count, 1, 32);
	}
}
