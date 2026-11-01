using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace MateLauncher.Infrastructure.Persistence;

public class LibraryRepository : ILibraryRepository
{
	private readonly IDbContextFactory<LauncherDbContext> _ContextFactory;

	public LibraryRepository( IDbContextFactory<LauncherDbContext> contextFactory )
	{
		_ContextFactory = contextFactory;
	}

	public async Task<IReadOnlyList<Game>> GetAllAsync()
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		return await context.Games.AsNoTracking().OrderBy( game => game.Name ).ToListAsync();
	}

	private static bool IsUnderAny( string path, IReadOnlyCollection<string> folderPaths )
	{
		return folderPaths.Any( folderPath => path.Equals( folderPath, StringComparison.OrdinalIgnoreCase ) || path.StartsWith( folderPath.TrimEnd( '\\', '/' ) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase ) );
	}

	public async Task SaveDetectedAsync( IEnumerable<Game> detectedGames, IReadOnlyCollection<string> protectedFolderPaths )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		List<Game> existingGames = await context.Games.ToListAsync();
		List<Game> detectedList = detectedGames.ToList();

		foreach ( Game missingFolderGame in existingGames.Where( game => game.Platform == GamePlatform.Folder && !detectedList.Any( detected => detected.Platform == GamePlatform.Folder && detected.PlatformGameId == game.PlatformGameId ) && !IsUnderAny( game.InstallPath, protectedFolderPaths ) ) )
		{
			context.Games.Remove( missingFolderGame );
		}

		foreach ( Game detected in detectedList )
		{
			Game? existing = existingGames.FirstOrDefault( game => game.Platform == detected.Platform && game.PlatformGameId == detected.PlatformGameId );

			if ( existing is null )
			{
				context.Games.Add( detected );
				continue;
			}

			existing.GroupName = detected.GroupName;

			if ( !existing.IsCustomized )
			{
				existing.Name = detected.Name;
				existing.InstallPath = detected.InstallPath;
				existing.LaunchCommand = detected.LaunchCommand;
			}

			bool canReplaceCover = string.IsNullOrEmpty( existing.CoverImagePath ) || Game.IsRemoteCover( existing.CoverImagePath ) || !Game.IsRemoteCover( detected.CoverImagePath );

			if ( detected.CoverImagePath is not null && canReplaceCover )
			{
				existing.CoverImagePath = detected.CoverImagePath;
			}
		}

		await context.SaveChangesAsync();
	}

	public async Task MarkPlayedAsync( Guid gameId )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		Game? game = await context.Games.FindAsync( gameId );

		if ( game is null )
		{
			return;
		}

		game.LastPlayedAt = DateTime.Now;
		await context.SaveChangesAsync();
	}

	public async Task SetFavoriteAsync( Guid gameId, bool isFavorite )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		Game? game = await context.Games.FindAsync( gameId );

		if ( game is null )
		{
			return;
		}

		game.IsFavorite = isFavorite;
		await context.SaveChangesAsync();
	}

	public async Task<long> AddPlaytimeAsync( Guid gameId, long seconds )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		Game? game = await context.Games.FindAsync( gameId );

		if ( game is null )
		{
			return 0;
		}

		game.PlaytimeSeconds += seconds;

		string day = DateTime.Now.ToString( "yyyy-MM-dd" );
		DailyPlaytime? daily = await context.DailyPlaytimes.FindAsync( day, gameId );

		if ( daily is null )
		{
			context.DailyPlaytimes.Add( new DailyPlaytime { Day = day, GameId = gameId, Seconds = seconds } );
		}
		else
		{
			daily.Seconds += seconds;
		}

		await context.SaveChangesAsync();
		return game.PlaytimeSeconds;
	}

	public async Task AddSessionAsync( Guid gameId, DateTime startedAt, long seconds )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		context.GameSessions.Add( new GameSession { GameId = gameId, StartedAt = startedAt, Seconds = seconds } );
		await context.SaveChangesAsync();
	}

	public async Task<int> ApplyImportedPlaytimeAsync( IReadOnlyDictionary<Guid, ImportedPlaytime> updates )
	{
		if ( updates.Count == 0 )
		{
			return 0;
		}

		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		List<Guid> ids = updates.Keys.ToList();
		List<Game> games = await context.Games.Where( game => ids.Contains( game.Id ) ).ToListAsync();

		foreach ( Game game in games )
		{
			ImportedPlaytime update = updates[ game.Id ];
			game.PlaytimeSeconds = Math.Max( game.PlaytimeSeconds, update.Seconds );
			game.LastPlayedAt ??= update.LastPlayedAt;
		}

		await context.SaveChangesAsync();
		return games.Count;
	}

	public async Task<IReadOnlyList<GameSession>> GetSessionsAsync( Guid gameId, int take )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		List<GameSession> sessions = await context.GameSessions.Where( session => session.GameId == gameId ).ToListAsync();
		return sessions.OrderByDescending( session => session.StartedAt ).Take( take ).ToList();
	}

	public async Task<IReadOnlyList<DailyPlaytime>> GetDailyPlaytimeAsync( int days )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		string firstDay = DateTime.Now.Date.AddDays( 1 - days ).ToString( "yyyy-MM-dd" );
		return await context.DailyPlaytimes.AsNoTracking().Where( daily => string.Compare( daily.Day, firstDay ) >= 0 ).ToListAsync();
	}

	public async Task SetSizeAsync( Guid gameId, long sizeBytes )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		await context.Games.Where( game => game.Id == gameId ).ExecuteUpdateAsync( setters => setters.SetProperty( game => game.SizeBytes, sizeBytes ) );
	}

	public async Task SetCategoryAsync( Guid gameId, string? categoryName )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		await context.Games.Where( game => game.Id == gameId ).ExecuteUpdateAsync( setters => setters.SetProperty( game => game.CategoryName, categoryName ) );
	}

	public async Task RenameCategoryAsync( string oldName, string? newName )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		await context.Games.Where( game => game.CategoryName == oldName ).ExecuteUpdateAsync( setters => setters.SetProperty( game => game.CategoryName, newName ) );
	}

	public async Task SetHiddenAsync( Guid gameId, bool isHidden )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		await context.Games.Where( game => game.Id == gameId ).ExecuteUpdateAsync( setters => setters.SetProperty( game => game.IsHidden, isHidden ) );
	}

	public async Task UnhideAllAsync()
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		await context.Games.Where( game => game.IsHidden ).ExecuteUpdateAsync( setters => setters.SetProperty( game => game.IsHidden, false ) );
	}

	public async Task AddAsync( Game game )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		context.Games.Add( game );
		await context.SaveChangesAsync();
	}

	public async Task UpdateAsync( Game game )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		Game? stored = await context.Games.FindAsync( game.Id );

		if ( stored is null )
		{
			return;
		}

		stored.Name = game.Name;
		stored.InstallPath = game.InstallPath;
		stored.LaunchCommand = game.LaunchCommand;
		stored.LaunchArguments = game.LaunchArguments;
		stored.RunAsAdministrator = game.RunAsAdministrator;
		stored.CoverImagePath = game.CoverImagePath;
		stored.IsHidden = game.IsHidden;
		stored.CategoryName = game.CategoryName;
		stored.IsCustomized = game.IsCustomized;
		await context.SaveChangesAsync();
	}

	public async Task RemoveAsync( Guid gameId )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		await context.Games.Where( game => game.Id == gameId ).ExecuteDeleteAsync();
	}

	public async Task SetCoverAsync( Guid gameId, string coverImagePath )
	{
		await using LauncherDbContext context = await _ContextFactory.CreateDbContextAsync();
		Game? game = await context.Games.FindAsync( gameId );

		if ( game is null )
		{
			return;
		}

		game.CoverImagePath = coverImagePath;
		await context.SaveChangesAsync();
	}
}
