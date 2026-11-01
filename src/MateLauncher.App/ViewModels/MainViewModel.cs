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

public partial class MainViewModel : ObservableObject
{
	private readonly LibrarySyncService _SyncService;
	private readonly IGameLauncher _GameLauncher;
	private readonly ILibraryRepository _Repository;
	private readonly ICoverCache _CoverCache;
	private readonly ICoverFinder _CoverFinder;
	private readonly IPlaytimeTracker _PlaytimeTracker;
	private readonly ISettingsService _SettingsService;
	private readonly IUpdateChecker _UpdateChecker;
	private readonly ThemeService _ThemeService;
	private readonly RuntimeOptions _RuntimeOptions;
	private bool _IsApplyingFolderEdit;
	private bool _IsBuildingFilters;
	private readonly SteamAchievementsService _AchievementsService = new SteamAchievementsService();
	private readonly Dictionary<string, AchievementResult> _AchievementCache = new Dictionary<string, AchievementResult>();
	private readonly HashSet<string> _SelectedFilterKeys = new HashSet<string>();
	private readonly HashSet<string> _CollapsedFilterGroups = new HashSet<string>();
	private readonly BackupService _BackupService;
	private readonly NewsService _NewsService;
	private readonly SteamStoreSearch _SteamStoreSearch;
	private readonly Dictionary<string, string> _CategoryColors = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
	private readonly List<string> _Categories = new List<string>();
	private readonly HashSet<Guid> _RunningGameIds = new HashSet<Guid>();
	private readonly Dictionary<string, ( long Manifest, long Staging )> _LastDownloadedBytes = new Dictionary<string, ( long Manifest, long Staging )>();
	private readonly Dictionary<string, ( long Manifest, long Staging )> _DownloadBaselines = new Dictionary<string, ( long Manifest, long Staging )>();
	private readonly System.Windows.Threading.DispatcherTimer _UpdatesTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds( 2 ) };
	private DateTime _LastUpdatesPollAt = DateTime.UtcNow;
	private double _ChartWidth = 640;
	private List<GameCardViewModel> _AllCards = new List<GameCardViewModel>();
	private bool _IsRebuildingNavigation;
	private bool _IsLoadingSettings;
	private double _AvailableWidth;
	private string _ThemeId;
	private string? _AccentColor;

	private const double CardSpacing = 16;
	private const long LargeGameBytes = 50L * 1024 * 1024 * 1024;

	[ObservableProperty]
	private string _SearchText = string.Empty;

	[ObservableProperty]
	private string _StatusText = string.Empty;

	[ObservableProperty]
	private string _CoverStatusText = string.Empty;

	[ObservableProperty]
	private GameCardViewModel? _SelectedGame;

	[ObservableProperty]
	private GameCardViewModel? _ActiveGame;

	[ObservableProperty]
	private DateTime? _SessionStartedAt;

	[ObservableProperty]
	private bool _IsSettingsOpen;

	[ObservableProperty]
	private bool _IsStatsOpen;

	[ObservableProperty]
	private bool _IsDownloadsOpen;

	[ObservableProperty]
	private int _SettingsSectionIndex;

	[ObservableProperty]
	private bool _IsListView;

	[ObservableProperty]
	private bool _AnimationsEnabled = true;

	[ObservableProperty]
	private bool _OverlayEnabled = true;

	[ObservableProperty]
	private bool _IsSafeMode;

	[ObservableProperty]
	private FolderSortMode _FolderSort;

	[ObservableProperty]
	private bool _MergeFolders;

	[ObservableProperty]
	private GameSortMode _SortMode;

	[ObservableProperty]
	private bool _MinimizeToTray;

	[ObservableProperty]
	private bool _StartWithWindows;

	[ObservableProperty]
	private string _OverlayHotkey = "Ctrl+Alt+O";

	[ObservableProperty]
	private bool _IsNewsOpen;

	[ObservableProperty]
	private bool _IsFilterPanelOpen;

	[ObservableProperty]
	private int _ActiveFilterCount;

	[ObservableProperty]
	private bool _HasActiveFilters;

	[ObservableProperty]
	private bool _IsNewsPanelOpen = true;

	[ObservableProperty]
	private string _GoogleApiKey = string.Empty;

	[ObservableProperty]
	private string _GoogleSearchEngineId = string.Empty;

	[ObservableProperty]
	private string _SteamApiKey = string.Empty;

	[ObservableProperty]
	private string _SteamId = string.Empty;

	[ObservableProperty]
	private string _SteamAccountStatus = string.Empty;

	[ObservableProperty]
	private bool _ShowAchievements;

	[ObservableProperty]
	private string _AchievementsStatus = string.Empty;

	[ObservableProperty]
	private string _AchievementsSummary = string.Empty;

	[ObservableProperty]
	private double _AchievementsProgress;

	[ObservableProperty]
	private bool _IsNewsLoading;

	[ObservableProperty]
	private bool _IncludeGameNews = true;

	[ObservableProperty]
	private bool _HasNoNews = true;

	[ObservableProperty]
	private string _NewsStatus = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( SteamStatusText ) )]
	private bool _IsSteamRunning;

	[ObservableProperty]
	private string _DownloadSearchText = string.Empty;

	[ObservableProperty]
	private string _SteamSearchStatus = string.Empty;

	[ObservableProperty]
	private string _BackupStatus = string.Empty;

	public string SteamStatusText => T( IsSteamRunning ? "Downloads_SteamRunning" : "Downloads_SteamStopped" );

	public ObservableCollection<FilterGroupViewModel> FilterGroups { get; } = new ObservableCollection<FilterGroupViewModel>();
	public ObservableCollection<FilterOptionViewModel> ActiveFilters { get; } = new ObservableCollection<FilterOptionViewModel>();
	public ObservableCollection<AchievementViewModel> Achievements { get; } = new ObservableCollection<AchievementViewModel>();
	public ObservableCollection<NewsItemViewModel> NewsItems { get; } = new ObservableCollection<NewsItemViewModel>();
	public ObservableCollection<FeedEntryViewModel> NewsFeedEntries { get; } = new ObservableCollection<FeedEntryViewModel>();
	public ObservableCollection<SteamResultViewModel> SteamResults { get; } = new ObservableCollection<SteamResultViewModel>();

	partial void OnSortModeChanged( GameSortMode value )
	{
		if ( _IsLoadingSettings )
		{
			return;
		}

		SaveSettings();
		ApplyFilter();
	}

	partial void OnGoogleApiKeyChanged( string value )
	{
		SaveSettings();
	}

	partial void OnGoogleSearchEngineIdChanged( string value )
	{
		SaveSettings();
	}

	[RelayCommand]
	public void ToggleNewsPanel()
	{
		IsNewsPanelOpen = !IsNewsPanelOpen;
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

	partial void OnIncludeGameNewsChanged( bool value )
	{
		if ( _IsLoadingSettings )
		{
			return;
		}

		SaveSettings();

		if ( IsNewsOpen )
		{
			_ = RefreshNewsAsync();
		}
	}

	private static List<NewsFeed> BuildDefaultFeeds()
	{
		return new List<NewsFeed>
		{
			new NewsFeed { Title = "Steam", Url = "https://store.steampowered.com/feeds/news.xml" },
			new NewsFeed { Title = "Rock Paper Shotgun", Url = "https://www.rockpapershotgun.com/feed" },
			new NewsFeed { Title = "PC Gamer", Url = "https://www.pcgamer.com/rss/" }
		};
	}

	partial void OnFolderSortChanged( FolderSortMode value )
	{
		ReapplyFolderOrganization();
	}

	partial void OnMergeFoldersChanged( bool value )
	{
		ReapplyFolderOrganization();
	}

	private void ReapplyFolderOrganization()
	{
		if ( _IsLoadingSettings )
		{
			return;
		}

		SaveSettings();
		RebuildNavigation();
		ApplyFilter();
	}

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( OverlayDescription ) )]
	private string _OverlayHotkeyText = string.Empty;

	public string OverlayDescription => string.IsNullOrEmpty( OverlayHotkeyText )
		? string.Format( T( "General_OverlayUnavailable" ) )
		: string.Format( T( "General_OverlayDesc" ), OverlayHotkeyText );

	[ObservableProperty]
	private string _Language = "fr";

	[ObservableProperty]
	private double _CardWidth = 190;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( ActualCardHeight ) )]
	private double _ActualCardWidth = 190;

	[ObservableProperty]
	private NavItemViewModel? _SelectedLibraryItem;

	[ObservableProperty]
	private NavItemViewModel? _SelectedToolItem;

	[ObservableProperty]
	private NavItemViewModel? _SelectedPlatformItem;

	[ObservableProperty]
	private NavItemViewModel? _SelectedCategoryItem;

	[ObservableProperty]
	private NavItemViewModel? _SelectedGroupItem;

	[ObservableProperty]
	private bool _HasGroups;

	[ObservableProperty]
	private bool _HasNoFolders = true;

	[ObservableProperty]
	private StatsViewModel? _Stats;

	[ObservableProperty]
	private bool _HasNoUpdates = true;

	[ObservableProperty]
	private string _DownloadsMessage = string.Empty;

	public double ActualCardHeight => ActualCardWidth * 1.5;

	public ObservableCollection<GameCardViewModel> Games { get; } = new ObservableCollection<GameCardViewModel>();
	public ObservableCollection<NavItemViewModel> LibraryItems { get; }
	public ObservableCollection<NavItemViewModel> ToolItems { get; }
	public ObservableCollection<NavItemViewModel> PlatformItems { get; } = new ObservableCollection<NavItemViewModel>();
	public ObservableCollection<NavItemViewModel> CategoryItems { get; } = new ObservableCollection<NavItemViewModel>();
	public ObservableCollection<NavItemViewModel> GroupItems { get; } = new ObservableCollection<NavItemViewModel>();
	[ObservableProperty]
	private bool _CheckUpdatesOnStartup = true;

	[ObservableProperty]
	private string _UpdateStatusText = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( CanInstallUpdate ) )]
	private bool _IsUpdateAvailable;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( CanInstallUpdate ) )]
	private bool _IsUpdating;

	[ObservableProperty]
	private bool _IsUpdateBannerVisible;

	[ObservableProperty]
	private double _UpdateProgress;

	[ObservableProperty]
	private string _UpdateBannerText = string.Empty;

	private AppUpdateInfo? _PendingUpdate;

	public bool CanInstallUpdate => IsUpdateAvailable && !IsUpdating;

	public string CurrentVersion { get; } = ReadCurrentVersion();

	public string VersionBadge => "v" + CurrentVersion;

	public string CurrentVersionText => string.Format( T( "Update_Current" ), CurrentVersion );

	partial void OnCheckUpdatesOnStartupChanged( bool value )
	{
		SaveSettings();
	}

	public ObservableCollection<ThemeOptionViewModel> ThemeOptions { get; } = new ObservableCollection<ThemeOptionViewModel>();
	public ObservableCollection<AccentOptionViewModel> AccentOptions { get; } = new ObservableCollection<AccentOptionViewModel>();
	public ObservableCollection<FolderEntryViewModel> Folders { get; } = new ObservableCollection<FolderEntryViewModel>();
	public ObservableCollection<UpdateItemViewModel> PendingUpdates { get; } = new ObservableCollection<UpdateItemViewModel>();

	public MainViewModel( LibrarySyncService syncService, IGameLauncher gameLauncher, ILibraryRepository repository, ICoverCache coverCache, ICoverFinder coverFinder, IPlaytimeTracker playtimeTracker, ISettingsService settingsService, IUpdateChecker updateChecker, ThemeService themeService, RuntimeOptions runtimeOptions, BackupService backupService, NewsService newsService, SteamStoreSearch steamStoreSearch )
	{
		_IsLoadingSettings = true;
		_SettingsService = settingsService;
		_ThemeService = themeService;
		_RuntimeOptions = runtimeOptions;
		IsSafeMode = runtimeOptions.SafeMode;
		_BackupService = backupService;
		_NewsService = newsService;
		_SteamStoreSearch = steamStoreSearch;
		_SyncService = syncService;
		_GameLauncher = gameLauncher;
		_Repository = repository;
		_CoverCache = coverCache;
		_CoverFinder = coverFinder;
		_UpdateChecker = updateChecker;
		_PlaytimeTracker = playtimeTracker;

		AppSettings settings = settingsService.Load();
		Localizer.Instance.SetLanguage( settings.Language );
		Language = Localizer.Instance.Language;
		CardWidth = Math.Clamp( settings.CardWidth, 140, 280 );
		IsListView = settings.IsListView;
		AnimationsEnabled = settings.AnimationsEnabled;
		OverlayEnabled = settings.OverlayEnabled;
		FolderSort = settings.FolderSort;
		MergeFolders = settings.MergeFolders;
		SortMode = settings.SortMode;
		MinimizeToTray = settings.MinimizeToTray;
		StartWithWindows = settings.StartWithWindows;
		OverlayHotkey = settings.OverlayHotkey;
		IncludeGameNews = settings.IncludeGameNews;
		CheckUpdatesOnStartup = settings.CheckUpdatesOnStartup;
		_ThemeService.SetCustomThemes( settings.CustomThemes );
		GoogleApiKey = settings.GoogleApiKey;
		GoogleSearchEngineId = settings.GoogleSearchEngineId;
		SteamApiKey = settings.SteamApiKey;
		SteamId = settings.SteamId;
		BackupStatus = T( "Backup_Desc" );

		foreach ( KeyValuePair<string, string> categoryColor in settings.CategoryColors )
		{
			_CategoryColors[ categoryColor.Key ] = categoryColor.Value;
		}

		foreach ( NewsFeed feed in settings.NewsFeedsInitialized ? settings.NewsFeeds : BuildDefaultFeeds() )
		{
			NewsFeedEntries.Add( new FeedEntryViewModel( feed.Title, feed.Url ) );
		}
		_ThemeId = themeService.GetTheme( settings.ThemeId ).Id;
		_AccentColor = settings.AccentColor;
		_Categories.AddRange( settings.Categories );

		RebuildThemeOptions();

		RebuildAccentOptions();

		foreach ( GameFolder gameFolder in settings.GameFolders )
		{
			Folders.Add( CreateFolderEntry( gameFolder.Path, gameFolder.TabName, gameFolder.IconPath, gameFolder.IsEnabled, gameFolder.ScanMode ) );
		}

		HasNoFolders = Folders.Count == 0;

		LibraryItems = new ObservableCollection<NavItemViewModel>
		{
			new NavItemViewModel( string.Empty, "", NavKind.All, titleKey: "Nav_All" ),
			new NavItemViewModel( string.Empty, "", NavKind.Favorites, titleKey: "Nav_Favorites" ),
			new NavItemViewModel( string.Empty, "", NavKind.Recent, titleKey: "Nav_Recent" )
		};

		ToolItems = new ObservableCollection<NavItemViewModel>
		{
			new NavItemViewModel( string.Empty, "", NavKind.Stats, titleKey: "Nav_Stats" ),
			new NavItemViewModel( string.Empty, "", NavKind.Downloads, titleKey: "Nav_Downloads" ),
			new NavItemViewModel( string.Empty, "\uE8A5", NavKind.News, titleKey: "Nav_News" )
		};

		SelectedLibraryItem = LibraryItems[ 0 ];
		_IsLoadingSettings = false;

		_UpdatesTimer.Tick += async ( _, _ ) =>
		{
			if ( IsDownloadsOpen )
			{
				await RefreshUpdatesAsync();
			}
		};
		_UpdatesTimer.Start();
		_PlaytimeTracker.PlaytimeChanged += OnPlaytimeChanged;
		_PlaytimeTracker.SessionStarted += OnSessionStarted;
		_PlaytimeTracker.SessionEnded += OnSessionEnded;
		Localizer.Instance.LanguageChanged += () => Application.Current.Dispatcher.Invoke( async () => await OnLanguageSwitchedAsync() );
	}

	private GameCardViewModel CreateCard( Game game )
	{
		return new GameCardViewModel( game ) { IsRunning = _RunningGameIds.Contains( game.Id ) };
	}

	private static string T( string key )
	{
		return Localizer.Instance.Get( key );
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
	public void SelectTheme( ThemeOptionViewModel? option )
	{
		if ( option is null )
		{
			return;
		}

		_ThemeId = option.Theme.Id;

		foreach ( ThemeOptionViewModel themeOption in ThemeOptions )
		{
			themeOption.IsSelected = themeOption == option;
		}

		_ThemeService.Apply( _ThemeId, _AccentColor );
		SaveSettings();
	}

	[RelayCommand]
	public void SelectAccent( AccentOptionViewModel? option )
	{
		if ( option is null )
		{
			return;
		}

		_AccentColor = option.Hex;

		foreach ( AccentOptionViewModel accentOption in AccentOptions )
		{
			accentOption.IsSelected = accentOption == option;
		}

		_ThemeService.Apply( _ThemeId, _AccentColor );
		SaveSettings();
	}

	private static string ReadCurrentVersion()
	{
		string? path = Environment.ProcessPath;
		string? version = path is null ? null : FileVersionInfo.GetVersionInfo( path ).ProductVersion;
		return version?.Split( '+' )[ 0 ] ?? "0.0.0";
	}

	private void RebuildAccentOptions()
	{
		AccentOptions.Clear();
		AccentOptions.Add( new AccentOptionViewModel( null ) { IsSelected = _AccentColor is null } );

		foreach ( string accentColor in _ThemeService.AccentColors )
		{
			AccentOptions.Add( new AccentOptionViewModel( accentColor ) { IsSelected = string.Equals( accentColor, _AccentColor, StringComparison.OrdinalIgnoreCase ) } );
		}

		if ( _AccentColor is not null && _ThemeService.AccentColors.All( accentColor => !string.Equals( accentColor, _AccentColor, StringComparison.OrdinalIgnoreCase ) ) )
		{
			AccentOptions.Add( new AccentOptionViewModel( _AccentColor ) { IsSelected = true } );
		}
	}

	[RelayCommand]
	public void PickCustomAccent()
	{
		ColorPickerWindow window = new ColorPickerWindow( T( "Appearance_Accent" ), _AccentColor ?? _ThemeService.GetTheme( _ThemeId ).Accent ) { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() != true )
		{
			return;
		}

		if ( window.IsReset )
		{
			_AccentColor = null;
		}
		else if ( window.SelectedColor is not null && AppTheme.IsValidColor( window.SelectedColor ) )
		{
			_AccentColor = AppTheme.NormalizeColor( window.SelectedColor );
		}

		RebuildAccentOptions();
		_ThemeService.Apply( _ThemeId, _AccentColor );
		SaveSettings();
	}

	private void RebuildThemeOptions()
	{
		ThemeOptions.Clear();

		foreach ( AppTheme theme in _ThemeService.Themes )
		{
			ThemeOptions.Add( new ThemeOptionViewModel( theme ) { IsSelected = theme.Id == _ThemeId } );
		}
	}

	[RelayCommand]
	public void CreateTheme()
	{
		OpenThemeEditor( _ThemeService.GetTheme( _ThemeId ), true );
	}

	[RelayCommand]
	public void EditTheme( ThemeOptionViewModel? option )
	{
		if ( option is not null && option.Theme.IsCustom )
		{
			OpenThemeEditor( option.Theme, false );
		}
	}

	private void OpenThemeEditor( AppTheme source, bool isNew )
	{
		ThemeEditorWindow window = new ThemeEditorWindow( source, isNew ) { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() != true )
		{
			return;
		}

		if ( window.IsDeleted )
		{
			if ( !ConfirmDialog.Ask( T( "Theme_DeleteTitle" ), string.Format( T( "Theme_DeleteMessage" ), source.Name ), T( "Btn_Delete" ) ) )
			{
				return;
			}

			_ThemeService.RemoveCustomTheme( source.Id );

			if ( _ThemeId == source.Id )
			{
				_ThemeId = ThemeService.DefaultThemeId;
			}
		}
		else if ( window.Result is not null )
		{
			_ThemeService.SaveCustomTheme( window.Result );
			_ThemeId = window.Result.Id;
			_AccentColor = null;

			foreach ( AccentOptionViewModel accentOption in AccentOptions )
			{
				accentOption.IsSelected = accentOption.Hex is null;
			}
		}

		RebuildThemeOptions();
		_ThemeService.Apply( _ThemeId, _AccentColor );
		SaveSettings();
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

	private FolderEntryViewModel CreateFolderEntry( string path, string tabName, string? iconPath = null, bool isEnabled = true, FolderScanMode scanMode = FolderScanMode.Subfolders )
	{
		FolderEntryViewModel entry = new FolderEntryViewModel( path, tabName, iconPath, isEnabled, scanMode );

		entry.PropertyChanged += async ( _, args ) =>
		{
			if ( _IsApplyingFolderEdit || args.PropertyName is nameof( FolderEntryViewModel.Icon ) or nameof( FolderEntryViewModel.HasIcon ) )
			{
				return;
			}

			SaveSettings();
			await SyncAsync();
		};

		return entry;
	}

	[RelayCommand]
	public async Task AddFolderAsync()
	{
		OpenFolderDialog dialog = new OpenFolderDialog { Title = T( "Library_AddFolder" ) };

		if ( dialog.ShowDialog() != true || Folders.Any( folder => string.Equals( folder.Path, dialog.FolderName, StringComparison.OrdinalIgnoreCase ) ) )
		{
			return;
		}

		string tabName = new DirectoryInfo( dialog.FolderName ).Name;
		Folders.Add( CreateFolderEntry( dialog.FolderName, tabName ) );
		HasNoFolders = false;
		SaveSettings();
		await SyncAsync();
	}

	[RelayCommand]
	public async Task RemoveFolderAsync( FolderEntryViewModel? entry )
	{
		if ( entry is null )
		{
			return;
		}

		Folders.Remove( entry );
		HasNoFolders = Folders.Count == 0;
		SaveSettings();
		await SyncAsync();
	}

	internal static string? PickIconFile()
	{
		OpenFileDialog dialog = new OpenFileDialog { Title = T( "Config_PickImage" ), Filter = "Images (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico" };

		if ( dialog.ShowDialog() != true )
		{
			return null;
		}

		string iconsFolder = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), "MateLauncher", "icons" );
		Directory.CreateDirectory( iconsFolder );
		string target = Path.Combine( iconsFolder, $"{Guid.NewGuid():N}{Path.GetExtension( dialog.FileName )}" );
		File.Copy( dialog.FileName, target, true );
		return target;
	}

	[RelayCommand]
	public void ChooseFolderIcon( FolderEntryViewModel? entry )
	{
		string? iconPath = entry is null ? null : PickIconFile();

		if ( entry is not null && iconPath is not null )
		{
			entry.IconPath = iconPath;
		}
	}

	[RelayCommand]
	public void ResetFolderIcon( FolderEntryViewModel? entry )
	{
		if ( entry is not null )
		{
			entry.IconPath = null;
		}
	}

	private void ChangeGroupIcon( string groupName )
	{
		string? iconPath = PickIconFile();

		if ( iconPath is null )
		{
			return;
		}

		foreach ( FolderEntryViewModel entry in Folders.Where( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) ).ToList() )
		{
			entry.IconPath = iconPath;
		}
	}

	private void RenameGroup( string groupName )
	{
		string? newName = InputDialog.Ask( T( "Dialog_Rename" ), T( "Dialog_NewName" ), groupName );

		if ( newName is null || newName == groupName )
		{
			return;
		}

		foreach ( FolderEntryViewModel entry in Folders.Where( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) ).ToList() )
		{
			entry.TabName = newName;
		}
	}

	private async Task DeleteGroupAsync( string groupName )
	{
		if ( !ConfirmDialog.Ask( T( "Msg_RemoveTitle" ), string.Format( T( "Msg_DeleteFolder" ), groupName ), T( "Btn_Remove" ) ) )
		{
			return;
		}

		foreach ( FolderEntryViewModel entry in Folders.Where( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) ).ToList() )
		{
			Folders.Remove( entry );
		}

		HasNoFolders = Folders.Count == 0;
		SaveSettings();
		await SyncAsync();
	}

	[RelayCommand]
	public void AddCategory()
	{
		string? name = InputDialog.Ask( T( "Dialog_NewCategory" ), T( "Dialog_CategoryName" ) );

		if ( name is null || _Categories.Any( category => string.Equals( category, name, StringComparison.OrdinalIgnoreCase ) ) )
		{
			return;
		}

		_Categories.Add( name );
		SaveSettings();
		RebuildNavigation();
		SelectedCategoryItem = CategoryItems.FirstOrDefault( item => item.Title == name );
	}

	private List<string> GetOrderedGroupNames()
	{
		List<string> names = _AllCards.Where( card => card.Game.Platform == GamePlatform.Folder && !string.IsNullOrWhiteSpace( card.Game.GroupName ) ).Select( card => card.Game.GroupName! ).Distinct().ToList();

		switch ( FolderSort )
		{
			case FolderSortMode.Manual:
				return names.OrderBy( FindFolderIndex ).ThenBy( name => name, StringComparer.CurrentCultureIgnoreCase ).ToList();
			case FolderSortMode.GameCount:
				return names.OrderByDescending( name => _AllCards.Count( card => card.Game.Platform == GamePlatform.Folder && card.Game.GroupName == name ) ).ThenBy( name => name, StringComparer.CurrentCultureIgnoreCase ).ToList();
			default:
				return names.OrderBy( name => name, StringComparer.CurrentCultureIgnoreCase ).ToList();
		}
	}

	private int FindFolderIndex( string groupName )
	{
		int index = Folders.ToList().FindIndex( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) );
		return index < 0 ? int.MaxValue : index;
	}

	[RelayCommand]
	public void SetFolderSort( string? mode )
	{
		if ( Enum.TryParse( mode, out FolderSortMode parsedMode ) )
		{
			FolderSort = parsedMode;
		}
	}

	[RelayCommand]
	public void MoveFolderUp( FolderEntryViewModel? entry )
	{
		MoveFolder( entry, -1 );
	}

	[RelayCommand]
	public void MoveFolderDown( FolderEntryViewModel? entry )
	{
		MoveFolder( entry, 1 );
	}

	private void MoveFolder( FolderEntryViewModel? entry, int direction )
	{
		int index = entry is null ? -1 : Folders.IndexOf( entry );
		int target = index + direction;

		if ( index < 0 || target < 0 || target >= Folders.Count )
		{
			return;
		}

		Folders.Move( index, target );
		SaveSettings();

		if ( FolderSort == FolderSortMode.Manual )
		{
			RebuildNavigation();
			ApplyFilter();
		}
		else
		{
			FolderSort = FolderSortMode.Manual;
		}
	}

	private async Task EditGroupAsync( string groupName )
	{
		List<FolderEntryViewModel> entries = Folders.Where( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) ).ToList();

		if ( entries.Count > 0 )
		{
			await EditFolderEntriesAsync( groupName, entries );
		}
	}

	[RelayCommand]
	public async Task EditFolderAsync( FolderEntryViewModel? entry )
	{
		if ( entry is not null )
		{
			await EditFolderEntriesAsync( entry.TabName, new List<FolderEntryViewModel> { entry } );
		}
	}

	private async Task EditFolderEntriesAsync( string tabName, List<FolderEntryViewModel> entries )
	{
		FolderEditWindow window = new FolderEditWindow( tabName, entries.FirstOrDefault( entry => entry.HasIcon )?.IconPath, entries ) { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() != true )
		{
			return;
		}

		_IsApplyingFolderEdit = true;

		foreach ( FolderDraftViewModel draft in window.Drafts )
		{
			if ( draft.IsRemoved )
			{
				Folders.Remove( draft.Source );
				continue;
			}

			draft.Source.TabName = window.TabName;
			draft.Source.IconPath = window.IconPath;
			draft.Source.IsEnabled = draft.IsEnabled;
			draft.Source.ScanMode = ( FolderScanMode ) draft.ScanModeIndex;
		}

		_IsApplyingFolderEdit = false;
		HasNoFolders = Folders.Count == 0;
		SaveSettings();
		await SyncAsync();
	}

	private void OpenGroupFolder( string groupName )
	{
		FolderEntryViewModel? entry = Folders.FirstOrDefault( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) && Directory.Exists( folder.Path ) );

		if ( entry is not null )
		{
			Process.Start( new ProcessStartInfo( "explorer.exe", $"\"{entry.Path}\"" ) { UseShellExecute = true } );
		}
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
	private void ChangeCategoryColor( string categoryName )
	{
		_CategoryColors.TryGetValue( categoryName, out string? currentColor );
		ColorPickerWindow window = new ColorPickerWindow( categoryName, currentColor ) { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() != true )
		{
			return;
		}

		if ( window.IsReset )
		{
			_CategoryColors.Remove( categoryName );
		}
		else if ( window.SelectedColor is not null )
		{
			_CategoryColors[ categoryName ] = window.SelectedColor;
		}

		SaveSettings();
		RebuildNavigation();
		ApplyFilter();
	}

	[RelayCommand]
	public void SetSort( string? mode )
	{
		if ( Enum.TryParse( mode, out GameSortMode parsedMode ) )
		{
			SortMode = parsedMode;
		}
	}

	[RelayCommand]
	public void ClearFilters()
	{
		_IsBuildingFilters = true;

		foreach ( FilterOptionViewModel option in FilterGroups.SelectMany( group => group.Options ) )
		{
			option.IsSelected = false;
		}

		_SelectedFilterKeys.Clear();
		_IsBuildingFilters = false;
		RefreshActiveFilters();
		ApplyFilter();
	}

	[RelayCommand]
	public void RemoveFilter( FilterOptionViewModel? option )
	{
		if ( option is not null )
		{
			option.IsSelected = false;
		}
	}

	[RelayCommand]
	public void ToggleFilterPanel()
	{
		IsFilterPanelOpen = !IsFilterPanelOpen;
	}

	private void OnFilterOptionChanged( FilterOptionViewModel option )
	{
		if ( _IsBuildingFilters )
		{
			return;
		}

		if ( option.IsSelected )
		{
			_SelectedFilterKeys.Add( option.Key );
		}
		else
		{
			_SelectedFilterKeys.Remove( option.Key );
		}

		RefreshActiveFilters();
		ApplyFilter();
	}

	private void RefreshActiveFilters()
	{
		ActiveFilters.Clear();

		foreach ( FilterOptionViewModel option in FilterGroups.SelectMany( group => group.Options ).Where( option => option.IsSelected ) )
		{
			ActiveFilters.Add( option );
		}

		ActiveFilterCount = ActiveFilters.Count;
		HasActiveFilters = ActiveFilters.Count > 0;
	}

	private FilterGroupViewModel AddFilterGroup( string id, string titleKey )
	{
		FilterGroupViewModel group = new FilterGroupViewModel( id, T( titleKey ) ) { IsExpanded = !_CollapsedFilterGroups.Contains( id ) };
		group.PropertyChanged += ( _, args ) =>
		{
			if ( args.PropertyName == nameof( FilterGroupViewModel.IsExpanded ) )
			{
				if ( group.IsExpanded )
				{
					_CollapsedFilterGroups.Remove( id );
				}
				else
				{
					_CollapsedFilterGroups.Add( id );
				}
			}
		};
		FilterGroups.Add( group );
		return group;
	}

	private void AddFilterOption( FilterGroupViewModel group, string key, string label )
	{
		FilterOptionViewModel option = new FilterOptionViewModel( key, label ) { IsSelected = _SelectedFilterKeys.Contains( key ) };
		option.Changed = OnFilterOptionChanged;
		group.Options.Add( option );
	}

	private void BuildFilters()
	{
		_IsBuildingFilters = true;
		FilterGroups.Clear();
		List<GameCardViewModel> visibleCards = _AllCards.Where( card => !card.Game.IsHidden ).ToList();

		FilterGroupViewModel platformGroup = AddFilterGroup( "platform", "Filter_Platforms" );

		foreach ( GamePlatform platform in visibleCards.Select( card => card.Game.Platform ).Distinct().OrderBy( platform => platform ) )
		{
			AddFilterOption( platformGroup, $"platform:{platform}", GameCardViewModel.GetPlatformLabel( platform ) );
		}

		FilterGroupViewModel statusGroup = AddFilterGroup( "status", "Filter_Status" );
		AddFilterOption( statusGroup, "status:favorites", T( "Filter_Favorites" ) );
		AddFilterOption( statusGroup, "status:played", T( "Filter_Played" ) );
		AddFilterOption( statusGroup, "status:unplayed", T( "Filter_Unplayed" ) );
		AddFilterOption( statusGroup, "status:running", T( "Filter_Running" ) );

		List<string> categoryNames = _Categories.Concat( visibleCards.Where( card => !string.IsNullOrEmpty( card.CategoryName ) ).Select( card => card.CategoryName ) ).Distinct().OrderBy( name => name, StringComparer.CurrentCultureIgnoreCase ).ToList();

		if ( categoryNames.Count > 0 )
		{
			FilterGroupViewModel categoryGroup = AddFilterGroup( "category", "Filter_Categories" );

			foreach ( string categoryName in categoryNames )
			{
				AddFilterOption( categoryGroup, $"category:{categoryName}", categoryName );
			}
		}

		List<string> folderNames = GetOrderedGroupNames();

		if ( folderNames.Count > 0 )
		{
			FilterGroupViewModel folderGroup = AddFilterGroup( "folder", "Filter_Folders" );

			foreach ( string folderName in folderNames )
			{
				AddFilterOption( folderGroup, $"folder:{folderName}", folderName );
			}
		}

		FilterGroupViewModel playtimeGroup = AddFilterGroup( "playtime", "Filter_Playtime" );
		AddFilterOption( playtimeGroup, "playtime:lt1", T( "Playtime_Lt1" ) );
		AddFilterOption( playtimeGroup, "playtime:1to10", T( "Playtime_1to10" ) );
		AddFilterOption( playtimeGroup, "playtime:10to50", T( "Playtime_10to50" ) );
		AddFilterOption( playtimeGroup, "playtime:gt50", T( "Playtime_Gt50" ) );

		FilterGroupViewModel sizeGroup = AddFilterGroup( "size", "Filter_Size" );
		AddFilterOption( sizeGroup, "size:lt1", T( "Size_Lt1" ) );
		AddFilterOption( sizeGroup, "size:1to10", T( "Size_1to10" ) );
		AddFilterOption( sizeGroup, "size:10to50", T( "Size_10to50" ) );
		AddFilterOption( sizeGroup, "size:gt50", T( "Size_Gt50" ) );

		FilterGroupViewModel recentGroup = AddFilterGroup( "recent", "Filter_LastPlayed" );
		AddFilterOption( recentGroup, "recent:7d", T( "Recent_7d" ) );
		AddFilterOption( recentGroup, "recent:30d", T( "Recent_30d" ) );
		AddFilterOption( recentGroup, "recent:older", T( "Recent_Older" ) );
		AddFilterOption( recentGroup, "recent:never", T( "Recent_Never" ) );

		_IsBuildingFilters = false;
		RefreshActiveFilters();
		UpdateFilterCounts();
	}

	private void UpdateFilterCounts()
	{
		List<GameCardViewModel> visibleCards = _AllCards.Where( card => !card.Game.IsHidden ).ToList();

		foreach ( FilterOptionViewModel option in FilterGroups.SelectMany( group => group.Options ) )
		{
			option.Count = visibleCards.Count( card => CardMatchesKey( card, option.Key ) );
		}
	}

	private bool MatchesFilters( GameCardViewModel card )
	{
		if ( _SelectedFilterKeys.Count == 0 )
		{
			return true;
		}

		return _SelectedFilterKeys
			.GroupBy( key => key[ ..key.IndexOf( ':' ) ] )
			.All( group => group.Any( key => CardMatchesKey( card, key ) ) );
	}

	private static bool CardMatchesKey( GameCardViewModel card, string key )
	{
		int separator = key.IndexOf( ':' );
		string kind = key[ ..separator ];
		string value = key[ ( separator + 1 ).. ];
		double hours = card.PlaytimeSeconds / 3600.0;
		double gigabytes = card.SizeBytes / 1073741824.0;
		DateTime now = DateTime.Now;

		return kind switch
		{
			"platform" => card.Game.Platform.ToString() == value,
			"category" => card.CategoryName == value,
			"folder" => card.Game.Platform == GamePlatform.Folder && card.Game.GroupName == value,
			"status" => value switch
			{
				"favorites" => card.IsFavorite,
				"played" => card.PlaytimeSeconds > 0,
				"unplayed" => card.PlaytimeSeconds == 0,
				"running" => card.IsRunning,
				_ => false
			},
			"playtime" => value switch
			{
				"lt1" => hours < 1,
				"1to10" => hours >= 1 && hours < 10,
				"10to50" => hours >= 10 && hours < 50,
				"gt50" => hours >= 50,
				_ => false
			},
			"size" => card.SizeBytes > 0 && value switch
			{
				"lt1" => gigabytes < 1,
				"1to10" => gigabytes >= 1 && gigabytes < 10,
				"10to50" => gigabytes >= 10 && gigabytes < 50,
				"gt50" => gigabytes >= 50,
				_ => false
			},
			"recent" => value switch
			{
				"7d" => card.LastPlayedAt is not null && card.LastPlayedAt >= now.AddDays( -7 ),
				"30d" => card.LastPlayedAt is not null && card.LastPlayedAt >= now.AddDays( -30 ),
				"older" => card.LastPlayedAt is not null && card.LastPlayedAt < now.AddDays( -30 ),
				"never" => card.LastPlayedAt is null,
				_ => false
			},
			_ => false
		};
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

	public ( double? Left, double? Top ) GetOverlayPosition()
	{
		AppSettings settings = _SettingsService.Load();
		return ( settings.OverlayLeft, settings.OverlayTop );
	}

	public void SaveOverlayPosition( double left, double top )
	{
		AppSettings settings = _SettingsService.Load();
		settings.OverlayLeft = left;
		settings.OverlayTop = top;
		_SettingsService.Save( settings );
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

	[RelayCommand]
	public async Task RefreshNewsAsync()
	{
		IsNewsLoading = true;
		NewsStatus = T( "News_Loading" );
		List<NewsFeed> feeds = NewsFeedEntries.Select( feed => new NewsFeed { Title = feed.Title, Url = feed.Url } ).ToList();

		if ( IncludeGameNews )
		{
			IEnumerable<GameCardViewModel> steamCards = _AllCards
				.Where( card => card.Game.Platform == GamePlatform.Steam && !card.Game.IsHidden && long.TryParse( card.Game.PlatformGameId, out _ ) )
				.OrderByDescending( card => card.PlaytimeSeconds )
				.ThenBy( card => card.Name )
				.Take( 8 );

			foreach ( GameCardViewModel card in steamCards )
			{
				feeds.Add( new NewsFeed { Title = card.Name, Url = NewsService.BuildSteamAppFeedUrl( card.Game.PlatformGameId ) } );
			}
		}

		IReadOnlyList<NewsEntry> entries = await _NewsService.FetchAsync( feeds );
		NewsItems.Clear();

		foreach ( NewsEntry entry in entries )
		{
			NewsItems.Add( new NewsItemViewModel( entry ) );
		}

		HasNoNews = NewsItems.Count == 0;
		NewsStatus = string.Format( T( "News_Updated" ), DateTime.Now.ToString( "HH:mm" ), feeds.Count );
		IsNewsLoading = false;
	}

	[RelayCommand]
	public async Task AddFeedAsync()
	{
		string? url = InputDialog.Ask( T( "News_AddFeed" ), T( "News_FeedUrl" ), "https://" );

		if ( url is null || !Uri.TryCreate( url, UriKind.Absolute, out Uri? uri ) || uri.Scheme is not ( "http" or "https" ) || NewsFeedEntries.Any( feed => string.Equals( feed.Url, url, StringComparison.OrdinalIgnoreCase ) ) )
		{
			return;
		}

		string title = InputDialog.Ask( T( "News_AddFeed" ), T( "News_FeedTitle" ), uri.Host ) ?? uri.Host;
		NewsFeedEntries.Add( new FeedEntryViewModel( title, url ) );
		SaveSettings();
		await RefreshNewsAsync();
	}

	[RelayCommand]
	public async Task RemoveFeedAsync( FeedEntryViewModel? feed )
	{
		if ( feed is null )
		{
			return;
		}

		NewsFeedEntries.Remove( feed );
		SaveSettings();
		await RefreshNewsAsync();
	}

	public async Task RunAutomaticBackupAsync()
	{
		AppSettings settings = _SettingsService.Load();

		if ( settings.LastBackupAt is not null && ( DateTime.Now - settings.LastBackupAt.Value ).TotalHours < 24 )
		{
			return;
		}

		try
		{
			await _BackupService.CreateAutomaticBackupAsync();
			settings.LastBackupAt = DateTime.Now;
			_SettingsService.Save( settings );
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException )
		{
			DebugLog.Write( $"Automatic backup failed{Environment.NewLine}{exception}" );
		}
	}

	[RelayCommand]
	public async Task BackupNowAsync()
	{
		try
		{
			string path = await _BackupService.CreateAutomaticBackupAsync();
			BackupStatus = string.Format( T( "Backup_Done" ), Path.GetFileName( path ) );
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException )
		{
			BackupStatus = exception.Message;
		}
	}

	[RelayCommand]
	public async Task ExportBackupAsync()
	{
		SaveFileDialog dialog = new SaveFileDialog { Title = T( "Backup_Export" ), Filter = "MateLauncher backup (*.zip)|*.zip", FileName = $"MateLauncher-{DateTime.Now:yyyyMMdd}.zip" };

		if ( dialog.ShowDialog() != true )
		{
			return;
		}

		try
		{
			await _BackupService.ExportAsync( dialog.FileName );
			BackupStatus = string.Format( T( "Backup_Done" ), Path.GetFileName( dialog.FileName ) );
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException )
		{
			BackupStatus = exception.Message;
		}
	}

	[RelayCommand]
	public void ImportBackup()
	{
		OpenFileDialog dialog = new OpenFileDialog { Title = T( "Backup_Import" ), Filter = "MateLauncher backup (*.zip)|*.zip" };

		if ( dialog.ShowDialog() != true || !ConfirmDialog.Ask( T( "Backup_Import" ), T( "Backup_ImportConfirm" ), T( "Backup_ImportButton" ) ) )
		{
			return;
		}

		try
		{
			_BackupService.StageImport( dialog.FileName );
		}
		catch ( Exception exception ) when ( exception is IOException or InvalidDataException or UnauthorizedAccessException )
		{
			BackupStatus = exception.Message;
			return;
		}

		AppRestarter.Restart();
	}

	[RelayCommand]
	public void OpenBackupsFolder()
	{
		Directory.CreateDirectory( _BackupService.BackupsFolder );
		Process.Start( new ProcessStartInfo( "explorer.exe", $"\"{_BackupService.BackupsFolder}\"" ) { UseShellExecute = true } );
	}
	private async Task RenameCategoryAsync( string categoryName )
	{
		string? newName = InputDialog.Ask( T( "Dialog_Rename" ), T( "Dialog_NewName" ), categoryName );

		if ( newName is null || newName == categoryName || _Categories.Contains( newName ) )
		{
			return;
		}

		_Categories[ _Categories.IndexOf( categoryName ) ] = newName;

		if ( _CategoryColors.Remove( categoryName, out string? movedColor ) )
		{
			_CategoryColors[ newName ] = movedColor;
		}

		SaveSettings();
		await _Repository.RenameCategoryAsync( categoryName, newName );
		await RefreshAsync();
	}

	private async Task DeleteCategoryAsync( string categoryName )
	{
		if ( !ConfirmDialog.Ask( T( "Msg_RemoveTitle" ), string.Format( T( "Msg_DeleteCategory" ), categoryName ), T( "Btn_Delete" ) ) )
		{
			return;
		}

		_Categories.Remove( categoryName );
		_CategoryColors.Remove( categoryName );
		SaveSettings();
		await _Repository.RenameCategoryAsync( categoryName, null );
		await RefreshAsync();
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
		IsSettingsOpen = true;
	}

	[RelayCommand]
	public void OpenInstallFolder( GameCardViewModel? card )
	{
		if ( card is not null && card.HasInstallPath )
		{
			Process.Start( new ProcessStartInfo( card.InstallPath ) { UseShellExecute = true } );
		}
	}

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

	partial void OnSearchTextChanged( string value )
	{
		ApplyFilter();
	}

	partial void OnSelectedLibraryItemChanged( NavItemViewModel? value )
	{
		OnNavigationSelected( value, nameof( SelectedLibraryItem ) );
	}

	partial void OnSelectedToolItemChanged( NavItemViewModel? value )
	{
		OnNavigationSelected( value, nameof( SelectedToolItem ) );
	}

	partial void OnSelectedPlatformItemChanged( NavItemViewModel? value )
	{
		OnNavigationSelected( value, nameof( SelectedPlatformItem ) );
	}

	partial void OnSelectedCategoryItemChanged( NavItemViewModel? value )
	{
		OnNavigationSelected( value, nameof( SelectedCategoryItem ) );
	}

	partial void OnSelectedGroupItemChanged( NavItemViewModel? value )
	{
		OnNavigationSelected( value, nameof( SelectedGroupItem ) );
	}

	private void OnNavigationSelected( NavItemViewModel? value, string source )
	{
		if ( _IsRebuildingNavigation )
		{
			return;
		}

		if ( value is not null )
		{
			if ( source != nameof( SelectedLibraryItem ) )
			{
				SelectedLibraryItem = null;
			}

			if ( source != nameof( SelectedToolItem ) )
			{
				SelectedToolItem = null;
			}

			if ( source != nameof( SelectedPlatformItem ) )
			{
				SelectedPlatformItem = null;
			}

			if ( source != nameof( SelectedCategoryItem ) )
			{
				SelectedCategoryItem = null;
			}

			if ( source != nameof( SelectedGroupItem ) )
			{
				SelectedGroupItem = null;
			}

			SelectedGame = null;
			IsSettingsOpen = false;
			IsStatsOpen = value.Kind == NavKind.Stats;
			IsDownloadsOpen = value.Kind == NavKind.Downloads;
			IsNewsOpen = value.Kind == NavKind.News;

			if ( IsStatsOpen )
			{
				_ = LoadStatsAsync();
			}

			if ( IsDownloadsOpen )
			{
				_ = RefreshUpdatesAsync();
			}

			if ( IsNewsOpen && NewsItems.Count == 0 )
			{
				_ = RefreshNewsAsync();
			}
		}

		ApplyFilter();
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
		ICoverFinder storeFinder = new CompositeCoverFinder( new SteamStoreCoverFinder(), new GoogleImageCoverFinder( _SettingsService ) );
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

	public void CycleNavigation( int direction )
	{
		List<NavItemViewModel> items = LibraryItems.Concat( ToolItems ).Concat( PlatformItems ).Concat( CategoryItems ).Concat( GroupItems ).ToList();
		NavItemViewModel? current = SelectedLibraryItem ?? SelectedToolItem ?? SelectedPlatformItem ?? SelectedCategoryItem ?? SelectedGroupItem;
		int index = current is null ? 0 : items.IndexOf( current );
		NavItemViewModel next = items[ ( index + direction + items.Count ) % items.Count ];

		switch ( next.Kind )
		{
			case NavKind.Platform:
				SelectedPlatformItem = next;
				break;
			case NavKind.Category:
				SelectedCategoryItem = next;
				break;
			case NavKind.Group:
				SelectedGroupItem = next;
				break;
			case NavKind.Stats:
			case NavKind.Downloads:
				SelectedToolItem = next;
				break;
			default:
				SelectedLibraryItem = next;
				break;
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

	private void RebuildNavigation()
	{
		GamePlatform? previousPlatform = SelectedPlatformItem?.Platform;
		string? previousGroup = SelectedGroupItem?.Title;
		string? previousCategory = SelectedCategoryItem?.Title;
		_IsRebuildingNavigation = true;

		PlatformItems.Clear();

		foreach ( GamePlatform platform in _AllCards.Select( card => card.Game.Platform ).Where( platform => platform != GamePlatform.Folder ).Distinct().OrderBy( platform => platform ) )
		{
			PlatformItems.Add( new NavItemViewModel( GameCardViewModel.GetPlatformLabel( platform ), platform == GamePlatform.Xbox ? "" : "", NavKind.Platform, platform ) { Icon = PlatformIcons.Get( platform ) } );
		}

		CategoryItems.Clear();

		foreach ( string categoryName in _Categories.Concat( _AllCards.Where( card => !string.IsNullOrEmpty( card.CategoryName ) ).Select( card => card.CategoryName ) ).Distinct().OrderBy( name => name, StringComparer.CurrentCultureIgnoreCase ) )
		{
			string capturedName = categoryName;

			CategoryItems.Add( new NavItemViewModel( capturedName, "", NavKind.Category )
			{
				ColorBrush = _CategoryColors.TryGetValue( capturedName, out string? categoryHex ) ? AppTheme.ToBrush( categoryHex ) : null,
					ColorCommand = new RelayCommand( () => ChangeCategoryColor( capturedName ) ),
					RenameCommand = new AsyncRelayCommand( () => RenameCategoryAsync( capturedName ) ),
				DeleteCommand = new AsyncRelayCommand( () => DeleteCategoryAsync( capturedName ) )
			} );
		}

		GroupItems.Clear();

		if ( MergeFolders )
		{
			if ( GetOrderedGroupNames().Count > 0 )
			{
				GroupItems.Add( new NavItemViewModel( T( "Nav_Folders" ), "", NavKind.Group ) { IsMerged = true } );
			}
		}
		else
		{
			foreach ( string groupName in GetOrderedGroupNames() )
			{
				string capturedName = groupName;

				GroupItems.Add( new NavItemViewModel( capturedName, "", NavKind.Group )
				{
					Icon = Folders.FirstOrDefault( folder => string.Equals( folder.TabName, capturedName, StringComparison.OrdinalIgnoreCase ) && folder.HasIcon )?.Icon,
					EditCommand = new AsyncRelayCommand( () => EditGroupAsync( capturedName ) ),
					OpenCommand = new RelayCommand( () => OpenGroupFolder( capturedName ) ),
					DeleteCommand = new AsyncRelayCommand( () => DeleteGroupAsync( capturedName ) )
				} );
			}
		}

		HasGroups = GroupItems.Count > 0;
		SelectedPlatformItem = PlatformItems.FirstOrDefault( item => item.Platform == previousPlatform && previousPlatform is not null );
		SelectedGroupItem = GroupItems.FirstOrDefault( item => item.Title == previousGroup && previousGroup is not null );
		SelectedCategoryItem = CategoryItems.FirstOrDefault( item => item.Title == previousCategory && previousCategory is not null );

		bool nothingSelected = SelectedPlatformItem is null && SelectedGroupItem is null && SelectedCategoryItem is null && SelectedLibraryItem is null && SelectedToolItem is null;

		if ( nothingSelected && !IsSettingsOpen )
		{
			SelectedLibraryItem = LibraryItems[ 0 ];
		}

		_IsRebuildingNavigation = false;
		BuildFilters();
	}

	private void ApplyFilter()
	{
		List<GameCardViewModel> visibleCards = _AllCards.Where( card => !card.Game.IsHidden ).ToList();

		LibraryItems[ 0 ].Count = visibleCards.Count;
		LibraryItems[ 1 ].Count = visibleCards.Count( card => card.IsFavorite );
		LibraryItems[ 2 ].Count = visibleCards.Count( card => card.LastPlayedAt is not null );

		foreach ( NavItemViewModel platformItem in PlatformItems )
		{
			platformItem.Count = visibleCards.Count( card => card.Game.Platform == platformItem.Platform );
		}

		foreach ( NavItemViewModel categoryItem in CategoryItems )
		{
			categoryItem.Count = visibleCards.Count( card => card.CategoryName == categoryItem.Title );
		}

		foreach ( NavItemViewModel groupItem in GroupItems )
		{
			groupItem.Count = visibleCards.Count( card => card.Game.Platform == GamePlatform.Folder && ( groupItem.IsMerged || card.Game.GroupName == groupItem.Title ) );
		}

		IEnumerable<GameCardViewModel> filtered = visibleCards;
		bool sortByRecent = false;

		if ( SelectedPlatformItem is not null )
		{
			filtered = filtered.Where( card => card.Game.Platform == SelectedPlatformItem.Platform );
		}
		else if ( SelectedCategoryItem is not null )
		{
			filtered = filtered.Where( card => card.CategoryName == SelectedCategoryItem.Title );
		}
		else if ( SelectedGroupItem is not null )
		{
			filtered = filtered.Where( card => card.Game.Platform == GamePlatform.Folder && ( SelectedGroupItem.IsMerged || card.Game.GroupName == SelectedGroupItem.Title ) );
		}
		else if ( SelectedLibraryItem?.Kind == NavKind.Favorites )
		{
			filtered = filtered.Where( card => card.IsFavorite );
		}
		else if ( SelectedLibraryItem?.Kind == NavKind.Recent )
		{
			filtered = filtered.Where( card => card.LastPlayedAt is not null );
			sortByRecent = true;
		}

		if ( SelectedLibraryItem?.Kind == NavKind.Unplayed )
		{
			filtered = filtered.Where( card => card.PlaytimeSeconds == 0 );
		}
		else if ( SelectedLibraryItem?.Kind == NavKind.Large )
		{
			filtered = filtered.Where( card => card.SizeBytes >= LargeGameBytes );
		}

		filtered = filtered.Where( MatchesFilters );

		filtered = filtered.Where( card => card.Name.Contains( SearchText, StringComparison.OrdinalIgnoreCase ) );		List<GameCardViewModel> result = ( sortByRecent ? GameSortMode.LastPlayed : SortMode ) switch
		{
			GameSortMode.Playtime => filtered.OrderByDescending( card => card.PlaytimeSeconds ).ThenBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).ToList(),
			GameSortMode.LastPlayed => filtered.OrderByDescending( card => card.LastPlayedAt ).ThenBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).ToList(),
			GameSortMode.Size => filtered.OrderByDescending( card => card.SizeBytes ).ThenBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).ToList(),
			GameSortMode.Platform => filtered.OrderBy( card => card.Game.Platform ).ThenBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).ToList(),
			_ => filtered.OrderBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).ToList()
		};
		Games.Clear();

		foreach ( GameCardViewModel card in result )
		{
			Games.Add( card );
		}

		UpdateFilterCounts();
		StatusText = string.Format( T( "Status_Items" ), Games.Count );
	}
}
