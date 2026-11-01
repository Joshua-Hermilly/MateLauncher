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
	partial void OnMinimizeToTrayChanged( bool value )	{
		SaveSettings();
	}

	partial void OnStartWithWindowsChanged( bool value )
	{
		if ( _IsLoadingSettings )
		{
			return;
		}

		StartupRegistration.Apply( value );
		SaveSettings();
	}

	partial void OnOverlayHotkeyChanged( string value )
	{
		SaveSettings();
	}

	private async Task OnLanguageSwitchedAsync()
	{
		foreach ( NavItemViewModel item in LibraryItems.Concat( ToolItems ) )
		{
			item.RefreshTitle();
		}

		OnPropertyChanged( nameof( OverlayDescription ) );
		await RefreshAsync();

		if ( IsStatsOpen )
		{
			await LoadStatsAsync();
		}

		if ( IsDownloadsOpen )
		{
			await RefreshUpdatesAsync();
		}
	}

	partial void OnCardWidthChanged( double value )
	{
		SaveSettings();
		UpdateGridLayout( _AvailableWidth );
	}

	public void UpdateGridLayout( double availableWidth )
	{
		_AvailableWidth = availableWidth;

		if ( availableWidth <= 0 )
		{
			ActualCardWidth = CardWidth;
			return;
		}

		int columns = Math.Max( 1, ( int ) Math.Floor( availableWidth / ( CardWidth + CardSpacing ) ) );
		ActualCardWidth = Math.Floor( availableWidth / columns ) - CardSpacing;
	}

	partial void OnIsListViewChanged( bool value )
	{
		SaveSettings();
	}

	partial void OnTranslucencyLevelChanged( double value )
	{
		if ( !_IsLoadingSettings )
		{
			_ThemeService.SetTranslucency( ( int ) Math.Round( value ) );
		}

		SaveSettings();
	}

	partial void OnAnimationsEnabledChanged( bool value )
	{
		SaveSettings();
	}

	partial void OnOverlayEnabledChanged( bool value )
	{
		SaveSettings();
	}

	partial void OnLanguageChanged( string value )
	{
		Localizer.Instance.SetLanguage( value );
		SaveSettings();
	}

	[RelayCommand]
	public void SetLanguage( string? code )
	{
		Language = code == "en" ? "en" : "fr";
	}

	[RelayCommand]
	public void ShowGrid()
	{
		IsListView = false;
	}

	[RelayCommand]
	public void ShowList()
	{
		IsListView = true;
	}

	private void SaveSettings()
	{
		if ( _IsLoadingSettings )
		{
			return;
		}

		AppSettings settings = _SettingsService.Load();
		settings.CardWidth = CardWidth;
		settings.IsListView = IsListView;
		settings.ThemeId = _ThemeId;
		settings.CustomThemes = _ThemeService.GetCustomThemes().ToList();
		settings.CheckUpdatesOnStartup = CheckUpdatesOnStartup;
		settings.AccentColor = _AccentColor;
		settings.AnimationsEnabled = AnimationsEnabled;
		settings.PerformanceModeEnabled = PerformanceModeEnabled;
		settings.PerformancePrograms = GetPerformanceProgramList();
		settings.TranslucencyLevel = ( int ) Math.Round( TranslucencyLevel );
		settings.Language = Language;
		settings.OverlayEnabled = OverlayEnabled;
		settings.OverlayHotkey = OverlayHotkey;
		settings.MinimizeToTray = MinimizeToTray;
		settings.StartWithWindows = StartWithWindows;
		settings.SortMode = SortMode;
		settings.FolderSort = FolderSort;
		settings.MergeFolders = MergeFolders;
		settings.IncludeGameNews = IncludeGameNews;
		settings.GoogleApiKey = GoogleApiKey;
		settings.SteamGridDbApiKey = SteamGridDbApiKey;
		settings.IgdbClientId = IgdbClientId;
		settings.IgdbClientSecret = IgdbClientSecret;
		settings.GoogleSearchEngineId = GoogleSearchEngineId;
		settings.SteamApiKey = SteamApiKey;
		settings.SteamId = SteamId;
		settings.NewsFeedsInitialized = true;
		settings.NewsFeeds = NewsFeedEntries.Select( feed => new NewsFeed { Title = feed.Title, Url = feed.Url } ).ToList();
		settings.GameFolders = Folders.Select( folder => new GameFolder { Path = folder.Path, TabName = folder.TabName, IconPath = folder.IconPath, IsEnabled = folder.IsEnabled, ScanMode = folder.ScanMode } ).ToList();
		settings.Categories = _Categories.ToList();
		settings.CategoryColors = new Dictionary<string, string>( _CategoryColors );
		_SettingsService.Save( settings );	}

	[RelayCommand]
	public void RestartNormal()
	{
		AppRestarter.Restart();
	}

	[RelayCommand]
	public void RestartSafe()
	{
		AppRestarter.Restart( "--safe" );
	}

	[RelayCommand]
	public void RestartDebug()
	{
		AppRestarter.Restart( "--debug" );
	}

	[RelayCommand]
	public void OpenDataFolder()
	{
		Process.Start( new ProcessStartInfo( "explorer.exe", $"\"{StartupGuard.DataFolder}\"" ) { UseShellExecute = true } );
	}

	[RelayCommand]
	public void ChangeOverlayHotkey()
	{
		HotkeyCaptureWindow window = new HotkeyCaptureWindow( string.IsNullOrEmpty( OverlayHotkeyText ) ? OverlayHotkey : OverlayHotkeyText ) { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() == true && window.Hotkey is not null )
		{
			OverlayHotkey = window.Hotkey;
		}
	}

	public void SaveOverlayPosition( double left, double top )
	{
		AppSettings settings = _SettingsService.Load();
		settings.OverlayLeft = left;
		settings.OverlayTop = top;
		_SettingsService.Save( settings );
	}

	[RelayCommand]
	public void OpenSettings()
	{
		SelectedGame = null;
		SelectedLibraryItem = null;
		SelectedToolItem = null;
		SelectedPlatformItem = null;
		SelectedCategoryItem = null;
		SelectedGroupItem = null;
		IsStatsOpen = false;
		IsDownloadsOpen = false;
		IsNewsOpen = false;
		IsFriendsOpen = false;
		IsHomeOpen = false;
		IsSettingsOpen = true;
	}
}
