using System.Diagnostics;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;

namespace MateLauncher.Core.Services;

public class LibrarySyncService
{
	private readonly IEnumerable<IGameProvider> _Providers;
	private readonly ILibraryRepository _Repository;
	private readonly ISettingsService _SettingsService;
	private readonly RuntimeOptions _RuntimeOptions;

	public LibrarySyncService( IEnumerable<IGameProvider> providers, ILibraryRepository repository, ISettingsService settingsService, RuntimeOptions runtimeOptions )
	{
		_Providers = providers;
		_Repository = repository;
		_SettingsService = settingsService;
		_RuntimeOptions = runtimeOptions;
	}

	public async Task<IReadOnlyList<Game>> SyncAsync()
	{
		if ( _RuntimeOptions.SafeMode )
		{
			DebugLog.Write( "Safe mode: detection skipped" );
			return await LoadAsync();
		}

		List<Game> detectedGames = new List<Game>();

		foreach ( IGameProvider provider in _Providers )
		{
			Stopwatch stopwatch = Stopwatch.StartNew();

			try
			{
				IReadOnlyList<Game> games = provider.Detect();
				detectedGames.AddRange( games );
				DebugLog.Write( $"{provider.Platform}: {games.Count} game(s) in {stopwatch.ElapsedMilliseconds} ms" );
			}
			catch ( Exception exception )
			{
				DebugLog.Write( $"{provider.Platform}: detection failed{Environment.NewLine}{exception}" );
			}
		}

		await _Repository.SaveDetectedAsync( detectedGames, GetDisabledFolderPaths() );
		return await LoadAsync();
	}

	public async Task<IReadOnlyList<Game>> LoadAsync()
	{
		IReadOnlyList<Game> games = await _Repository.GetAllAsync();
		List<string> disabledPaths = GetDisabledFolderPaths();

		if ( disabledPaths.Count == 0 )
		{
			return games;
		}

		return games.Where( game => game.Platform != GamePlatform.Folder || !IsUnderAny( game.InstallPath, disabledPaths ) ).ToList();
	}

	private List<string> GetDisabledFolderPaths()
	{
		return _SettingsService.Load().GameFolders.Where( folder => !folder.IsEnabled ).Select( folder => folder.Path ).ToList();
	}

	private static bool IsUnderAny( string path, IEnumerable<string> folderPaths )
	{
		return folderPaths.Any( folderPath => path.Equals( folderPath, StringComparison.OrdinalIgnoreCase ) || path.StartsWith( folderPath.TrimEnd( '\\', '/' ) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase ) );
	}
}
