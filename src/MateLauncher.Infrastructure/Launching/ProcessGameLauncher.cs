using System.Diagnostics;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;

namespace MateLauncher.Infrastructure.Launching;

public class ProcessGameLauncher : IGameLauncher
{
	private readonly ILibraryRepository _Repository;
	private readonly IPlaytimeTracker _PlaytimeTracker;

	public ProcessGameLauncher( ILibraryRepository repository, IPlaytimeTracker playtimeTracker )
	{
		_Repository = repository;
		_PlaytimeTracker = playtimeTracker;
	}

	public Task StopAsync( Game game )
	{
		if ( !Directory.Exists( game.InstallPath ) )
		{
			return Task.CompletedTask;
		}

		return Task.Run( () =>
		{
			foreach ( Process process in ProcessFinder.FindInFolder( game.InstallPath ) )
			{
				using ( process )
				{
					try
					{
						process.Kill( true );
					}
					catch ( Exception exception ) when ( exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException )
					{
					}
				}
			}
		} );
	}

	public async Task LaunchAsync( Game game )
	{
		if ( string.IsNullOrWhiteSpace( game.LaunchCommand ) )
		{
			throw new InvalidOperationException( $"No launch command for {game.Name}." );
		}

		ProcessStartInfo startInfo = new ProcessStartInfo( game.LaunchCommand )
		{
			UseShellExecute = true,
			Arguments = game.LaunchArguments
		};

		if ( File.Exists( game.LaunchCommand ) )
		{
			startInfo.WorkingDirectory = Directory.Exists( game.InstallPath ) ? game.InstallPath : Path.GetDirectoryName( game.LaunchCommand ) ?? string.Empty;
		}

		if ( game.RunAsAdministrator )
		{
			startInfo.Verb = "runas";
		}

		Process.Start( startInfo );
		await _Repository.MarkPlayedAsync( game.Id );
		_PlaytimeTracker.Track( game );
	}
}
