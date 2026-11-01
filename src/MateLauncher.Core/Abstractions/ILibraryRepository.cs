using MateLauncher.Core.Models;

namespace MateLauncher.Core.Abstractions;

public interface ILibraryRepository
{
	Task<IReadOnlyList<Game>> GetAllAsync();

	Task SaveDetectedAsync( IEnumerable<Game> detectedGames, IReadOnlyCollection<string> protectedFolderPaths );

	Task MarkPlayedAsync( Guid gameId );

	Task SetFavoriteAsync( Guid gameId, bool isFavorite );

	Task SetCoverAsync( Guid gameId, string coverImagePath );

	Task SetSizeAsync( Guid gameId, long sizeBytes );

	Task SetCategoryAsync( Guid gameId, string? categoryName );

	Task RenameCategoryAsync( string oldName, string? newName );

	Task SetHiddenAsync( Guid gameId, bool isHidden );

	Task UnhideAllAsync();

	Task<long> AddPlaytimeAsync( Guid gameId, long seconds );

	Task<IReadOnlyList<DailyPlaytime>> GetDailyPlaytimeAsync( int days );

	Task AddAsync( Game game );

	Task UpdateAsync( Game game );

	Task RemoveAsync( Guid gameId );
}
