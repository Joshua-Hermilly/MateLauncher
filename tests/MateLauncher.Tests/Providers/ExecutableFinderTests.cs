using MateLauncher.Providers.Common;

namespace MateLauncher.Tests.Providers;

public class ExecutableFinderTests
{
	[Theory]
	[InlineData( "Portal 2", "portal2" )]
	[InlineData( "The Witcher 3: Wild Hunt", "thewitcher3wildhunt" )]
	[InlineData( "  ", "" )]
	public void Normalize_KeepsLowercaseLettersAndDigits( string input, string expected )
	{
		Assert.Equal( expected, ExecutableFinder.Normalize( input ) );
	}
}
