using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MateLauncher.App.Localization;
using MateLauncher.App.ViewModels;

namespace MateLauncher.App;

public partial class OverlayWindow : Window
{
	private readonly MainViewModel _ViewModel;
	private readonly DispatcherTimer _Timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds( 1 ) };
	private long _TodaySeconds;
	private int _TicksSinceTodayRefresh;

	public OverlayWindow( MainViewModel viewModel )
	{
		InitializeComponent();
		_ViewModel = viewModel;
		DataContext = viewModel;
		_ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		_Timer.Tick += async ( _, _ ) => await RefreshAsync();
		IsVisibleChanged += async ( _, _ ) =>
		{
			if ( IsVisible )
			{
				_TicksSinceTodayRefresh = 0;
				_TodaySeconds = await _ViewModel.GetTodaySecondsAsync();
				await RefreshAsync();
				_Timer.Start();
			}
			else
			{
				_Timer.Stop();
			}
		};
	}

	public void Toggle()
	{
		if ( IsVisible )
		{
			Hide();
			return;
		}

		( double? savedLeft, double? savedTop ) = _ViewModel.GetOverlayPosition();
		Rect workArea = SystemParameters.WorkArea;
		Left = Math.Clamp( savedLeft ?? workArea.Right - Width - 12, workArea.Left, Math.Max( workArea.Left, workArea.Right - Width ) );
		Top = Math.Clamp( savedTop ?? workArea.Top + 48, workArea.Top, Math.Max( workArea.Top, workArea.Bottom - 120 ) );
		Show();
		Activate();
	}

	private void OnViewModelPropertyChanged( object? sender, System.ComponentModel.PropertyChangedEventArgs e )
	{
		if ( e.PropertyName == nameof( MainViewModel.ActiveGame ) && _ViewModel.ActiveGame is null && IsVisible )
		{
			Hide();
		}
	}

	private void OnHeaderMouseDown( object sender, MouseButtonEventArgs e )
	{
		if ( e.ChangedButton != MouseButton.Left )
		{
			return;
		}

		DragMove();
		_ViewModel.SaveOverlayPosition( Left, Top );
	}

	private async Task RefreshAsync()
	{
		ClockText.Text = DateTime.Now.ToString( "HH:mm" );
		HintText.Text = string.Format( Localizer.Instance.Get( "Overlay_Hint" ), _ViewModel.OverlayHotkeyText );
		GameCardViewModel? game = _ViewModel.ActiveGame;
		StopButton.Visibility = game is null ? Visibility.Collapsed : Visibility.Visible;

		StateText.Text = Localizer.Instance.Get( game is null ? "Overlay_Idle" : "TitleBar_Playing" );

		if ( game is null )
		{
			GameNameText.Text = Localizer.Instance.Get( "Overlay_NoGame" );
			PlatformText.Text = string.Empty;
			SessionText.Text = "—";
			TotalText.Text = "—";
		}
		else
		{
			TimeSpan session = DateTime.Now - ( _ViewModel.SessionStartedAt ?? DateTime.Now );
			GameNameText.Text = game.Name;
			PlatformText.Text = game.PlatformLabel;
			SessionText.Text = session.TotalHours >= 1 ? session.ToString( @"h\:mm\:ss" ) : session.ToString( @"mm\:ss" );
			TotalText.Text = game.PlaytimeText;
		}

		if ( ++_TicksSinceTodayRefresh % 30 == 0 )
		{
			_TodaySeconds = await _ViewModel.GetTodaySecondsAsync();
		}

		long liveSession = game is null ? 0 : ( long ) ( DateTime.Now - ( _ViewModel.SessionStartedAt ?? DateTime.Now ) ).TotalSeconds;
		TodayText.Text = GameCardViewModel.FormatDuration( _TodaySeconds + liveSession );
	}

	private void OnOpenLauncherClicked( object sender, RoutedEventArgs e )
	{
		Window? main = Application.Current.MainWindow;

		if ( main is not null )
		{
			main.Show();
			main.WindowState = WindowState.Normal;
			main.Activate();
		}

		Hide();
	}

	private void OnStopClicked( object sender, RoutedEventArgs e )
	{
		_ViewModel.StopGameCommand.Execute( _ViewModel.ActiveGame );
	}

	private void OnCloseClicked( object sender, RoutedEventArgs e )
	{
		Hide();
	}

	private void OnKeyDown( object sender, KeyEventArgs e )
	{
		if ( e.Key == Key.Escape )
		{
			Hide();
		}
	}
}
