namespace MateLauncher.Core.Models;

public class GameFolder
{
	public string Path { get; set; } = string.Empty;
	public string TabName { get; set; } = string.Empty;
	public string? IconPath { get; set; }
	public bool IsEnabled { get; set; } = true;
	public FolderScanMode ScanMode { get; set; } = FolderScanMode.Subfolders;
}
