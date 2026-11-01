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
	partial void OnSteamGridDbApiKeyChanged( string value )
	{
		SaveSettings();
	}

	partial void OnIgdbClientIdChanged( string value )
	{
		SaveSettings();
	}

	partial void OnIgdbClientSecretChanged( string value )
	{
		SaveSettings();
	}

	private async Task LoadMetadataAsync( GameCardViewModel? card )
	{
		GameSummary = string.Empty;
		GameInfoLine = string.Empty;
		HasGameInfo = false;

		if ( card is null )
		{
			return;
		}

		GameMetadata? metadata = await _MetadataProvider.GetMetadataAsync( card.Name );

		if ( metadata is null || SelectedGame != card )
		{
			return;
		}

		List<string> parts = new List<string>();

		if ( metadata.Genres.Count > 0 )
		{
			parts.Add( string.Join( ", ", metadata.Genres ) );
		}

		if ( metadata.ReleaseDate is not null )
		{
			parts.Add( metadata.ReleaseDate.Value.Year.ToString() );
		}

		if ( metadata.Rating is not null )
		{
			parts.Add( "★ " + metadata.Rating.Value.ToString( "0" ) + "/100" );
		}

		if ( metadata.TimeToBeatSeconds is int secondsToBeat )
		{
			parts.Add( string.Format( T( "Meta_TimeToBeat" ), Math.Max( 1, ( int ) Math.Round( secondsToBeat / 3600.0 ) ) ) );
			double? ratio = SessionStatistics.ProgressRatio( card.PlaytimeSeconds, secondsToBeat );

			if ( ratio is not null && card.PlaytimeSeconds > 0 )
			{
				parts.Add( string.Format( T( "Meta_Progress" ), ( int ) Math.Round( ratio.Value * 100 ) ) );
			}
		}

		GameSummary = metadata.Summary;
		GameInfoLine = string.Join( "  Â·  ", parts );
		HasGameInfo = GameSummary.Length > 0 || GameInfoLine.Length > 0;
	}

	partial void OnGoogleApiKeyChanged( string value )
	{
		SaveSettings();
	}

	partial void OnGoogleSearchEngineIdChanged( string value )
	{
		SaveSettings();
	}

	partial void OnSteamApiKeyChanged( string value )
	{
		_AchievementCache.Clear();
		SaveSettings();
	}

	partial void OnSteamIdChanged( string value )
	{
		_AchievementCache.Clear();
		SaveSettings();
	}

	partial void OnSelectedGameChanged( GameCardViewModel? value )
	{
		_ = LoadAchievementsAsync( value );
		_ = LoadMetadataAsync( value );
		_ = LoadGameFriendsAsync( value );
		_ = LoadSavesAsync( value );
		_ = LoadSessionsAsync( value );
	}

	private async Task LoadAchievementsAsync( GameCardViewModel? card )
	{
		Achievements.Clear();
		AchievementsSummary = string.Empty;
		AchievementsProgress = 0;
		bool isSteamGame = card is not null && card.Game.Platform == GamePlatform.Steam && long.TryParse( card.Game.PlatformGameId, out _ );
		ShowAchievements = isSteamGame;

		if ( !isSteamGame || card is null )
		{
			return;
		}

		if ( string.IsNullOrWhiteSpace( SteamApiKey ) || string.IsNullOrWhiteSpace( SteamId ) )
		{
			AchievementsStatus = T( "Ach_NoKey" );
			return;
		}

		AchievementsStatus = T( "Ach_Loading" );
		string cacheKey = $"{card.Game.PlatformGameId}|{Language}";

		if ( !_AchievementCache.TryGetValue( cacheKey, out AchievementResult? result ) )
		{
			result = await _AchievementsService.GetAchievementsAsync( SteamApiKey, SteamId, card.Game.PlatformGameId, Language );

			if ( result.ErrorKey is null )
			{
				_AchievementCache[ cacheKey ] = result;
			}
		}

		if ( SelectedGame != card )
		{
			return;
		}

		if ( result.ErrorKey is not null )
		{
			AchievementsStatus = T( result.ErrorKey );
			return;
		}

		foreach ( AchievementInfo info in result.Items )
		{
			Achievements.Add( new AchievementViewModel( info ) );
		}

		int unlocked = result.Items.Count( item => item.IsUnlocked );
		AchievementsStatus = result.Items.Count == 0 ? T( "Ach_NoStats" ) : string.Empty;
		AchievementsSummary = string.Format( T( "Ach_Summary" ), unlocked, result.Items.Count );
		AchievementsProgress = result.Items.Count == 0 ? 0 : unlocked * 100.0 / result.Items.Count;
	}

	[RelayCommand]
	public void DetectSteamId()
	{
		string? detected = SteamAccountLocator.FindSteamId();

		if ( detected is null )
		{
			SteamAccountStatus = T( "Steam_NoLocal" );
			return;
		}

		SteamId = detected;
		SteamAccountStatus = T( "Steam_Detected" );
	}

	[RelayCommand]
	public async Task CheckSteamAccountAsync()
	{
		if ( string.IsNullOrWhiteSpace( SteamApiKey ) || string.IsNullOrWhiteSpace( SteamId ) )
		{
			SteamAccountStatus = T( "Steam_MissingFields" );
			return;
		}

		SteamAccountStatus = T( "Steam_Checking" );
		SteamAccountCheck check = await _AchievementsService.CheckAccountAsync( SteamApiKey, SteamId );

		if ( !check.Success )
		{
			SteamAccountStatus = T( check.ErrorKey ?? "Ach_Error" );
			return;
		}

		SteamAccountStatus = string.Format( T( check.IsPublic ? "Steam_CheckOk" : "Steam_CheckPrivate" ), check.PersonaName );
	}

	private void UpdateSteamStatus()
	{
		IsSteamRunning = Process.GetProcessesByName( "steam" ).Length > 0;
	}

	[RelayCommand]
	public void StartSteam()
	{
		OpenClient( "steam://open/main" );
	}

	[RelayCommand]
	public async Task SearchSteamAsync()
	{
		SteamResults.Clear();
		SteamSearchStatus = T( "Downloads_Searching" );
		IReadOnlyList<SteamSearchResult> results = await _SteamStoreSearch.SearchAsync( DownloadSearchText, Language );

		foreach ( SteamSearchResult result in results )
		{
			SteamResults.Add( new SteamResultViewModel( result ) );
		}

		SteamSearchStatus = results.Count == 0 ? T( "Downloads_NoResult" ) : string.Empty;
	}

	[RelayCommand]
	public async Task InstallSteamGameAsync( SteamResultViewModel? result )
	{
		if ( result is null )
		{
			return;
		}

		UpdateSteamStatus();

		if ( !IsSteamRunning )
		{
			OpenClient( "steam://open/main" );
			await Task.Delay( 6000 );
		}

		OpenClient( $"steam://install/{result.AppId}" );
		DownloadsMessage = T( "Downloads_RequestSent" );
		await Task.Delay( 1500 );
		OpenClient( "steam://nav/downloads" );
	}
}
