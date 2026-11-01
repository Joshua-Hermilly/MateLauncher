using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MateLauncher.App.Input;
using MateLauncher.App.Startup;
using MateLauncher.App.ViewModels;

namespace MateLauncher.App;

public partial class MainWindow : Window
{
	private const int DwmUseImmersiveDarkMode = 20;
	private const double SlideDistance = 28;
	private const int OverlayHotKeyId = 0x4D4C;
	private const int WmHotKey = 0x0312;
	private const uint ModNoRepeat = 0x4000;

	private static readonly Duration _TransitionDuration = new Duration( TimeSpan.FromMilliseconds( 280 ) );
	private static readonly string[] _FallbackHotkeys = { "Ctrl+Alt+O", "Ctrl+Shift+O", "Ctrl+Alt+G", "Ctrl+Shift+Space", "Ctrl+Shift+F9" };

	private readonly MainViewModel _ViewModel;
	private readonly TrayService _Tray;
	private OverlayWindow? _Overlay;
	private bool _IsQuitting;

	public MainWindow( MainViewModel viewModel )
	{
		InitializeComponent();
		_ViewModel = viewModel;
		DataContext = viewModel;
		SourceInitialized += OnSourceInitialized;
		Closed += OnWindowClosed;

		if ( !viewModel.IsSafeMode )
		{
			new GamepadController( this, viewModel ).Start();
		}

		_Tray = new TrayService( ShowFromTray, ToggleOverlay, Quit );
		_Tray.SetVisible( viewModel.MinimizeToTray );
		Application.Current.SessionEnding += ( _, _ ) => _IsQuitting = true;
		viewModel.PropertyChanged += OnViewModelPropertyChanged;
	}

	public void ShowFromTray()
	{
		Show();

		if ( WindowState == WindowState.Minimized )
		{
			WindowState = WindowState.Normal;
		}

		Activate();
	}

	public void HideToTrayIfEnabled()
	{
		if ( _ViewModel.MinimizeToTray )
		{
			Hide();
		}
		else
		{
			WindowState = WindowState.Minimized;
		}
	}

	public void ToggleOverlay()
	{
		if ( !_ViewModel.OverlayEnabled )
		{
			return;
		}

		_Overlay ??= new OverlayWindow( _ViewModel );
		_Overlay.Toggle();
	}

	private void Quit()
	{
		Dispatcher.Invoke( () =>
		{
			_IsQuitting = true;
			Close();
		} );
	}

	protected override void OnClosing( CancelEventArgs e )
	{
		if ( _ViewModel.MinimizeToTray && !_IsQuitting )
		{
			e.Cancel = true;
			Hide();
			return;
		}

		base.OnClosing( e );
	}

	private void OnWindowClosed( object? sender, EventArgs e )
	{
		UnregisterHotKey( new WindowInteropHelper( this ).Handle, OverlayHotKeyId );
		_Overlay?.Close();
		_Tray.Dispose();
	}

	private IntPtr OnWindowMessage( IntPtr windowHandle, int message, IntPtr wordParameter, IntPtr longParameter, ref bool handled )
	{
		if ( message == WmHotKey && wordParameter.ToInt32() == OverlayHotKeyId )
		{
			handled = true;
			ToggleOverlay();
		}

		return IntPtr.Zero;
	}

	private void OnViewModelPropertyChanged( object? sender, PropertyChangedEventArgs e )
	{
		switch ( e.PropertyName )
		{
			case nameof( MainViewModel.SelectedLibraryItem ) when _ViewModel.SelectedLibraryItem is not null:
			case nameof( MainViewModel.SelectedPlatformItem ) when _ViewModel.SelectedPlatformItem is not null:
			case nameof( MainViewModel.SelectedGroupItem ) when _ViewModel.SelectedGroupItem is not null:
			case nameof( MainViewModel.IsListView ):
				PlayTransition( LibraryPage );
				break;
			case nameof( MainViewModel.SelectedGame ):
				PlayTransition( _ViewModel.SelectedGame is null ? LibraryPage : DetailPage );
				break;
			case nameof( MainViewModel.IsSettingsOpen ) when _ViewModel.IsSettingsOpen:
				PlayTransition( SettingsPage );
				break;
			case nameof( MainViewModel.IsStatsOpen ) when _ViewModel.IsStatsOpen:
				PlayTransition( StatsPage );
				break;
			case nameof( MainViewModel.IsDownloadsOpen ) when _ViewModel.IsDownloadsOpen:
				PlayTransition( DownloadsPage );
				break;
			case nameof( MainViewModel.IsNewsOpen ) when _ViewModel.IsNewsOpen:
				PlayTransition( NewsPage );
				break;
			case nameof( MainViewModel.IsHomeOpen ) when _ViewModel.IsHomeOpen:
				PlayTransition( HomePage );
				break;
			case nameof( MainViewModel.SettingsSectionIndex ):
				PlayTransition( SettingsContent );
				break;
			case nameof( MainViewModel.MinimizeToTray ):
				_Tray.SetVisible( _ViewModel.MinimizeToTray );
				break;
			case nameof( MainViewModel.OverlayHotkey ):
				RegisterOverlayHotkey();
				break;
			case nameof( MainViewModel.Language ):
				_Tray.RefreshTexts();
				break;
		}
	}

	private void PlayTransition( FrameworkElement element )
	{
		if ( !_ViewModel.AnimationsEnabled )
		{
			return;
		}

		TranslateTransform slide = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
		element.RenderTransform = slide;

		CubicEase easing = new CubicEase { EasingMode = EasingMode.EaseOut };
		element.BeginAnimation( OpacityProperty, new DoubleAnimation( 0, 1, _TransitionDuration ) { EasingFunction = easing } );
		slide.BeginAnimation( TranslateTransform.XProperty, new DoubleAnimation( SlideDistance, 0, _TransitionDuration ) { EasingFunction = easing } );
	}

	private void OnMinimizeClicked( object sender, RoutedEventArgs e )
	{
		WindowState = WindowState.Minimized;
	}

	private void OnMaximizeClicked( object sender, RoutedEventArgs e )
	{
		WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
	}

	private void OnCloseClicked( object sender, RoutedEventArgs e )
	{
		Close();
	}

	protected override void OnStateChanged( EventArgs e )
	{
		base.OnStateChanged( e );
		bool isMaximized = WindowState == WindowState.Maximized;
		RootPanel.Margin = isMaximized ? new Thickness( 7 ) : new Thickness( 0 );
		MaximizeGlyph.Text = isMaximized ? "" : "";
	}

	private void OnStatsChartSizeChanged( object sender, SizeChangedEventArgs e )
	{
		_ViewModel.UpdateChartWidth( e.NewSize.Width - 42 );
	}

	private void OnSidebarMouseWheel( object sender, MouseWheelEventArgs e )
	{
		SidebarScroll.ScrollToVerticalOffset( SidebarScroll.VerticalOffset - e.Delta );
		e.Handled = true;
	}

	private void OnGameGridSizeChanged( object sender, SizeChangedEventArgs e )
	{
		_ViewModel.UpdateGridLayout( e.NewSize.Width );
	}

	[DllImport( "dwmapi.dll" )]
	private static extern int DwmSetWindowAttribute( IntPtr windowHandle, int attribute, ref int value, int size );

	[DllImport( "user32.dll" )]
	private static extern bool RegisterHotKey( IntPtr windowHandle, int id, uint modifiers, uint virtualKey );

	[DllImport( "user32.dll" )]
	private static extern bool UnregisterHotKey( IntPtr windowHandle, int id );

	private void OnSourceInitialized( object? sender, EventArgs e )
	{
		IntPtr handle = new WindowInteropHelper( this ).Handle;
		int enabled = 1;
		DwmSetWindowAttribute( handle, DwmUseImmersiveDarkMode, ref enabled, sizeof( int ) );
		HwndSource.FromHwnd( handle )?.AddHook( OnWindowMessage );
		RegisterOverlayHotkey();
	}

	private void RegisterOverlayHotkey()
	{
		IntPtr handle = new WindowInteropHelper( this ).Handle;
		UnregisterHotKey( handle, OverlayHotKeyId );
		_ViewModel.OverlayHotkeyText = string.Empty;

		if ( _ViewModel.IsSafeMode )
		{
			return;
		}

		IEnumerable<HotkeyDefinition> candidates = new[] { _ViewModel.OverlayHotkey }
			.Concat( _FallbackHotkeys )
			.Select( HotkeyDefinition.Parse )
			.OfType<HotkeyDefinition>();

		foreach ( HotkeyDefinition candidate in candidates )
		{
			if ( RegisterHotKey( handle, OverlayHotKeyId, candidate.Modifiers | ModNoRepeat, candidate.VirtualKey ) )
			{
				_ViewModel.OverlayHotkeyText = candidate.Text;
				return;
			}
		}
	}
}
