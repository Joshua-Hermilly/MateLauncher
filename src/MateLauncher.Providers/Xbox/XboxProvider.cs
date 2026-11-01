using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Providers.Common;

namespace MateLauncher.Providers.Xbox;

public class XboxProvider : IGameProvider
{
	public GamePlatform Platform => GamePlatform.Xbox;

	public IReadOnlyList<Game> Detect()
	{
		List<Game> games = new List<Game>();

		foreach ( DriveInfo drive in DriveInfo.GetDrives().Where( drive => drive.DriveType == DriveType.Fixed && drive.IsReady ) )
		{
			string xboxFolder = Path.Combine( drive.RootDirectory.FullName, "XboxGames" );

			if ( !Directory.Exists( xboxFolder ) )
			{
				continue;
			}

			foreach ( string gameFolder in Directory.EnumerateDirectories( xboxFolder ) )
			{
				string contentFolder = Path.Combine( gameFolder, "Content" );
				string name = new DirectoryInfo( gameFolder ).Name;
				string? executable = ExecutableFinder.FindMainExecutable( Directory.Exists( contentFolder ) ? contentFolder : gameFolder, name );

				if ( executable is null )
				{
					continue;
				}

				games.Add( new Game
				{
					Name = name,
					Platform = Platform,
					PlatformGameId = gameFolder,
					InstallPath = gameFolder,
					LaunchCommand = executable
				} );
			}
		}

		return games;
	}
}
