using MateLauncher.Core.Models;

namespace MateLauncher.Core.Abstractions;

public interface IGameProvider
{
	GamePlatform Platform { get; }

	IReadOnlyList<Game> Detect();
}
