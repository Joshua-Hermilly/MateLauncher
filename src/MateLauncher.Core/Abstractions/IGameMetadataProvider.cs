using MateLauncher.Core.Models;

namespace MateLauncher.Core.Abstractions;

public interface IGameMetadataProvider
{
	Task<GameMetadata?> GetMetadataAsync( string gameName );
}
