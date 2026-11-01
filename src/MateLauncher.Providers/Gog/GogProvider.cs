using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Providers.Common;
using Microsoft.Win32;

namespace MateLauncher.Providers.Gog;

public class GogProvider : IGameProvider
{
	public GamePlatform Platform => GamePlatform.Gog;

	public IReadOnlyList<Game> Detect()
	{
		List<Game> games = new List<Game>();

		if ( !OperatingSystem.IsWindows() )
		{
			return games;
		}

		using RegistryKey baseKey = RegistryKey.OpenBaseKey( RegistryHive.LocalMachine, RegistryView.Registry32 );
		using RegistryKey? gamesKey = baseKey.OpenSubKey( @"SOFTWARE\GOG.com\Games" );

		if ( gamesKey is null )
		{
			return games;
		}

		foreach ( string gameId in gamesKey.GetSubKeyNames() )
		{
			using RegistryKey? gameKey = gamesKey.OpenSubKey( gameId );
			string name = gameKey?.GetValue( "gameName" ) as string ?? string.Empty;
			string installPath = gameKey?.GetValue( "path" ) as string ?? string.Empty;
			string executable = gameKey?.GetValue( "exe" ) as string ?? string.Empty;

			if ( name.Length == 0 || !Directory.Exists( installPath ) )
			{
				continue;
			}

			string? launchCommand = File.Exists( executable ) ? executable : ExecutableFinder.FindMainExecutable( installPath, name );

			if ( launchCommand is null )
			{
				continue;
			}

			games.Add( new Game
			{
				Name = name,
				Platform = Platform,
				PlatformGameId = gameId,
				InstallPath = installPath,
				LaunchCommand = launchCommand,
				LaunchArguments = gameKey?.GetValue( "launchParam" ) as string ?? string.Empty
			} );
		}

		return games;
	}
}
