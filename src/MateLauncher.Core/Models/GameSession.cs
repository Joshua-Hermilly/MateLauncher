namespace MateLauncher.Core.Models;

public class GameSession
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid GameId { get; set; }
	public DateTime StartedAt { get; set; }
	public long Seconds { get; set; }
}
