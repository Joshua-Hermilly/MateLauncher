using MateLauncher.Infrastructure.Launching;

namespace MateLauncher.Tests.Infrastructure;

public class PerformanceModeTests
{
	[Fact]
	public void ParseProgramNames_SplitsTrimsAndRemovesExtension()
	{
		List<string> names = PerformanceMode.ParseProgramNames( "chrome, Discord.exe ;  spotify\nobs64" );

		Assert.Equal( new[] { "chrome", "Discord", "spotify", "obs64" }, names );
	}

	[Fact]
	public void ParseProgramNames_RemovesDuplicatesIgnoringCase()
	{
		Assert.Equal( new[] { "chrome" }, PerformanceMode.ParseProgramNames( "chrome, CHROME.exe, Chrome" ) );
	}

	[Theory]
	[InlineData( "explorer" )]
	[InlineData( "EXPLORER.EXE" )]
	[InlineData( "MateLauncher.App" )]
	[InlineData( "svchost" )]
	[InlineData( "winlogon" )]
	public void ParseProgramNames_NeverReturnsProtectedProcesses( string name )
	{
		Assert.Empty( PerformanceMode.ParseProgramNames( name ) );
	}

	[Fact]
	public void ParseProgramNames_RejectsPathsAndInvalidNames()
	{
		Assert.Empty( PerformanceMode.ParseProgramNames( @"C:\Windows\evil, a|b, <x>" ) );
	}

	[Theory]
	[InlineData( null )]
	[InlineData( "" )]
	[InlineData( "  ,  ; " )]
	public void ParseProgramNames_ReturnsEmptyForBlankInput( string? text )
	{
		Assert.Empty( PerformanceMode.ParseProgramNames( text ) );
	}
}
