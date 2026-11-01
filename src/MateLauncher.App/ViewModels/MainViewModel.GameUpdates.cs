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
	[RelayCommand]
	public async Task StartDownloadAsync( UpdateItemViewModel? item )
	{
		if ( item is null )
		{
			return;
		}

		if ( !IsSteamRunning )
		{
			OpenClient( "steam://open/main" );
			await Task.Delay( 6000 );
		}

		OpenClient( item.StartUri );
		DownloadsMessage = T( "Downloads_RequestSent" );
		await Task.Delay( 1500 );
		OpenClient( "steam://nav/downloads" );
	}

	[RelayCommand]
	public void OpenClient( string? uri )
	{
		if ( string.IsNullOrWhiteSpace( uri ) )
		{
			return;
		}

		try
		{
			Process.Start( new ProcessStartInfo( uri ) { UseShellExecute = true } );
		}
		catch ( System.ComponentModel.Win32Exception )
		{
		}
	}

	[RelayCommand]
	public async Task RefreshUpdatesAsync()
	{
		UpdateSteamStatus();
		IReadOnlyList<GameUpdate> updates = await Task.Run( _UpdateChecker.FindPendingUpdates );
		PendingUpdates.Clear();
		DateTime now = DateTime.UtcNow;
		double elapsedSeconds = Math.Max( 0.5, ( now - _LastUpdatesPollAt ).TotalSeconds );
		_LastUpdatesPollAt = now;

		foreach ( GameUpdate update in updates.OrderBy( update => update.GameName ) )
		{
			bool hasPrevious = _LastDownloadedBytes.TryGetValue( update.AppId, out ( long Manifest, long Staging ) previous );

			if ( !_DownloadBaselines.TryGetValue( update.AppId, out ( long Manifest, long Staging ) baseline ) )
			{
				baseline = ( update.BytesDownloaded, update.StagingBytes );
				_DownloadBaselines[ update.AppId ] = baseline;
			}

			long liveBytes = baseline.Manifest + Math.Max( 0, update.StagingBytes - baseline.Staging );
			long shownBytes = Math.Min( update.BytesToDownload > 0 ? update.BytesToDownload : long.MaxValue, Math.Max( update.BytesDownloaded, liveBytes ) );
			long grown = Math.Max( update.StagingBytes - previous.Staging, update.BytesDownloaded - previous.Manifest );
			bool isDownloading = hasPrevious && grown > 0;
			double bytesPerSecond = isDownloading ? grown / elapsedSeconds : 0;
			_LastDownloadedBytes[ update.AppId ] = ( update.BytesDownloaded, update.StagingBytes );
			PendingUpdates.Add( new UpdateItemViewModel( update with { BytesDownloaded = shownBytes }, isDownloading, bytesPerSecond ) );
		}

		HasNoUpdates = PendingUpdates.Count == 0;
	}
}
