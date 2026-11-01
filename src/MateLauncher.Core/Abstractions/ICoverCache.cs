using MateLauncher.Core.Models;

namespace MateLauncher.Core.Abstractions;

public interface ICoverCache
{
	Task<string?> CacheAsync( Game game );

	Task<string?> ImportAsync( Game game, string sourceFile );
}
