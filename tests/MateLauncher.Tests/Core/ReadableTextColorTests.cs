using MateLauncher.Core.Services;

namespace MateLauncher.Tests.Core;

public class ReadableTextColorTests
{
	[Theory]
	[InlineData( "#FF3B30", "#FFFFFF" )]
	[InlineData( "#A855F7", "#FFFFFF" )]
	[InlineData( "#3B9EFF", "#14181F" )]
	[InlineData( "#FFB020", "#14181F" )]
	[InlineData( "#2FD08A", "#14181F" )]
	[InlineData( "#000000", "#FFFFFF" )]
	[InlineData( "#FFFFFF", "#14181F" )]
	public void GetReadableTextColor_PicksTheMoreReadableOfLightAndDark( string accent, string expected )
	{
		Assert.Equal( expected, ColorContrast.GetReadableTextColor( accent ) );
	}
}
