using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Providers.Common;
using Microsoft.Win32;

namespace MateLauncher.Providers.Ubisoft;

public class UbisoftProvider : IGameProvider
{
	private const string InstallKeyPrefix = "Uplay Install ";

	public GamePlatform Platform => GamePlatform.UbisoftConnect;

	public IReadOnlyList<Game> Detect()
	{
		List<Game> games = new List<Game>();

		if ( !OperatingSystem.IsWindows() )
		{
			return games;
		}

		using RegistryKey baseKey = RegistryKey.OpenBaseKey( RegistryHive.LocalMachine, RegistryView.Registry32 );
		using RegistryKey? installsKey = baseKey.OpenSubKey( @"SOFTWARE\Ubisoft\Launcher\Installs" );

		if ( installsKey is null )
		{
			return games;
		}

		Dictionary<string, InstalledProgram> programs = InstalledProgramsReader.ReadAll()
			.Where( program => program.KeyName.StartsWith( InstallKeyPrefix, StringComparison.Ordinal ) )
			.GroupBy( program => program.KeyName )
			.ToDictionary( group => group.Key[ InstallKeyPrefix.Length.. ], group => group.First() );

		foreach ( string gameId in installsKey.GetSubKeyNames() )
		{
			using RegistryKey? gameKey = installsKey.OpenSubKey( gameId );
			string installPath = ( gameKey?.GetValue( "InstallDir" ) as string ?? string.Empty ).Replace( '/', '\\' ).TrimEnd( '\\' );

			if ( !Directory.Exists( installPath ) )
			{
				continue;
			}

			string name = programs.TryGetValue( gameId, out InstalledProgram? program ) ? program.DisplayName : new DirectoryInfo( installPath ).Name;

			games.Add( new Game
			{
				Name = name,
				Platform = Platform,
				PlatformGameId = gameId,
				InstallPath = installPath,
				LaunchCommand = $"uplay://launch/{gameId}/0"
			} );
		}

		return games;
	}
}
