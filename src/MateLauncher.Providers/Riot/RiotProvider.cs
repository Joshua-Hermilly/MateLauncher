using System.Text.Json;
using System.Text.RegularExpressions;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;

namespace MateLauncher.Providers.Riot;

public partial class RiotProvider : IGameProvider
{
	private static readonly Dictionary<string, string> _ProductNames = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase )
	{
		[ "valorant" ] = "VALORANT",
		[ "league_of_legends" ] = "League of Legends",
		[ "bacon" ] = "Legends of Runeterra",
		[ "lion" ] = "2XKO",
		[ "teamfighttactics" ] = "Teamfight Tactics"
	};

	[GeneratedRegex( "product_install_full_path:\\s*\"?(?<path>[^\"\\r\\n]+)\"?" )]
	private static partial Regex InstallPathPattern();

	public GamePlatform Platform => GamePlatform.Riot;

	public IReadOnlyList<Game> Detect()
	{
		List<Game> games = new List<Game>();
		string riotData = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData ), "Riot Games" );
		string metadataFolder = Path.Combine( riotData, "Metadata" );
		string? clientPath = FindClientPath( riotData );

		if ( clientPath is null || !Directory.Exists( metadataFolder ) )
		{
			return games;
		}

		foreach ( string productFolder in Directory.EnumerateDirectories( metadataFolder ) )
		{
			string folderName = new DirectoryInfo( productFolder ).Name;
			string[] parts = folderName.Split( '.' );

			if ( parts.Length < 2 )
			{
				continue;
			}

			string product = parts[ 0 ];
			string patchline = parts[ 1 ];
			string settingsFile = Path.Combine( productFolder, $"{folderName}.product_settings.yaml" );

			if ( !File.Exists( settingsFile ) )
			{
				continue;
			}

			Match match = InstallPathPattern().Match( File.ReadAllText( settingsFile ) );
			string installPath = match.Success ? match.Groups[ "path" ].Value.Trim().Replace( "/", "\\" ) : string.Empty;

			if ( !Directory.Exists( installPath ) )
			{
				continue;
			}

			games.Add( new Game
			{
				Name = _ProductNames.TryGetValue( product, out string? name ) ? name : product,
				Platform = Platform,
				PlatformGameId = folderName,
				InstallPath = installPath,
				LaunchCommand = clientPath,
				LaunchArguments = $"--launch-product={product} --launch-patchline={patchline}"
			} );
		}

		return games;
	}

	private static string? FindClientPath( string riotData )
	{
		string installsFile = Path.Combine( riotData, "RiotClientInstalls.json" );

		if ( File.Exists( installsFile ) )
		{
			try
			{
				using JsonDocument document = JsonDocument.Parse( File.ReadAllText( installsFile ) );

				foreach ( string key in new[] { "rc_default", "rc_live" } )
				{
					if ( document.RootElement.TryGetProperty( key, out JsonElement element ) && element.GetString() is string path && File.Exists( path ) )
					{
						return path;
					}
				}
			}
			catch ( Exception exception ) when ( exception is JsonException or IOException )
			{
			}
		}

		string fallback = @"C:\Riot Games\Riot Client\RiotClientServices.exe";
		return File.Exists( fallback ) ? fallback : null;
	}
}
