using MateLauncher.Core.Models;
using MateLauncher.Core.Services;

namespace MateLauncher.Tests.Core;

public class SessionStatisticsTests
{
	private static GameSession Session( long seconds )
	{
		return new GameSession { GameId = Guid.NewGuid(), StartedAt = DateTime.Now, Seconds = seconds };
	}

	[Fact]
	public void Compute_ReturnsZerosWithoutSessions()
	{
		SessionSummary summary = SessionStatistics.Compute( Array.Empty<GameSession>() );

		Assert.Equal( 0, summary.Count );
		Assert.Equal( 0, summary.AverageSeconds );
		Assert.Equal( 0, summary.LongestSeconds );
	}

	[Fact]
	public void Compute_ReturnsCountAverageAndLongest()
	{
		SessionSummary summary = SessionStatistics.Compute( new[] { Session( 600 ), Session( 1800 ), Session( 3600 ) } );

		Assert.Equal( 3, summary.Count );
		Assert.Equal( 2000, summary.AverageSeconds );
		Assert.Equal( 3600, summary.LongestSeconds );
	}

	[Theory]
	[InlineData( 0, 36000, 0.0 )]
	[InlineData( 18000, 36000, 0.5 )]
	[InlineData( 72000, 36000, 1.0 )]
	public void ProgressRatio_IsClampedBetweenZeroAndOne( long played, int toBeat, double expected )
	{
		Assert.Equal( expected, SessionStatistics.ProgressRatio( played, toBeat ) );
	}

	[Theory]
	[InlineData( null )]
	[InlineData( 0 )]
	public void ProgressRatio_IsNullWhenDurationUnknown( int? toBeat )
	{
		Assert.Null( SessionStatistics.ProgressRatio( 1000, toBeat ) );
	}
}
