using MateLauncher.Core.Models;

namespace MateLauncher.Core.Abstractions;

public interface IPerformanceMode
{
	Task PrepareAsync( Game game );

	void ApplyToRunningGame( Game game );

	Task RestoreAsync( Game game );
}
