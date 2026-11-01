using MateLauncher.Core.Models;
using MateLauncher.Providers.Common;
using MateLauncher.Providers.Steam;

namespace MateLauncher.Providers.Saves;

public record SaveLocation( string Path, string Source, long SizeBytes, DateTime? LastWriteTime );

public record SaveSearchRoot( string Path, string Label );

public static class SaveLocationFinder
{
	private const int MaximumDepth = 2;
	private const int MaximumFilesCounted = 20000;
	private const int MinimumFuzzyLength = 6;

	private static readonly HashSet<string> _IgnoredFolders = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
	{
		"Microsoft", "Packages", "Temp", "Google", "Mozilla", "npm", "NuGet", "Programs", "Package Cache", "D3DSCache", "CrashDumps", "ConnectedDevices", "Comms", "Publishers", "Adobe", "Packages", "Low", "Steam", "Epic", "EpicGamesLauncher"
	};

	public static IReadOnlyList<SaveSearchRoot> GetDefaultRoots()
	{
		string appData = Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData );
		string localAppData = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
		string profile = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
		string documents = Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments );

		return new List<SaveSearchRoot>
		{
			new SaveSearchRoot( appData, @"AppData\Roaming" ),
			new SaveSearchRoot( localAppData, @"AppData\Local" ),
			new SaveSearchRoot( Path.Combine( localAppData, "..", "LocalLow" ), @"AppData\LocalLow" ),
			new SaveSearchRoot( documents, "Documents" ),
			new SaveSearchRoot( Path.Combine( profile, "Saved Games" ), "Saved Games" )
		};
	}

	public static IReadOnlyList<SaveLocation> Find( Game game )
	{
		List<SaveLocation> locations = FindInRoots( GetDefaultRoots(), game.Name, GetInstallFolderName( game ) ).ToList();

		if ( game.Platform == GamePlatform.Steam && game.PlatformGameId.All( char.IsDigit ) )
		{
			locations.AddRange( FindSteamCloudSaves( game.PlatformGameId ) );
		}

		return locations.GroupBy( location => location.Path, StringComparer.OrdinalIgnoreCase ).Select( group => group.First() ).OrderByDescending( location => location.LastWriteTime ).ToList();
	}

	public static IEnumerable<SaveLocation> FindInRoots( IEnumerable<SaveSearchRoot> roots, string gameName, string? installFolderName )
	{
		string normalizedName = ExecutableFinder.Normalize( gameName );
		string normalizedInstall = installFolderName is null ? string.Empty : ExecutableFinder.Normalize( installFolderName );

		if ( normalizedName.Length == 0 )
		{
			yield break;
		}

		foreach ( SaveSearchRoot root in roots )
		{
			string rootPath = System.IO.Path.GetFullPath( root.Path );

			if ( !Directory.Exists( rootPath ) )
			{
				continue;
			}

			foreach ( string match in FindMatchingFolders( rootPath, normalizedName, normalizedInstall, 1 ) )
			{
				yield return Measure( match, root.Label );
			}
		}
	}

	public static bool IsMatch( string folderName, string normalizedGameName, string normalizedInstallName )
	{
		string normalizedFolder = ExecutableFinder.Normalize( folderName );

		if ( normalizedFolder.Length < 4 )
		{
			return false;
		}

		if ( normalizedFolder == normalizedGameName || ( normalizedInstallName.Length > 0 && normalizedFolder == normalizedInstallName ) )
		{
			return true;
		}

		return normalizedGameName.Length >= MinimumFuzzyLength && normalizedFolder.Length >= MinimumFuzzyLength && ( normalizedFolder.Contains( normalizedGameName ) || normalizedGameName.Contains( normalizedFolder ) );
	}

	private static IEnumerable<string> FindMatchingFolders( string folder, string normalizedName, string normalizedInstall, int depth )
	{
		IEnumerable<string> children;

		try
		{
			children = Directory.EnumerateDirectories( folder ).ToList();
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException )
		{
			yield break;
		}

		foreach ( string child in children )
		{
			string name = System.IO.Path.GetFileName( child );

			if ( IsMatch( name, normalizedName, normalizedInstall ) )
			{
				yield return child;
				continue;
			}

			if ( depth >= MaximumDepth || _IgnoredFolders.Contains( name ) )
			{
				continue;
			}

			foreach ( string nested in FindMatchingFolders( child, normalizedName, normalizedInstall, depth + 1 ) )
			{
				yield return nested;
			}
		}
	}

	private static IEnumerable<SaveLocation> FindSteamCloudSaves( string appId )
	{
		string? steamPath = SteamProvider.FindSteamPath();

		if ( steamPath is null )
		{
			yield break;
		}

		string userData = System.IO.Path.Combine( steamPath, "userdata" );

		if ( !Directory.Exists( userData ) )
		{
			yield break;
		}

		foreach ( string user in Directory.EnumerateDirectories( userData ) )
		{
			string appFolder = System.IO.Path.Combine( user, appId );

			if ( Directory.Exists( appFolder ) )
			{
				yield return Measure( appFolder, "Steam Cloud" );
			}
		}
	}

	private static string? GetInstallFolderName( Game game )
	{
		return string.IsNullOrWhiteSpace( game.InstallPath ) ? null : System.IO.Path.GetFileName( game.InstallPath.TrimEnd( '\\', '/' ) );
	}

	private static SaveLocation Measure( string folder, string source )
	{
		long size = 0;
		DateTime? lastWrite = null;
		int counted = 0;

		try
		{
			foreach ( FileInfo file in new DirectoryInfo( folder ).EnumerateFiles( "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true } ) )
			{
				size += file.Length;

				if ( lastWrite is null || file.LastWriteTime > lastWrite )
				{
					lastWrite = file.LastWriteTime;
				}

				if ( ++counted >= MaximumFilesCounted )
				{
					break;
				}
			}
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException )
		{
		}

		return new SaveLocation( folder, source, size, lastWrite );
	}
}
