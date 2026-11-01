using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;

namespace MateLauncher.Tests.Core;

public class LibrarySyncServiceTests
{
	private sealed class FakeProvider : IGameProvider
	{
		private readonly Func<IReadOnlyList<Game>> _Detect;

		public FakeProvider( GamePlatform platform, Func<IReadOnlyList<Game>> detect )
		{
			Platform = platform;
			_Detect = detect;
		}

		public GamePlatform Platform { get; }

		public IReadOnlyList<Game> Detect()
		{
			return _Detect();
		}
	}

	private sealed class FakeSettings : ISettingsService
	{
		public AppSettings Settings { get; } = new AppSettings();

		public AppSettings Load()
		{
			return Settings;
		}

		public void Save( AppSettings settings )
		{
		}
	}

	private sealed class FakeRepository : ILibraryRepository
	{
		public List<Game> Stored { get; set; } = new List<Game>();
		public List<Game> LastDetected { get; private set; } = new List<Game>();
		public IReadOnlyCollection<string> LastProtectedPaths { get; private set; } = Array.Empty<string>();

		public Task<IReadOnlyList<Game>> GetAllAsync()
		{
			return Task.FromResult<IReadOnlyList<Game>>( Stored );
		}

		public Task SaveDetectedAsync( IEnumerable<Game> detectedGames, IReadOnlyCollection<string> protectedFolderPaths )
		{
			LastDetected = detectedGames.ToList();
			LastProtectedPaths = protectedFolderPaths;
			return Task.CompletedTask;
		}

		public Task MarkPlayedAsync( Guid gameId ) => Task.CompletedTask;
		public Task SetFavoriteAsync( Guid gameId, bool isFavorite ) => Task.CompletedTask;
		public Task SetCoverAsync( Guid gameId, string coverImagePath ) => Task.CompletedTask;
		public Task SetSizeAsync( Guid gameId, long sizeBytes ) => Task.CompletedTask;
		public Task SetCategoryAsync( Guid gameId, string? categoryName ) => Task.CompletedTask;
		public Task RenameCategoryAsync( string oldName, string? newName ) => Task.CompletedTask;
		public Task SetHiddenAsync( Guid gameId, bool isHidden ) => Task.CompletedTask;
		public Task UnhideAllAsync() => Task.CompletedTask;
		public Task<long> AddPlaytimeAsync( Guid gameId, long seconds ) => Task.FromResult( 0L );
		public Task AddSessionAsync( Guid gameId, DateTime startedAt, long seconds ) => Task.CompletedTask;
		public Task<int> ApplyImportedPlaytimeAsync( IReadOnlyDictionary<Guid, ImportedPlaytime> updates ) => Task.FromResult( 0 );
		public Task<IReadOnlyList<GameSession>> GetSessionsAsync( Guid gameId, int take ) => Task.FromResult<IReadOnlyList<GameSession>>( Array.Empty<GameSession>() );
		public Task<IReadOnlyList<DailyPlaytime>> GetDailyPlaytimeAsync( int days ) => Task.FromResult<IReadOnlyList<DailyPlaytime>>( Array.Empty<DailyPlaytime>() );
		public Task AddAsync( Game game ) => Task.CompletedTask;
		public Task UpdateAsync( Game game ) => Task.CompletedTask;
		public Task RemoveAsync( Guid gameId ) => Task.CompletedTask;
	}

	private static Game CreateGame( string name, GamePlatform platform, string installPath = "" )
	{
		return new Game { Name = name, Platform = platform, InstallPath = installPath };
	}

	[Fact]
	public async Task SyncAsync_MergesEveryProviderAndSurvivesAFailingOne()
	{
		FakeRepository repository = new FakeRepository();
		List<IGameProvider> providers = new List<IGameProvider>
		{
			new FakeProvider( GamePlatform.Steam, () => new[] { CreateGame( "A", GamePlatform.Steam ) } ),
			new FakeProvider( GamePlatform.Epic, () => throw new InvalidOperationException() ),
			new FakeProvider( GamePlatform.Gog, () => new[] { CreateGame( "B", GamePlatform.Gog ) } )
		};
		LibrarySyncService service = new LibrarySyncService( providers, repository, new FakeSettings(), new RuntimeOptions() );

		await service.SyncAsync();

		Assert.Equal( new[] { "A", "B" }, repository.LastDetected.Select( game => game.Name ).ToArray() );
	}

	[Fact]
	public async Task SyncAsync_SkipsDetectionInSafeMode()
	{
		FakeRepository repository = new FakeRepository();
		bool detected = false;
		List<IGameProvider> providers = new List<IGameProvider> { new FakeProvider( GamePlatform.Steam, () => { detected = true; return Array.Empty<Game>(); } ) };
		LibrarySyncService service = new LibrarySyncService( providers, repository, new FakeSettings(), new RuntimeOptions { SafeMode = true } );

		await service.SyncAsync();

		Assert.False( detected );
	}

	[Fact]
	public async Task LoadAsync_HidesGamesFromDisabledFolders()
	{
		string disabledFolder = Path.Combine( Path.GetTempPath(), "DisabledFolder" );
		FakeRepository repository = new FakeRepository
		{
			Stored = new List<Game>
			{
				CreateGame( "InDisabled", GamePlatform.Folder, Path.Combine( disabledFolder, "Game" ) ),
				CreateGame( "Elsewhere", GamePlatform.Folder, Path.Combine( Path.GetTempPath(), "Other", "Game" ) ),
				CreateGame( "SteamGame", GamePlatform.Steam, Path.Combine( disabledFolder, "SteamGame" ) )
			}
		};
		FakeSettings settings = new FakeSettings();
		settings.Settings.GameFolders.Add( new GameFolder { Path = disabledFolder, IsEnabled = false } );
		LibrarySyncService service = new LibrarySyncService( Array.Empty<IGameProvider>(), repository, settings, new RuntimeOptions() );

		IReadOnlyList<Game> games = await service.LoadAsync();

		Assert.Equal( new[] { "Elsewhere", "SteamGame" }, games.Select( game => game.Name ).ToArray() );
	}
}
