namespace MateLauncher.Core.Models;

public class Game
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public GamePlatform Platform { get; set; }
	public string PlatformGameId { get; set; } = string.Empty;
	public string InstallPath { get; set; } = string.Empty;
	public string LaunchCommand { get; set; } = string.Empty;
	public string LaunchArguments { get; set; } = string.Empty;
	public bool RunAsAdministrator { get; set; }
	public string? CoverImagePath { get; set; }
	public long PlaytimeSeconds { get; set; }
	public DateTime? LastPlayedAt { get; set; }
	public bool IsFavorite { get; set; }
	public bool IsHidden { get; set; }
	public string? GroupName { get; set; }
	public bool IsCustomized { get; set; }
	public long SizeBytes { get; set; }
	public string? CategoryName { get; set; }

	public static bool IsRemoteCover( string? coverImagePath )
	{
		return coverImagePath is not null && coverImagePath.StartsWith( "http", StringComparison.OrdinalIgnoreCase );
	}
}
