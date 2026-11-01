using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MateLauncher.App.Icons;
using MateLauncher.App.Localization;
using MateLauncher.App.Themes;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;
using MateLauncher.Infrastructure.Achievements;
using MateLauncher.Infrastructure.Backup;
using MateLauncher.Providers.Steam;
using MateLauncher.Infrastructure.Covers;
using MateLauncher.App.Icons;
using MateLauncher.Infrastructure.Downloads;
using MateLauncher.Infrastructure.News;
using MateLauncher.Infrastructure.Storage;
using MateLauncher.Infrastructure.Updates;
using MateLauncher.App.Startup;
using Microsoft.Win32;

namespace MateLauncher.App.ViewModels;

public partial class MainViewModel
{
	public string CurrentVersion { get; } = ReadCurrentVersion();

	partial void OnCheckUpdatesOnStartupChanged( bool value )
	{
		SaveSettings();
	}

	private static string ReadCurrentVersion()
	{
		string? path = Environment.ProcessPath;
		string? version = path is null ? null : FileVersionInfo.GetVersionInfo( path ).ProductVersion;
		return version?.Split( '+' )[ 0 ] ?? "0.0.0";
	}

	[RelayCommand]
	public async Task CheckForUpdatesAsync()
	{
		await RunUpdateCheckAsync();
	}

	public async Task CheckUpdatesOnStartupAsync()
	{
		if ( CheckUpdatesOnStartup && !_RuntimeOptions.SafeMode )
		{
			await RunUpdateCheckAsync();
		}
	}

	private async Task RunUpdateCheckAsync()
	{
		if ( IsUpdating )
		{
			return;
		}

				UpdateStatusText = T( "Update_Checking" );
		UpdateCheckResult result = await AppUpdateService.CheckAsync( AppUpdateService.Repository, CurrentVersion );
		_PendingUpdate = result.Update;
		IsUpdateAvailable = result.Status == UpdateCheckStatus.Available;
		IsUpdateBannerVisible = IsUpdateAvailable;
		UpdateBannerText = result.Update is null ? string.Empty : string.Format( T( "Update_Banner" ), result.Update.Version );
		UpdateStatusText = result.Status switch
		{
			UpdateCheckStatus.Available => string.Format( T( "Update_Available" ), result.Update!.Version ),
			UpdateCheckStatus.UpToDate => T( "Update_UpToDate" ),
			UpdateCheckStatus.NotFound => T( "Update_NotFound" ),
			UpdateCheckStatus.NoInstaller => T( "Update_NoInstaller" ),
			UpdateCheckStatus.NoChecksum => T( "Update_NoChecksum" ),
			UpdateCheckStatus.InvalidRepository => T( "Update_Invalid" ),
			_ => T( "Update_Failed" )
		};
	}

	[RelayCommand]
	public void DismissUpdateBanner()
	{
		IsUpdateBannerVisible = false;
	}

	[RelayCommand]
	public async Task InstallUpdateAsync()
	{
		if ( _PendingUpdate is null || IsUpdating )
		{
			return;
		}

		if ( !ConfirmDialog.Ask( T( "Update_Install" ), T( "Update_Confirm" ), T( "Update_Install" ) ) )
		{
			return;
		}

		IsUpdating = true;
		UpdateProgress = 0;
		UpdateStatusText = T( "Update_Downloading" );

		try
		{
			string installerPath = await AppUpdateService.DownloadAsync( _PendingUpdate, new Progress<double>( value => UpdateProgress = value ) );
			Process.Start( new ProcessStartInfo( installerPath, "/update" ) { UseShellExecute = true } );
			Application.Current.Shutdown();
		}
		catch ( Exception exception ) when ( exception is System.Net.Http.HttpRequestException or System.IO.IOException or InvalidOperationException or TaskCanceledException or System.ComponentModel.Win32Exception )
		{
			UpdateStatusText = T( "Update_Failed" );
			IsUpdating = false;
		}
	}
}
