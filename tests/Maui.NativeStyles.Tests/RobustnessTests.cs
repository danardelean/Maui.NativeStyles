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

public class NativeStylesOptionsTests
{
	static readonly Color Brand = Color.FromArgb("#0B7A75");

	[Fact]
	public void Entry_handler_is_replaced_by_default()
	{
		Assert.True(new NativeStylesOptions().ReplaceEntryHandler);
	}

	[Fact]
	public void Default_options_are_equivalent()
	{
		Assert.True(new NativeStylesOptions().IsEquivalentTo(new NativeStylesOptions()));
	}

	[Fact]
	public void Brands_are_compared_by_value()
	{
		var first = new NativeStylesOptions { Brand = new BrandPalette(Brand) { MaterialColorMatch = true }.Set(SystemColorRole.Destructive, Colors.Red) };
		var second = new NativeStylesOptions { Brand = new BrandPalette(Brand) { MaterialColorMatch = true }.Set(SystemColorRole.Destructive, Colors.Red) };

		Assert.True(first.IsEquivalentTo(second));
	}

	[Theory]
	[InlineData(nameof(NativeStylesOptions.AndroidDynamicColors))]
	[InlineData(nameof(NativeStylesOptions.AndroidRecreateOnThemeChange))]
	[InlineData(nameof(NativeStylesOptions.ReplaceEntryHandler))]
	[InlineData(nameof(NativeStylesOptions.Brand))]
	[InlineData(nameof(BrandPalette.TintsSwitches))]
	[InlineData(nameof(BrandPalette.IOS))]
	[InlineData(nameof(BrandPalette.Set))]
	public void Any_difference_makes_options_not_equivalent(string difference)
	{
		var first = new NativeStylesOptions { Brand = new BrandPalette(Brand) };
		var second = new NativeStylesOptions { Brand = new BrandPalette(Brand) };
		switch (difference)
		{
			case nameof(NativeStylesOptions.AndroidDynamicColors): second.AndroidDynamicColors = true; break;
			case nameof(NativeStylesOptions.AndroidRecreateOnThemeChange): second.AndroidRecreateOnThemeChange = false; break;
			case nameof(NativeStylesOptions.ReplaceEntryHandler): second.ReplaceEntryHandler = false; break;
			case nameof(NativeStylesOptions.Brand): second.Brand = null; break;
			case nameof(BrandPalette.TintsSwitches): second.Brand!.TintsSwitches = true; break;
			case nameof(BrandPalette.IOS): second.Brand!.IOS.Accent = Colors.Orange; break;
			case nameof(BrandPalette.Set): second.Brand!.Set(SystemColorRole.Accent, Colors.Orange); break;
		}

		Assert.False(first.IsEquivalentTo(second));
		Assert.False(second.IsEquivalentTo(first));
	}
}
