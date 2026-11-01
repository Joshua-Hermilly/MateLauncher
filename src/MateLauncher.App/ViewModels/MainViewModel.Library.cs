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
	private void OnPlaytimeChanged( Guid gameId, long totalSeconds )
	{
		Application.Current.Dispatcher.Invoke( async () =>
		{
			GameCardViewModel? card = _AllCards.FirstOrDefault( candidate => candidate.Game.Id == gameId );

			if ( card is not null )
			{
				card.PlaytimeSeconds = totalSeconds;
				card.Game.PlaytimeSeconds = totalSeconds;
			}

			if ( IsStatsOpen )
			{
				await LoadStatsAsync();
			}

			RefreshHome();

			if ( SelectedGame is not null && SelectedGame.Game.Id == gameId )
			{
				await LoadSessionsAsync( SelectedGame );
			}
		} );
	}

	private void OnSessionStarted( Guid gameId )
	{
		Application.Current.Dispatcher.Invoke( () =>
		{
			ActiveGame = _AllCards.FirstOrDefault( candidate => candidate.Game.Id == gameId );
			SessionStartedAt = DateTime.Now;
			_RunningGameIds.Add( gameId );

			if ( ActiveGame is not null )
			{
				ActiveGame.IsRunning = true;
			}
		} );
	}

	private void OnSessionEnded( Guid gameId )
	{
		Application.Current.Dispatcher.Invoke( () =>
		{
			_RunningGameIds.Remove( gameId );

			foreach ( GameCardViewModel card in _AllCards.Where( candidate => candidate.Game.Id == gameId ) )
			{
				card.IsRunning = false;
			}

			if ( ActiveGame?.Game.Id == gameId )
			{
				ActiveGame = null;
				SessionStartedAt = null;
			}
		} );
	}

	public async Task<long> GetTodaySecondsAsync()
	{
		IReadOnlyList<DailyPlaytime> today = await _Repository.GetDailyPlaytimeAsync( 1 );
		return today.Sum( daily => daily.Seconds );
	}

	[RelayCommand]
	public async Task OpenAddChoiceAsync()
	{
		AddChoiceWindow window = new AddChoiceWindow { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() != true )
		{
			return;
		}

		if ( window.Choice == AddChoice.Game )
		{
			await AddGameDialogAsync();
		}
		else
		{
			await AddFolderAsync();
		}
	}

	[RelayCommand]
	public async Task AddGameDialogAsync()
	{
		AddGameWindow window = new AddGameWindow { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() == true && window.NewGame is not null )
		{
			await AddManualGameAsync( window.NewGame, window.CoverSourceFile );
		}
	}

	[RelayCommand]
	public void OpenDetail( GameCardViewModel? card )
	{
		IsSettingsOpen = false;
		SelectedGame = card;
	}

	[RelayCommand]
	public void CloseDetail()
	{
		SelectedGame = null;
		IsSettingsOpen = false;
	}

	[RelayCommand]
	public void OpenInstallFolder( GameCardViewModel? card )
	{
		if ( card is not null && card.HasInstallPath )
		{
			Process.Start( new ProcessStartInfo( card.InstallPath ) { UseShellExecute = true } );
		}
	}

	public void UpdateChartWidth( double width )
	{
		if ( width <= 0 || Math.Abs( width - _ChartWidth ) < 8 )
		{
			return;
		}

		_ChartWidth = width;

		if ( IsStatsOpen )
		{
			_ = LoadStatsAsync();
		}
	}

	private async Task LoadStatsAsync()
	{
		IReadOnlyList<DailyPlaytime> daily = await _Repository.GetDailyPlaytimeAsync( 14 );
		Stats = StatsViewModel.Build( _AllCards.Where( card => !card.Game.IsHidden ).ToList(), daily, _ChartWidth );
	}

	[RelayCommand]
	public async Task SyncAsync()
	{
		IReadOnlyList<Game> games = await _SyncService.SyncAsync();
		_AllCards = games.Select( CreateCard ).ToList();
		RebuildNavigation();
		ApplyFilter();

		if ( _RuntimeOptions.SafeMode )
		{
			return;
		}

		await FindMissingCoversAsync();
		await DownloadRemoteCoversAsync();
		await ComputeMissingSizesAsync();
	}

	public async Task AddManualGameAsync( Game game, string? coverSourceFile )
	{
		game.Platform = GamePlatform.Manual;
		game.PlatformGameId = game.Id.ToString();

		if ( !string.IsNullOrWhiteSpace( coverSourceFile ) )
		{
			game.CoverImagePath = await _CoverCache.ImportAsync( game, coverSourceFile );
		}

		await _Repository.AddAsync( game );
		await RefreshAsync();
		await FindMissingCoversAsync();
		await DownloadRemoteCoversAsync();
		await ComputeMissingSizesAsync();
	}

	[RelayCommand]
	public async Task ConfigureGameAsync( GameCardViewModel? card )
	{
		if ( card is null )
		{
			return;
		}

		GameSettingsWindow window = new GameSettingsWindow( card.Game, _CoverFinder, _Categories.ToList() ) { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() != true || window.EditedGame is null )
		{
			return;
		}

		Game edited = window.EditedGame;
		string? cover = edited.CoverImagePath;

		if ( window.ResetRequested )
		{
			edited.IsCustomized = false;
		}
		else if ( Game.IsRemoteCover( cover ) )
		{
			edited.CoverImagePath = await _CoverCache.CacheAsync( edited ) ?? cover;
		}
		else if ( !string.IsNullOrWhiteSpace( cover ) && cover != card.Game.CoverImagePath && File.Exists( cover ) )
		{
			edited.CoverImagePath = await _CoverCache.ImportAsync( edited, cover ) ?? card.Game.CoverImagePath;
		}

		await _Repository.UpdateAsync( edited );
		SelectedGame = null;

		if ( window.ResetRequested )
		{
			await SyncAsync();
			return;
		}

		await RefreshAsync();
	}

	[RelayCommand]
	public async Task RemoveGameAsync( GameCardViewModel? card )
	{
		if ( card is null )
		{
			return;
		}

		bool confirmed = ConfirmDialog.Ask( T( "Msg_RemoveTitle" ), string.Format( T( "Msg_RemoveGame" ), card.Name ), T( "Btn_Remove" ) );

		if ( !confirmed )
		{
			return;
		}

		if ( card.IsManual )
		{
			await _Repository.RemoveAsync( card.Game.Id );
		}
		else
		{
			await _Repository.SetHiddenAsync( card.Game.Id, true );
		}

		SelectedGame = null;
		await RefreshAsync();
	}

	[RelayCommand]
	public async Task UnhideAllAsync()
	{
		await _Repository.UnhideAllAsync();
		await RefreshAsync();
	}

	private async Task RefreshAsync()
	{
		IReadOnlyList<Game> games = await _SyncService.LoadAsync();
		_AllCards = games.Select( CreateCard ).ToList();
		RebuildNavigation();
		ApplyFilter();
		RefreshHome();
	}

	[RelayCommand]
	public async Task LaunchGameAsync( GameCardViewModel? card )
	{
		if ( card is null )
		{
			return;
		}

		try
		{
			await _GameLauncher.LaunchAsync( card.Game );
			card.LastPlayedAt = DateTime.Now;
			ApplyFilter();
		}
		catch ( Exception exception )
		{
			MessageBox.Show( exception.Message, T( "Msg_LaunchFailed" ), MessageBoxButton.OK, MessageBoxImage.Warning );
		}
	}

	[RelayCommand]
	public async Task StopGameAsync( GameCardViewModel? card )
	{
		if ( card is not null )
		{
			await _GameLauncher.StopAsync( card.Game );
		}
	}

	[RelayCommand]
	public async Task ToggleFavoriteAsync( GameCardViewModel? card )
	{
		if ( card is null )
		{
			return;
		}

		bool isFavorite = !card.IsFavorite;
		await _Repository.SetFavoriteAsync( card.Game.Id, isFavorite );
		card.IsFavorite = isFavorite;
		ApplyFilter();
	}
}
