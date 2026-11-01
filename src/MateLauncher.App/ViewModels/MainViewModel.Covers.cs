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
	private async Task RefreshStoreCoversOnceAsync()
	{
		AppSettings settings = _SettingsService.Load();

		if ( settings.StoreCoversRefreshed )
		{
			return;
		}

		foreach ( GameCardViewModel card in _AllCards.Where( card => card.Game.Platform is GamePlatform.Xbox or GamePlatform.Riot && !card.Game.IsCustomized ) )
		{
			await _Repository.SetCoverAsync( card.Game.Id, string.Empty );
			card.Game.CoverImagePath = null;
			card.CoverImagePath = null;
		}

		settings.StoreCoversRefreshed = true;
		_SettingsService.Save( settings );
	}

	private async Task FindMissingCoversAsync()
	{
		await RefreshStoreCoversOnceAsync();
		List<GameCardViewModel> cardsWithoutCover = _AllCards.Where( card => string.IsNullOrWhiteSpace( card.Game.CoverImagePath ) ).ToList();
		ICoverFinder storeFinder = new CompositeCoverFinder( new SteamStoreCoverFinder(), new SteamGridDbCoverFinder( _SettingsService ), new GoogleImageCoverFinder( _SettingsService ) );
		int processed = 0;

		foreach ( GameCardViewModel card in cardsWithoutCover )
		{
			CoverStatusText = string.Format( T( "Cover_Searching" ), processed, cardsWithoutCover.Count );
			bool usesGeneratedArt = card.Game.Platform is GamePlatform.Xbox or GamePlatform.Riot;
			string? coverUrl = await ( usesGeneratedArt ? storeFinder : _CoverFinder ).FindCoverUrlAsync( card.Name );
			processed++;

			if ( coverUrl is null && usesGeneratedArt )
			{
				string generatedFile = GeneratedCoverFactory.Create( card.Name, card.Game.Platform );
				string? cachedPath = await _CoverCache.ImportAsync( card.Game, generatedFile );
				File.Delete( generatedFile );

				if ( cachedPath is not null )
				{
					card.Game.CoverImagePath = cachedPath;
					card.CoverImagePath = cachedPath;
					await _Repository.SetCoverAsync( card.Game.Id, cachedPath );
				}

				continue;
			}

			if ( coverUrl is null )
			{
				continue;
			}

			card.Game.CoverImagePath = coverUrl;
			await _Repository.SetCoverAsync( card.Game.Id, coverUrl );
		}

		CoverStatusText = string.Empty;
	}

	private async Task DownloadRemoteCoversAsync()
	{
		List<GameCardViewModel> remoteCards = _AllCards.Where( card => Game.IsRemoteCover( card.Game.CoverImagePath ) ).ToList();
		int downloaded = 0;

		foreach ( GameCardViewModel card in remoteCards )
		{
			CoverStatusText = string.Format( T( "Cover_Downloading" ), downloaded, remoteCards.Count );
			string? coverPath = await _CoverCache.CacheAsync( card.Game );

			if ( coverPath is null )
			{
				continue;
			}

			await _Repository.SetCoverAsync( card.Game.Id, coverPath );
			card.Game.CoverImagePath = coverPath;
			card.CoverImagePath = coverPath;
			downloaded++;
		}

		CoverStatusText = string.Empty;
	}

	private async Task ComputeMissingSizesAsync()
	{
		List<GameCardViewModel> pending = _AllCards.Where( card => card.SizeBytes == 0 && card.HasInstallPath ).ToList();
		int done = 0;

		foreach ( GameCardViewModel card in pending )
		{
			CoverStatusText = string.Format( T( "Size_Computing" ), done, pending.Count );
			long size = await FolderSizeCalculator.CalculateAsync( card.InstallPath );
			done++;

			if ( size <= 0 )
			{
				continue;
			}

			card.Game.SizeBytes = size;
			card.SizeBytes = size;
			await _Repository.SetSizeAsync( card.Game.Id, size );
		}

		CoverStatusText = string.Empty;
	}
}
