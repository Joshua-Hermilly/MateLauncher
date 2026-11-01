namespace MateLauncher.Core.Models;

public class DailyPlaytime
{
	public string Day { get; set; } = string.Empty;
	public Guid GameId { get; set; }
	public long Seconds { get; set; }
}
