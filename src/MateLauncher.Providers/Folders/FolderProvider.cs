using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;
using MateLauncher.Providers.Common;

namespace MateLauncher.Providers.Folders;

public class FolderProvider : IGameProvider
{
	private const int MaxRecursiveDepth = 5;

	private static readonly string[] _IgnoredExecutableMarkers = { "unins", "uninstall", "setup", "vc_redist", "vcredist", "dxsetup", "crashhandler", "crashreport", "redist" };

	private static readonly HashSet<string> _SystemFolderNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
	{
		"Windows", "Program Files", "Program Files (x86)", "ProgramData", "Users", "Recovery", "PerfLogs", "System Volume Information", "Documents and Settings", "Intel", "AMD", "NVIDIA"
	};

	private readonly ISettingsService _SettingsService;

	public GamePlatform Platform => GamePlatform.Folder;

	public FolderProvider( ISettingsService settingsService )
	{
		_SettingsService = settingsService;
	}

	public IReadOnlyList<Game> Detect()
	{
		List<Game> games = new List<Game>();

		foreach ( GameFolder gameFolder in _SettingsService.Load().GameFolders )
		{
			if ( !gameFolder.IsEnabled || !Directory.Exists( gameFolder.Path ) )
			{
				DebugLog.Write( $"Folder skipped: {gameFolder.Path}" );
				continue;
			}

			string tabName = string.IsNullOrWhiteSpace( gameFolder.TabName ) ? new DirectoryInfo( gameFolder.Path ).Name : gameFolder.TabName.Trim();

			try
			{
				foreach ( string candidateFolder in ListCandidateFolders( gameFolder ) )
				{
					string name = CleanName( new DirectoryInfo( candidateFolder ).Name );
					string? executable = ExecutableFinder.FindMainExecutable( candidateFolder, name );

					if ( executable is null )
					{
						continue;
					}

					games.Add( new Game
					{
						Name = name,
						Platform = Platform,
						PlatformGameId = candidateFolder,
						InstallPath = candidateFolder,
						LaunchCommand = executable,
						GroupName = tabName
					} );
				}
			}
			catch ( Exception exception )
			{
				DebugLog.Write( $"Folder scan failed: {gameFolder.Path}{Environment.NewLine}{exception}" );
			}
		}

		return games;
	}

	private static List<string> ListCandidateFolders( GameFolder gameFolder )
	{
		switch ( gameFolder.ScanMode )
		{
			case FolderScanMode.Single:
				return new List<string> { gameFolder.Path };
			case FolderScanMode.Recursive:
				return ListRecursiveFolders( gameFolder.Path, 0 );
			case FolderScanMode.Nested:
				return ListNestedFolders( gameFolder.Path );
			default:
				return EnumerateSafely( gameFolder.Path );
		}
	}

	private static List<string> ListRecursiveFolders( string folder, int depth )
	{
		List<string> candidates = new List<string>();

		if ( depth >= MaxRecursiveDepth )
		{
			return candidates;
		}

		foreach ( string childFolder in EnumerateSafely( folder ) )
		{
			if ( HasLaunchableExecutable( childFolder ) )
			{
				candidates.Add( childFolder );
			}
			else
			{
				candidates.AddRange( ListRecursiveFolders( childFolder, depth + 1 ) );
			}
		}

		return candidates;
	}

	private static bool HasLaunchableExecutable( string folder )
	{
		try
		{
			return Directory.EnumerateFiles( folder, "*.exe", SearchOption.TopDirectoryOnly ).Any( file => !_IgnoredExecutableMarkers.Any( marker => Path.GetFileName( file ).Contains( marker, StringComparison.OrdinalIgnoreCase ) ) );
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException )
		{
			return false;
		}
	}

	private static List<string> ListNestedFolders( string rootFolder )
	{
		List<string> candidates = new List<string>();

		foreach ( string childFolder in EnumerateSafely( rootFolder ) )
		{
			List<string> grandChildren = EnumerateSafely( childFolder ).Where( grandChild => ExecutableFinder.FindMainExecutable( grandChild, CleanName( new DirectoryInfo( grandChild ).Name ) ) is not null ).ToList();

			if ( grandChildren.Count > 0 )
			{
				candidates.AddRange( grandChildren );
			}
			else
			{
				candidates.Add( childFolder );
			}
		}

		return candidates;
	}

	private static List<string> EnumerateSafely( string folder )
	{
		try
		{
			return Directory.EnumerateDirectories( folder ).Where( candidate => !IsSystemFolder( candidate ) ).ToList();
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException )
		{
			return new List<string>();
		}
	}

	private static bool IsSystemFolder( string folder )
	{
		string name = Path.GetFileName( folder.TrimEnd( '\\', '/' ) );

		if ( name.StartsWith( '$' ) || _SystemFolderNames.Contains( name ) )
		{
			return true;
		}

		try
		{
			return ( File.GetAttributes( folder ) & FileAttributes.System ) != 0;
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException )
		{
			return true;
		}
	}

	private static string CleanName( string folderName )
	{
		return string.Join( ' ', folderName.Replace( '_', ' ' ).Split( ' ', StringSplitOptions.RemoveEmptyEntries ) );
	}
}
