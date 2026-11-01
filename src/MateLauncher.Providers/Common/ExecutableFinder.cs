using System.Text.RegularExpressions;

namespace MateLauncher.Providers.Common;

public static partial class ExecutableFinder
{
	private const int MaximumDepth = 2;

	[GeneratedRegex( "unins|setup|redist|crash|installer|updater|dotnet|vcredist|dxsetup|cefsharp|helper|report|diagnostic|prerequisite|support|notification", RegexOptions.IgnoreCase )]
	private static partial Regex ExcludedNamePattern();

	public static string? FindMainExecutable( string folder, string gameName )
	{
		if ( !Directory.Exists( folder ) )
		{
			return null;
		}

		string launchHelper = Path.Combine( folder, "gamelaunchhelper.exe" );

		if ( File.Exists( launchHelper ) )
		{
			return launchHelper;
		}

		string normalizedGameName = Normalize( gameName );
		List<( string Path, int Depth )> candidates = new List<( string Path, int Depth )>();
		CollectExecutables( folder, 0, candidates );

		return candidates
			.Where( candidate => !ExcludedNamePattern().IsMatch( Path.GetFileNameWithoutExtension( candidate.Path ) ) )
			.OrderByDescending( candidate => Score( candidate.Path, candidate.Depth, normalizedGameName ) )
			.Select( candidate => candidate.Path )
			.FirstOrDefault();
	}

	private static void CollectExecutables( string folder, int depth, List<( string Path, int Depth )> candidates )
	{
		try
		{
			foreach ( string file in Directory.EnumerateFiles( folder, "*.exe" ) )
			{
				candidates.Add( ( file, depth ) );
			}

			if ( depth >= MaximumDepth )
			{
				return;
			}

			foreach ( string subFolder in Directory.EnumerateDirectories( folder ) )
			{
				CollectExecutables( subFolder, depth + 1, candidates );
			}
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException )
		{
		}
	}

	private static long Score( string path, int depth, string normalizedGameName )
	{
		string normalizedFileName = Normalize( Path.GetFileNameWithoutExtension( path ) );
		bool nameMatches = normalizedFileName.Length > 2 && ( normalizedGameName.Contains( normalizedFileName ) || normalizedFileName.Contains( normalizedGameName ) );
		long size = 0;

		try
		{
			size = new FileInfo( path ).Length;
		}
		catch ( IOException )
		{
		}

		return ( nameMatches ? 4_000_000_000_000 : 0 ) + ( ( MaximumDepth - depth ) * 100_000_000_000 ) + size;
	}

	public static string Normalize( string value )
	{
		return new string( value.Where( char.IsLetterOrDigit ).Select( char.ToLowerInvariant ).ToArray() );
	}
}
