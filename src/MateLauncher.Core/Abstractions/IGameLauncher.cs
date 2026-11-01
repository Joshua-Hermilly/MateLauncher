using MateLauncher.Core.Models;

namespace MateLauncher.Core.Abstractions;

public interface IGameLauncher
{
	Task LaunchAsync( Game game );

	Task StopAsync( Game game );
}
