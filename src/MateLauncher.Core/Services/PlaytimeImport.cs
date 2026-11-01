using MateLauncher.Core.Models;

namespace MateLauncher.Core.Services;

public static class PlaytimeImport
{
	public static Dictionary<Guid, ImportedPlaytime> ComputeUpdates( IEnumerable<Game> games, IReadOnlyDictionary<string, ImportedPlaytime> steamPlaytimes )
	{
		Dictionary<Guid, ImportedPlaytime> updates = new Dictionary<Guid, ImportedPlaytime>();

		foreach ( Game game in games.Where( candidate => candidate.Platform == GamePlatform.Steam ) )
		{
			if ( !steamPlaytimes.TryGetValue( game.PlatformGameId, out ImportedPlaytime? steam ) )
			{
				continue;
			}

			long seconds = Math.Max( game.PlaytimeSeconds, steam.Seconds );
			DateTime? lastPlayed = game.LastPlayedAt ?? steam.LastPlayedAt;

			if ( seconds != game.PlaytimeSeconds || lastPlayed != game.LastPlayedAt )
			{
				updates[ game.Id ] = new ImportedPlaytime( seconds, lastPlayed );
			}
		}

		return updates;
	}
}
