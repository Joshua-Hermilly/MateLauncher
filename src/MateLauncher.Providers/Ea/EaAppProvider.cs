using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Providers.Common;
using Microsoft.Win32;

namespace MateLauncher.Providers.Ea;

public class EaAppProvider : IGameProvider
{
	private static readonly string[] _ExcludedNames = { "EA app", "EA Desktop", "Origin", "EA Help" };

	public GamePlatform Platform => GamePlatform.EaApp;

	public IReadOnlyList<Game> Detect()
	{
		Dictionary<string, string> installFolders = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );

		foreach ( InstalledProgram program in InstalledProgramsReader.ReadAll().Where( program => program.Publisher.Contains( "Electronic Arts", StringComparison.OrdinalIgnoreCase ) ) )
		{
			if ( !_ExcludedNames.Any( excluded => program.DisplayName.StartsWith( excluded, StringComparison.OrdinalIgnoreCase ) ) && Directory.Exists( program.InstallLocation ) )
			{
				installFolders[ program.DisplayName ] = program.InstallLocation;
			}
		}

		foreach ( ( string name, string folder ) in ReadEaGamesRegistry() )
		{
			installFolders.TryAdd( name, folder );
		}

		List<Game> games = new List<Game>();

		foreach ( ( string name, string folder ) in installFolders )
		{
			string? executable = ExecutableFinder.FindMainExecutable( folder, name );

			if ( executable is null )
			{
				continue;
			}

			games.Add( new Game
			{
				Name = name,
				Platform = Platform,
				PlatformGameId = folder,
				InstallPath = folder,
				LaunchCommand = executable
			} );
		}

		return games;
	}

	private static IEnumerable<( string Name, string Folder )> ReadEaGamesRegistry()
	{
		if ( !OperatingSystem.IsWindows() )
		{
			yield break;
		}

		using RegistryKey baseKey = RegistryKey.OpenBaseKey( RegistryHive.LocalMachine, RegistryView.Registry32 );
		using RegistryKey? eaKey = baseKey.OpenSubKey( @"SOFTWARE\EA Games" );

		if ( eaKey is null )
		{
			yield break;
		}

		foreach ( string name in eaKey.GetSubKeyNames() )
		{
			using RegistryKey? gameKey = eaKey.OpenSubKey( name );
			string folder = gameKey?.GetValue( "Install Dir" ) as string ?? string.Empty;

			if ( Directory.Exists( folder ) )
			{
				yield return ( name, folder.TrimEnd( '\\' ) );
			}
		}
	}
}
