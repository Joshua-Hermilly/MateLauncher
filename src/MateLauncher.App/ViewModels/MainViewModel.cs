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
	private readonly IGameMetadataProvider _MetadataProvider;

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
	private double _TranslucencyLevel = 60;

	public bool IsTranslucencySupported => TitleBarStyler.IsBackdropSupported;

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
	private string _SteamGridDbApiKey = string.Empty;

	[ObservableProperty]
	private string _IgdbClientId = string.Empty;

	[ObservableProperty]
	private string _IgdbClientSecret = string.Empty;

	[ObservableProperty]
	private string _GameSummary = string.Empty;

	[ObservableProperty]
	private string _GameInfoLine = string.Empty;

	[ObservableProperty]
	private bool _HasGameInfo;

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

	public string VersionBadge => "v" + CurrentVersion;

	public string CurrentVersionText => string.Format( T( "Update_Current" ), CurrentVersion );

	public ObservableCollection<ThemeOptionViewModel> BuiltInThemeOptions { get; } = new ObservableCollection<ThemeOptionViewModel>();

	public ObservableCollection<ThemeOptionViewModel> CustomThemeOptions { get; } = new ObservableCollection<ThemeOptionViewModel>();

	[ObservableProperty]
	private bool _HasNoCustomThemes = true;

	public ObservableCollection<AccentOptionViewModel> AccentOptions { get; } = new ObservableCollection<AccentOptionViewModel>();

	public ObservableCollection<FolderEntryViewModel> Folders { get; } = new ObservableCollection<FolderEntryViewModel>();

	public ObservableCollection<UpdateItemViewModel> PendingUpdates { get; } = new ObservableCollection<UpdateItemViewModel>();

	public MainViewModel( LibrarySyncService syncService, IGameLauncher gameLauncher, ILibraryRepository repository, ICoverCache coverCache, ICoverFinder coverFinder, IPlaytimeTracker playtimeTracker, ISettingsService settingsService, IUpdateChecker updateChecker, ThemeService themeService, RuntimeOptions runtimeOptions, BackupService backupService, NewsService newsService, SteamStoreSearch steamStoreSearch, IGameMetadataProvider metadataProvider )
	{
		_IsLoadingSettings = true;
		_SettingsService = settingsService;
		_ThemeService = themeService;
		_RuntimeOptions = runtimeOptions;
		IsSafeMode = runtimeOptions.SafeMode;
		PropertyChanged += OnPageStateChanged;
		_BackupService = backupService;
		_NewsService = newsService;
		_SteamStoreSearch = steamStoreSearch;
		_MetadataProvider = metadataProvider;
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
		TranslucencyLevel = settings.TranslucencyLevel;
		OverlayEnabled = settings.OverlayEnabled;
		FolderSort = settings.FolderSort;
		MergeFolders = settings.MergeFolders;
		SortMode = settings.SortMode;
		MinimizeToTray = settings.MinimizeToTray;
		StartWithWindows = settings.StartWithWindows;
		OverlayHotkey = settings.OverlayHotkey;
		IncludeGameNews = settings.IncludeGameNews;
		CheckUpdatesOnStartup = settings.CheckUpdatesOnStartup;
		PerformanceModeEnabled = settings.PerformanceModeEnabled;
		PerformancePrograms = string.Join( ", ", settings.PerformancePrograms );
		_ThemeService.SetCustomThemes( settings.CustomThemes );
		GoogleApiKey = settings.GoogleApiKey;
		SteamGridDbApiKey = settings.SteamGridDbApiKey;
		IgdbClientId = settings.IgdbClientId;
		IgdbClientSecret = settings.IgdbClientSecret;
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
			new NavItemViewModel( string.Empty, "\uE80F", NavKind.Home, titleKey: "Nav_Home" ),
			new NavItemViewModel( string.Empty, "", NavKind.All, titleKey: "Nav_All" ),
			new NavItemViewModel( string.Empty, "", NavKind.Favorites, titleKey: "Nav_Favorites" ),
			new NavItemViewModel( string.Empty, "", NavKind.Recent, titleKey: "Nav_Recent" )
		};

		ToolItems = new ObservableCollection<NavItemViewModel>
		{
			new NavItemViewModel( string.Empty, "", NavKind.Stats, titleKey: "Nav_Stats" ),
			new NavItemViewModel( string.Empty, "", NavKind.Downloads, titleKey: "Nav_Downloads" ),
			new NavItemViewModel( string.Empty, "\uE8A5", NavKind.News, titleKey: "Nav_News" ),
			new NavItemViewModel( string.Empty, "", NavKind.Friends, titleKey: "Nav_Friends" )
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

	public ( double? Left, double? Top ) GetOverlayPosition()
	{
		AppSettings settings = _SettingsService.Load();
		return ( settings.OverlayLeft, settings.OverlayTop );
	}
}
