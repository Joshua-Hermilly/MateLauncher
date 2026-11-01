namespace MateLauncher.Core.Models;

public class AppSettings
{
	public double CardWidth { get; set; } = 190;
	public bool IsListView { get; set; }
	public string ThemeId { get; set; } = "matezone";
	public string? AccentColor { get; set; }
	public bool AnimationsEnabled { get; set; } = true;
	public int TranslucencyLevel { get; set; } = 60;
	public string Language { get; set; } = "fr";
	public bool OverlayEnabled { get; set; } = true;
	public string OverlayHotkey { get; set; } = "Ctrl+Alt+O";
	public double? OverlayLeft { get; set; }
	public double? OverlayTop { get; set; }
	public bool MinimizeToTray { get; set; }
	public bool StartWithWindows { get; set; }
	public GameSortMode SortMode { get; set; } = GameSortMode.Name;
	public FolderSortMode FolderSort { get; set; } = FolderSortMode.Name;
	public bool MergeFolders { get; set; }
	public bool IncludeGameNews { get; set; } = true;
	public bool NewsFeedsInitialized { get; set; }
	public DateTime? LastBackupAt { get; set; }
	public string GoogleApiKey { get; set; } = string.Empty;
	public string SteamApiKey { get; set; } = string.Empty;
	public string SteamGridDbApiKey { get; set; } = string.Empty;
	public string IgdbClientId { get; set; } = string.Empty;
	public string IgdbClientSecret { get; set; } = string.Empty;
	public string SteamId { get; set; } = string.Empty;
	public string GoogleSearchEngineId { get; set; } = string.Empty;
	public bool StoreCoversRefreshed { get; set; }
	public bool CheckUpdatesOnStartup { get; set; } = true;
	public bool PerformanceModeEnabled { get; set; }
	public List<string> PerformancePrograms { get; set; } = new List<string>();
	public List<CustomTheme> CustomThemes { get; set; } = new List<CustomTheme>();
	public List<NewsFeed> NewsFeeds { get; set; } = new List<NewsFeed>();
	public List<GameFolder> GameFolders { get; set; } = new List<GameFolder>();
	public List<string> Categories { get; set; } = new List<string>();
	public Dictionary<string, string> CategoryColors { get; set; } = new Dictionary<string, string>();
}
