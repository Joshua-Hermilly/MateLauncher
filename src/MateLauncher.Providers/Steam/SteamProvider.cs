using System.Text.RegularExpressions;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using Microsoft.Win32;

namespace MateLauncher.Providers.Steam;

public partial class SteamProvider : IGameProvider
{
	private static readonly string[] _CoverFileNames = { "library_600x900*.jpg", "library_capsule.jpg", "library_header.jpg", "library_hero.jpg" };

	public GamePlatform Platform => GamePlatform.Steam;

	public IReadOnlyList<Game> Detect()
	{
		List<Game> games = new List<Game>();
		string? steamPath = FindSteamPath();

		if ( steamPath is null )
		{
			return games;
		}

		foreach ( string libraryPath in FindLibraryPaths( steamPath ) )
		{
			string steamAppsPath = Path.Combine( libraryPath, "steamapps" );

			if ( !Directory.Exists( steamAppsPath ) )
			{
				continue;
			}

			foreach ( string manifestPath in Directory.EnumerateFiles( steamAppsPath, "appmanifest_*.acf" ) )
			{
				Game? game = ReadManifest( manifestPath, steamAppsPath, steamPath );

				if ( game is not null && !IsTool( game.Name ) )
				{
					games.Add( game );
				}
			}
		}

		return games;
	}

	internal static string? FindSteamPath()
	{
		if ( !OperatingSystem.IsWindows() )
		{
			return null;
		}

		using RegistryKey? key = Registry.CurrentUser.OpenSubKey( @"Software\Valve\Steam" );
		return key?.GetValue( "SteamPath" ) is string steamPath ? Path.GetFullPath( steamPath ) : null;
	}

	internal static IEnumerable<string> FindLibraryPaths( string steamPath )
	{
		yield return steamPath;

		string librariesFile = Path.Combine( steamPath, "steamapps", "libraryfolders.vdf" );

		if ( !File.Exists( librariesFile ) )
		{
			yield break;
		}

		foreach ( Match match in LibraryPathRegex().Matches( File.ReadAllText( librariesFile ) ) )
		{
			string path = match.Groups[ 1 ].Value.Replace( @"\\", @"\" );

			if ( !string.Equals( Path.GetFullPath( path ), Path.GetFullPath( steamPath ), StringComparison.OrdinalIgnoreCase ) )
			{
				yield return path;
			}
		}
	}

	private static bool IsTool( string name )
	{
		return name.StartsWith( "Steamworks Common", StringComparison.OrdinalIgnoreCase )
			|| name.StartsWith( "Steam Linux Runtime", StringComparison.OrdinalIgnoreCase )
			|| name.StartsWith( "Proton", StringComparison.OrdinalIgnoreCase );
	}

	private static string FindCover( string steamPath, string appId )
	{
		string cacheFolder = Path.Combine( steamPath, "appcache", "librarycache" );
		string flatFile = Path.Combine( cacheFolder, $"{appId}_library_600x900.jpg" );

		if ( File.Exists( flatFile ) )
		{
			return flatFile;
		}

		string appFolder = Path.Combine( cacheFolder, appId );

		if ( Directory.Exists( appFolder ) )
		{
			foreach ( string fileName in _CoverFileNames )
			{
				string? nestedFile = Directory.EnumerateFiles( appFolder, fileName, SearchOption.AllDirectories ).FirstOrDefault();

				if ( nestedFile is not null )
				{
					return nestedFile;
				}
			}
		}

		return $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg";
	}

	private static Game? ReadManifest( string manifestPath, string steamAppsPath, string steamPath )
	{
		string content = File.ReadAllText( manifestPath );
		Match appId = ValueRegex( "appid" ).Match( content );
		Match name = ValueRegex( "name" ).Match( content );
		Match installDir = ValueRegex( "installdir" ).Match( content );

		if ( !appId.Success || !name.Success )
		{
			return null;
		}

		return new Game
		{
			Name = name.Groups[ 1 ].Value,
			Platform = GamePlatform.Steam,
			PlatformGameId = appId.Groups[ 1 ].Value,
			InstallPath = installDir.Success ? Path.Combine( steamAppsPath, "common", installDir.Groups[ 1 ].Value ) : string.Empty,
			LaunchCommand = $"steam://run/{appId.Groups[ 1 ].Value}",
			CoverImagePath = FindCover( steamPath, appId.Groups[ 1 ].Value )
		};
	}

	private static Regex ValueRegex( string key )
	{
		return new Regex( $"\"{key}\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase );
	}

	[GeneratedRegex( "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase )]
	private static partial Regex LibraryPathRegex();
}
