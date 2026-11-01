using System.Text.RegularExpressions;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;

namespace MateLauncher.Providers.Steam;

public partial class SteamUpdateChecker : IUpdateChecker
{
	private static readonly string[] _ToolPrefixes = { "Steamworks Common", "Steam Linux Runtime", "Proton" };

	private const int FullyInstalledFlag = 4;
	private const int UpdateRequiredFlag = 2;

	[GeneratedRegex( "\"(?<key>[A-Za-z]+)\"\\s+\"(?<value>[^\"]*)\"" )]
	private static partial Regex ValuePattern();

	public IReadOnlyList<GameUpdate> FindPendingUpdates()
	{
		List<GameUpdate> updates = new List<GameUpdate>();
		string? steamPath = SteamProvider.FindSteamPath();

		if ( steamPath is null )
		{
			return updates;
		}

		foreach ( string libraryPath in SteamProvider.FindLibraryPaths( steamPath ) )
		{
			string steamAppsPath = Path.Combine( libraryPath, "steamapps" );

			if ( !Directory.Exists( steamAppsPath ) )
			{
				continue;
			}

			foreach ( string manifestPath in Directory.EnumerateFiles( steamAppsPath, "appmanifest_*.acf" ) )
			{
				GameUpdate? update = ReadManifest( manifestPath );

				if ( update is not null )
				{
					updates.Add( update );
				}
			}
		}

		return updates;
	}

	private static long GetFolderSize( string folder )
	{
		if ( !Directory.Exists( folder ) )
		{
			return 0;
		}

		long total = 0;

		foreach ( string file in Directory.EnumerateFiles( folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true } ) )
		{
			try
			{
				total += new FileInfo( file ).Length;
			}
			catch ( IOException )
			{
			}
		}

		return total;
	}

	private static GameUpdate? ReadManifest( string manifestPath )
	{
		try
		{
			Dictionary<string, string> values = new Dictionary<string, string>();

			foreach ( Match match in ValuePattern().Matches( File.ReadAllText( manifestPath ) ) )
			{
				values.TryAdd( match.Groups[ "key" ].Value, match.Groups[ "value" ].Value );
			}

			int flags = int.TryParse( values.GetValueOrDefault( "StateFlags" ), out int parsedFlags ) ? parsedFlags : FullyInstalledFlag;
			long toDownload = long.TryParse( values.GetValueOrDefault( "BytesToDownload" ), out long parsedToDownload ) ? parsedToDownload : 0;
			long downloaded = long.TryParse( values.GetValueOrDefault( "BytesDownloaded" ), out long parsedDownloaded ) ? parsedDownloaded : 0;
			bool isPending = ( flags & FullyInstalledFlag ) == 0 || ( flags & UpdateRequiredFlag ) != 0 || toDownload > downloaded;

			if ( !isPending || !values.TryGetValue( "name", out string? name ) || _ToolPrefixes.Any( prefix => name.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) ) )
			{
				return null;
			}

			string appId = values.GetValueOrDefault( "appid" ) ?? string.Empty;
			string stagingFolder = Path.Combine( Path.GetDirectoryName( manifestPath ) ?? string.Empty, "downloading", appId );
			long stagingBytes = GetFolderSize( stagingFolder );

			return new GameUpdate( appId, name, GamePlatform.Steam, toDownload, downloaded, ( flags & FullyInstalledFlag ) != 0, stagingBytes );
		}
		catch ( IOException )
		{
			return null;
		}
	}
}
