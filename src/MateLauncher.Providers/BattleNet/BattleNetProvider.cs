using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Providers.Common;

namespace MateLauncher.Providers.BattleNet;

public class BattleNetProvider : IGameProvider
{
	public GamePlatform Platform => GamePlatform.BattleNet;

	public IReadOnlyList<Game> Detect()
	{
		List<Game> games = new List<Game>();

		IEnumerable<InstalledProgram> blizzardPrograms = InstalledProgramsReader.ReadAll()
			.Where( program => program.Publisher.Contains( "Blizzard", StringComparison.OrdinalIgnoreCase ) )
			.Where( program => !program.DisplayName.Contains( "Battle.net", StringComparison.OrdinalIgnoreCase ) )
			.GroupBy( program => program.DisplayName )
			.Select( group => group.First() );

		foreach ( InstalledProgram program in blizzardPrograms )
		{
			if ( !Directory.Exists( program.InstallLocation ) )
			{
				continue;
			}

			string? executable = ExecutableFinder.FindMainExecutable( program.InstallLocation, program.DisplayName );

			if ( executable is null )
			{
				continue;
			}

			games.Add( new Game
			{
				Name = program.DisplayName,
				Platform = Platform,
				PlatformGameId = program.KeyName,
				InstallPath = program.InstallLocation,
				LaunchCommand = executable
			} );
		}

		return games;
	}
}
