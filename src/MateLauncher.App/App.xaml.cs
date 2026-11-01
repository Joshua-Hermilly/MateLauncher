using System.IO;
using System.Windows;
using System.Windows.Input;
using MateLauncher.App.Localization;
using MateLauncher.App.Startup;
using MateLauncher.App.ViewModels;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;
using MateLauncher.Infrastructure.Backup;
using MateLauncher.Infrastructure.Covers;
using MateLauncher.Infrastructure.Downloads;
using MateLauncher.Infrastructure.News;
using MateLauncher.Infrastructure.Launching;
using MateLauncher.Infrastructure.Metadata;
using MateLauncher.Infrastructure.Plugins;
using MateLauncher.Infrastructure.Settings;
using MateLauncher.Infrastructure.Persistence;
using MateLauncher.App.Themes;
using MateLauncher.Providers.BattleNet;
using MateLauncher.Providers.Ea;
using MateLauncher.Providers.Epic;
using MateLauncher.Providers.Folders;
using MateLauncher.Providers.Gog;
using MateLauncher.Providers.Ubisoft;
using MateLauncher.Providers.Riot;
using MateLauncher.Providers.Xbox;
using MateLauncher.Providers.Steam;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MateLauncher.App;

public partial class App : Application
{
	private ServiceProvider? _Services;

	protected override void OnStartup( StartupEventArgs e )
	{
		base.OnStartup( e );

		string dataFolder = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), "MateLauncher" );
		Directory.CreateDirectory( dataFolder );
		BackupService.ApplyPendingImport( dataFolder );

		DispatcherUnhandledException += ( _, args ) =>
		{
			StartupGuard.MarkCrash();
			File.AppendAllText( Path.Combine( dataFolder, "crash.log" ), $"{DateTime.Now:O}{Environment.NewLine}{args.Exception}{Environment.NewLine}{Environment.NewLine}" );
		};

		AppDomain.CurrentDomain.UnhandledException += ( _, _ ) => StartupGuard.MarkCrash();

		ServiceCollection services = new ServiceCollection();
		services.AddDbContextFactory<LauncherDbContext>( options => options.UseSqlite( $"Data Source={Path.Combine( dataFolder, "library.db" )}" ) );
		services.AddSingleton<ILibraryRepository, LibraryRepository>();
		services.AddSingleton<ICoverCache>( _ => new CoverCache( Path.Combine( dataFolder, "covers" ) ) );
		services.AddSingleton<IGameProvider, SteamProvider>();
		services.AddSingleton<IGameProvider, EpicProvider>();
		services.AddSingleton<IGameProvider, GogProvider>();
		services.AddSingleton<IGameProvider, UbisoftProvider>();
		services.AddSingleton<IGameProvider, EaAppProvider>();
		services.AddSingleton<IGameProvider, BattleNetProvider>();
		services.AddSingleton<IGameProvider, XboxProvider>();
		services.AddSingleton<IGameProvider, RiotProvider>();
		services.AddSingleton<IGameProvider, FolderProvider>();
		services.AddSingleton<IGameProvider>( _ => new PluginGameProvider( Path.Combine( dataFolder, "plugins" ) ) );
		services.AddSingleton( provider => new IgdbClient( provider.GetRequiredService<ISettingsService>(), Path.Combine( dataFolder, "metadata-cache-v2.json" ) ) );
		services.AddSingleton<IGameMetadataProvider>( provider => provider.GetRequiredService<IgdbClient>() );
		services.AddSingleton<ICoverFinder>( provider => new CompositeCoverFinder( new SteamStoreCoverFinder(), new SteamGridDbCoverFinder( provider.GetRequiredService<ISettingsService>() ), provider.GetRequiredService<IgdbClient>(), new WikipediaCoverFinder(), new GoogleImageCoverFinder( provider.GetRequiredService<ISettingsService>() ) ) );
		services.AddSingleton<IUpdateChecker, SteamUpdateChecker>();
		services.AddSingleton<ThemeService>();
		services.AddSingleton<ISettingsService>( _ => new JsonSettingsService( Path.Combine( dataFolder, "settings.json" ) ) );
		services.AddSingleton<IPerformanceMode, PerformanceMode>();
		services.AddSingleton<IPlaytimeTracker, PlaytimeTracker>();
		services.AddSingleton<IGameLauncher, ProcessGameLauncher>();
		services.AddSingleton<RuntimeOptions>();
		services.AddSingleton<NewsService>();
		services.AddSingleton<SteamStoreSearch>();
		services.AddSingleton( provider => new BackupService( provider.GetRequiredService<IDbContextFactory<LauncherDbContext>>(), dataFolder ) );
		services.AddSingleton<LibrarySyncService>();
		services.AddSingleton<MainViewModel>();
		services.AddSingleton<MainWindow>();
		_Services = services.BuildServiceProvider();

		using ( LauncherDbContext context = _Services.GetRequiredService<IDbContextFactory<LauncherDbContext>>().CreateDbContext() )
		{
			DatabaseInitializer.Initialize( context );
		}

		AppSettings settings = _Services.GetRequiredService<ISettingsService>().Load();
		ThemeService themeService = _Services.GetRequiredService<ThemeService>();
		themeService.SetCustomThemes( settings.CustomThemes );
		themeService.SetTranslucency( settings.TranslucencyLevel );
		themeService.Apply( settings.ThemeId, settings.AccentColor );
		TitleBarStyler.RegisterForAllWindows();
		Localizer.Instance.SetLanguage( settings.Language );

		RuntimeOptions runtimeOptions = _Services.GetRequiredService<RuntimeOptions>();
		runtimeOptions.DebugMode = e.Args.Contains( "--debug" );

		if ( runtimeOptions.DebugMode )
		{
			DebugLog.Enable( Path.Combine( dataFolder, "debug.log" ) );
		}

		bool wasInterrupted = StartupGuard.WasInterrupted();
		bool safeModeRequested = e.Args.Contains( "--safe" ) || Keyboard.IsKeyDown( Key.LeftShift ) || Keyboard.IsKeyDown( Key.RightShift );

		if ( wasInterrupted || safeModeRequested )
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown;
			SafeModeWindow safeModeWindow = new SafeModeWindow( settings, wasInterrupted && !safeModeRequested );

			if ( safeModeWindow.ShowDialog() != true )
			{
				Shutdown();
				return;
			}

			_Services.GetRequiredService<ISettingsService>().Save( settings );
			runtimeOptions.SafeMode = safeModeWindow.StartInSafeMode;
			DebugLog.Write( $"Startup: interrupted={wasInterrupted}, safe={runtimeOptions.SafeMode}" );
		}

		MainWindow mainWindow = _Services.GetRequiredService<MainWindow>();
		MainWindow = mainWindow;
		ShutdownMode = ShutdownMode.OnMainWindowClose;
		mainWindow.Show();

		if ( e.Args.Contains( "--minimized" ) )
		{
			mainWindow.HideToTrayIfEnabled();
		}

		_ = RunStartupTasksAsync( _Services.GetRequiredService<MainViewModel>() );
	}

	private static async Task RunStartupTasksAsync( MainViewModel viewModel )
	{
		await viewModel.RunAutomaticBackupAsync();
		await viewModel.SyncAsync();
		await viewModel.CheckUpdatesOnStartupAsync();
	}

	protected override void OnExit( ExitEventArgs e )
	{
		_Services?.Dispose();
		base.OnExit( e );
	}
}
