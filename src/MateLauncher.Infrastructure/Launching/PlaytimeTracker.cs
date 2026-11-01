using System.Collections.Concurrent;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;

namespace MateLauncher.Infrastructure.Launching;

public class PlaytimeTracker : IPlaytimeTracker
{
	private static readonly TimeSpan _StartTimeout = TimeSpan.FromMinutes( 3 );
	private static readonly TimeSpan _PollInterval = TimeSpan.FromSeconds( 5 );

	private readonly ILibraryRepository _Repository;
	private readonly ConcurrentDictionary<Guid, bool> _TrackedGames = new ConcurrentDictionary<Guid, bool>();

	public event Action<Guid, long>? PlaytimeChanged;
	public event Action<Guid>? SessionStarted;
	public event Action<Guid>? SessionEnded;

	public PlaytimeTracker( ILibraryRepository repository )
	{
		_Repository = repository;
	}

	public void Track( Game game )
	{
		if ( !Directory.Exists( game.InstallPath ) || !_TrackedGames.TryAdd( game.Id, true ) )
		{
			return;
		}

		_ = Task.Run( () => WatchAsync( game ) );
	}

	private async Task WatchAsync( Game game )
	{
		try
		{
			DateTime waitingSince = DateTime.UtcNow;

			while ( !ProcessFinder.IsAnyRunning( game.InstallPath ) )
			{
				if ( DateTime.UtcNow - waitingSince > _StartTimeout )
				{
					return;
				}

				await Task.Delay( _PollInterval );
			}

			DateTime startedAt = DateTime.UtcNow;
			DateTime lastSeenAt = startedAt;
			SessionStarted?.Invoke( game.Id );

			while ( DateTime.UtcNow - lastSeenAt < _PollInterval * 3 )
			{
				await Task.Delay( _PollInterval );

				if ( ProcessFinder.IsAnyRunning( game.InstallPath ) )
				{
					lastSeenAt = DateTime.UtcNow;
				}
			}

			SessionEnded?.Invoke( game.Id );
			long seconds = ( long ) ( lastSeenAt - startedAt ).TotalSeconds;

			if ( seconds > 0 )
			{
				long totalSeconds = await _Repository.AddPlaytimeAsync( game.Id, seconds );
				PlaytimeChanged?.Invoke( game.Id, totalSeconds );
			}
		}
		finally
		{
			_TrackedGames.TryRemove( game.Id, out _ );
		}
	}
}
