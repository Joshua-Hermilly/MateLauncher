using MateLauncher.Core.Models;

namespace MateLauncher.Core.Abstractions;

public interface IPlaytimeTracker
{
	event Action<Guid, long>? PlaytimeChanged;

	event Action<Guid>? SessionStarted;

	event Action<Guid>? SessionEnded;

	void Track( Game game );
}
