using NativeStyles;
using Xunit;

namespace NativeStyles.Tests;

public class NativeStyleDictionaryTests
{
	[Fact]
	public void Is_empty_on_the_plain_net_target()
	{
		// Platform XAML is only compiled into the iOS and Android assemblies; the net10.0 build is a no-op.
		var dictionary = new NativeStyleDictionary();

		Assert.Empty(dictionary.MergedDictionaries);
	}
}
