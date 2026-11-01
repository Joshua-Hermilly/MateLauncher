using MateLauncher.App.Localization;
using MateLauncher.Core.Models;

namespace MateLauncher.App.ViewModels;

public class UpdateItemViewModel
{
	public string AppId { get; }
	public long BytesDownloaded { get; }
	public string Name { get; }
	public string PlatformLabel { get; }
	public string StatusText { get; }
	public string ProgressText { get; }
	public double Progress { get; }
	public bool IsDownloading { get; }
	public string StartUri { get; }

	public UpdateItemViewModel( GameUpdate update, bool isDownloading, double bytesPerSecond )
	{
		AppId = update.AppId;
		BytesDownloaded = update.BytesDownloaded;
		IsDownloading = isDownloading;
		Name = update.GameName;
		PlatformLabel = GameCardViewModel.GetPlatformLabel( update.Platform );
		StartUri = update.IsInstalled ? $"steam://validate/{update.AppId}" : $"steam://install/{update.AppId}";
		StatusText = Localizer.Instance.Get( isDownloading ? "Downloads_Downloading" : update.IsInstalled ? "Downloads_Update" : "Downloads_Install" );
		Progress = update.BytesToDownload <= 0 ? 0 : Math.Min( 100, update.BytesDownloaded * 100.0 / update.BytesToDownload );

		string speed = isDownloading && bytesPerSecond > 0 ? $" · {GameCardViewModel.FormatSize( ( long ) bytesPerSecond )}/s" : string.Empty;
		ProgressText = update.BytesToDownload <= 0
			? string.Empty
			: $"{GameCardViewModel.FormatSize( update.BytesDownloaded )} / {GameCardViewModel.FormatSize( update.BytesToDownload )} · {Progress:0.0} %{speed}";
	}
}
