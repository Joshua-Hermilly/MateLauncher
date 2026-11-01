using MateLauncher.Core.Models;

namespace MateLauncher.Core.Services;

public record SessionSummary( int Count, long AverageSeconds, long LongestSeconds );

public static class SessionStatistics
{
	public static SessionSummary Compute( IReadOnlyCollection<GameSession> sessions )
	{
		if ( sessions.Count == 0 )
		{
			return new SessionSummary( 0, 0, 0 );
		}

		return new SessionSummary( sessions.Count, ( long ) Math.Round( sessions.Average( session => ( double ) session.Seconds ) ), sessions.Max( session => session.Seconds ) );
	}

	public static double? ProgressRatio( long playedSeconds, int? secondsToBeat )
	{
		if ( secondsToBeat is null or <= 0 )
		{
			return null;
		}

		return Math.Clamp( ( double ) playedSeconds / secondsToBeat.Value, 0, 1 );
	}
}
