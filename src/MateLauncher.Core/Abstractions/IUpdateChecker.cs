using MateLauncher.Core.Models;

namespace MateLauncher.Core.Abstractions;

public interface IUpdateChecker
{
	IReadOnlyList<GameUpdate> FindPendingUpdates();
}
