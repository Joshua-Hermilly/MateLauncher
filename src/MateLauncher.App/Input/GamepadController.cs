using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MateLauncher.App.ViewModels;

namespace MateLauncher.App.Input;

public class GamepadController
{
	private const ushort DPadUp = 0x0001;
	private const ushort DPadDown = 0x0002;
	private const ushort DPadLeft = 0x0004;
	private const ushort DPadRight = 0x0008;
	private const ushort StartButton = 0x0010;
	private const ushort LeftShoulder = 0x0100;
	private const ushort RightShoulder = 0x0200;
	private const ushort ButtonA = 0x1000;
	private const ushort ButtonB = 0x2000;
	private const ushort ButtonX = 0x4000;
	private const ushort ButtonY = 0x8000;
	private const short StickThreshold = 16000;
	private const double SliderStep = 10;

	private static readonly TimeSpan _FirstRepeatDelay = TimeSpan.FromMilliseconds( 350 );
	private static readonly TimeSpan _RepeatInterval = TimeSpan.FromMilliseconds( 110 );

	private readonly MainWindow _Window;
	private readonly MainViewModel _ViewModel;
	private readonly DispatcherTimer _Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds( 16 ) };
	private ushort _PreviousButtons;
	private FocusNavigationDirection? _HeldDirection;
	private DateTime _NextRepeatAt;
	private IInputElement? _LibraryFocus;
	private bool _HasPadInput;

	[StructLayout( LayoutKind.Sequential )]
	private struct XInputGamepad
	{
		public ushort Buttons;
		public byte LeftTrigger;
		public byte RightTrigger;
		public short ThumbLeftX;
		public short ThumbLeftY;
		public short ThumbRightX;
		public short ThumbRightY;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct XInputState
	{
		public uint PacketNumber;
		public XInputGamepad Gamepad;
	}

	[DllImport( "xinput1_4.dll", EntryPoint = "XInputGetState" )]
	private static extern uint XInputGetState( uint userIndex, out XInputState state );

	public GamepadController( MainWindow window, MainViewModel viewModel )
	{
		_Window = window;
		_ViewModel = viewModel;
		_Timer.Tick += ( _, _ ) => Poll();
		_ViewModel.PropertyChanged += OnViewModelChanged;
	}

	public void Start()
	{
		_Timer.Start();
	}

	private void Poll()
	{
		if ( !_Window.IsActive || !TryReadState( out XInputGamepad pad ) )
		{
			_PreviousButtons = 0;
			_HeldDirection = null;
			return;
		}

		ushort pressed = ( ushort ) ( pad.Buttons & ~_PreviousButtons );
		_PreviousButtons = pad.Buttons;

		HandleDirection( ReadDirection( pad ) );
		HandleButtons( pressed );
	}

	private static bool TryReadState( out XInputGamepad pad )
	{
		pad = default;

		try
		{
			for ( uint index = 0; index < 4; index++ )
			{
				if ( XInputGetState( index, out XInputState state ) == 0 )
				{
					pad = state.Gamepad;
					return true;
				}
			}
		}
		catch ( Exception exception ) when ( exception is DllNotFoundException or EntryPointNotFoundException )
		{
		}

		return false;
	}

	private static FocusNavigationDirection? ReadDirection( XInputGamepad pad )
	{
		if ( ( pad.Buttons & DPadUp ) != 0 || pad.ThumbLeftY > StickThreshold )
		{
			return FocusNavigationDirection.Up;
		}

		if ( ( pad.Buttons & DPadDown ) != 0 || pad.ThumbLeftY < -StickThreshold )
		{
			return FocusNavigationDirection.Down;
		}

		if ( ( pad.Buttons & DPadLeft ) != 0 || pad.ThumbLeftX < -StickThreshold )
		{
			return FocusNavigationDirection.Left;
		}

		if ( ( pad.Buttons & DPadRight ) != 0 || pad.ThumbLeftX > StickThreshold )
		{
			return FocusNavigationDirection.Right;
		}

		return null;
	}

	private void HandleDirection( FocusNavigationDirection? direction )
	{
		if ( direction is null )
		{
			_HeldDirection = null;
			return;
		}

		_HasPadInput = true;

		DateTime now = DateTime.UtcNow;

		if ( direction != _HeldDirection )
		{
			_HeldDirection = direction;
			_NextRepeatAt = now + _FirstRepeatDelay;
		}
		else if ( now >= _NextRepeatAt )
		{
			_NextRepeatAt = now + _RepeatInterval;
		}
		else
		{
			return;
		}

		Move( direction.Value );
	}

	private void Move( FocusNavigationDirection direction )
	{
		if ( !EnsureFocus() )
		{
			return;
		}

		if ( Keyboard.FocusedElement is DependencyObject sidebarFocus && IsInside( sidebarFocus, _Window.SidebarPanel ) )
		{
			MoveInSidebar( direction );
			return;
		}

		if ( Keyboard.FocusedElement is Slider slider && direction is FocusNavigationDirection.Left or FocusNavigationDirection.Right )
		{
			slider.Value = Math.Clamp( slider.Value + ( direction == FocusNavigationDirection.Right ? SliderStep : -SliderStep ), slider.Minimum, slider.Maximum );
			return;
		}

		if ( direction == FocusNavigationDirection.Left && Keyboard.FocusedElement is UIElement origin )
		{
			MoveLeftOrEnterSidebar( origin );
			return;
		}

		( Keyboard.FocusedElement as UIElement )?.MoveFocus( new TraversalRequest( direction ) );
	}

	private void MoveLeftOrEnterSidebar( UIElement origin )
	{
		double startLeft = GetLeft( origin );
		bool moved = origin.MoveFocus( new TraversalRequest( FocusNavigationDirection.Left ) );
		IInputElement? current = Keyboard.FocusedElement;
		bool hasProgressed = moved && !ReferenceEquals( current, origin ) && current is UIElement target && GetLeft( target ) < startLeft - 1;

		if ( hasProgressed )
		{
			return;
		}

		Keyboard.Focus( origin );
		EnterSidebar();
	}

	private void MoveInSidebar( FocusNavigationDirection direction )
	{
		if ( direction == FocusNavigationDirection.Right )
		{
			FocusContent();
			return;
		}

		if ( direction == FocusNavigationDirection.Left )
		{
			return;
		}

		List<UIElement> targets = GetSidebarTargets();
		int index = Keyboard.FocusedElement is UIElement focused ? targets.IndexOf( focused ) : -1;
		int next = index < 0 ? 0 : Math.Clamp( index + ( direction == FocusNavigationDirection.Down ? 1 : -1 ), 0, targets.Count - 1 );
		FocusSidebarTarget( targets, next );
	}

	private void EnterSidebar()
	{
		List<UIElement> targets = GetSidebarTargets();
		int selected = targets.FindIndex( element => element is ListBoxItem { IsSelected: true } );
		FocusSidebarTarget( targets, selected < 0 ? 0 : selected );
	}

	private static void FocusSidebarTarget( List<UIElement> targets, int index )
	{
		if ( index < 0 || index >= targets.Count )
		{
			return;
		}

		Keyboard.Focus( targets[ index ] );
		( targets[ index ] as FrameworkElement )?.BringIntoView();
	}

	private List<UIElement> GetSidebarTargets()
	{
		List<( UIElement Element, double Top )> found = new List<( UIElement Element, double Top )>();
		CollectSidebarTargets( _Window.SidebarPanel, found );
		return found.OrderBy( item => item.Top ).Select( item => item.Element ).ToList();
	}

	private void CollectSidebarTargets( DependencyObject parent, List<( UIElement Element, double Top )> found )
	{
		for ( int index = 0; index < VisualTreeHelper.GetChildrenCount( parent ); index++ )
		{
			DependencyObject child = VisualTreeHelper.GetChild( parent, index );

			if ( child is UIElement { IsVisible: true, IsEnabled: true, Focusable: true } element && ( element is ListBoxItem || element is Button ) )
			{
				try
				{
					found.Add( ( element, element.TransformToAncestor( _Window.SidebarPanel ).Transform( new Point( 0, 0 ) ).Y ) );
				}
				catch ( InvalidOperationException )
				{
				}

				continue;
			}

			CollectSidebarTargets( child, found );
		}
	}

	private double GetLeft( IInputElement? element )
	{
		if ( element is not UIElement uiElement )
		{
			return 0;
		}

		try
		{
			return uiElement.TransformToAncestor( _Window ).Transform( new Point( 0, 0 ) ).X;
		}
		catch ( InvalidOperationException )
		{
			return 0;
		}
	}

	private void FocusContent()
	{
		FrameworkElement? page = GetVisiblePage();

		if ( ReferenceEquals( page, _Window.DetailPage ) )
		{
			FocusDetail();
		}
		else if ( ReferenceEquals( page, _Window.LibraryPage ) )
		{
			RestoreLibraryFocus();
		}
		else
		{
			page?.MoveFocus( new TraversalRequest( FocusNavigationDirection.First ) );
		}
	}

	private FrameworkElement? GetVisiblePage()
	{
		FrameworkElement[] pages = { _Window.DetailPage, _Window.HomePage, _Window.StatsPage, _Window.DownloadsPage, _Window.FriendsPage, _Window.NewsPage, _Window.SettingsPage, _Window.LibraryPage };
		return pages.FirstOrDefault( candidate => candidate.IsVisible );
	}

	private void HandleButtons( ushort pressed )
	{
		if ( pressed == 0 )
		{
			return;
		}

		_HasPadInput = true;

		if ( ( pressed & ButtonA ) != 0 && EnsureFocus() )
		{
			Activate( Keyboard.FocusedElement as DependencyObject );
		}

		if ( ( pressed & ButtonB ) != 0 )
		{
			if ( Keyboard.FocusedElement is DependencyObject focusedForBack && IsInside( focusedForBack, _Window.SidebarPanel ) )
			{
				FocusContent();
			}
			else
			{
				_ViewModel.CloseDetailCommand.Execute( null );
			}
		}

		if ( ( pressed & ButtonX ) != 0 && GetFocusedCard() is GameCardViewModel launchCard )
		{
			_ViewModel.LaunchGameCommand.Execute( launchCard );
		}

		if ( ( pressed & ButtonY ) != 0 && GetFocusedCard() is GameCardViewModel favoriteCard )
		{
			_ViewModel.ToggleFavoriteCommand.Execute( favoriteCard );
		}

		if ( ( pressed & LeftShoulder ) != 0 )
		{
			_ViewModel.CycleNavigation( -1 );
		}

		if ( ( pressed & RightShoulder ) != 0 )
		{
			_ViewModel.CycleNavigation( 1 );
		}

		if ( ( pressed & StartButton ) != 0 )
		{
			_ViewModel.OpenSettingsCommand.Execute( null );
		}
	}

	private GameCardViewModel? GetFocusedCard()
	{
		if ( !EnsureFocus() )
		{
			return null;
		}

		return ( Keyboard.FocusedElement as FrameworkElement )?.DataContext as GameCardViewModel ?? _ViewModel.SelectedGame;
	}

	private static void Activate( DependencyObject? element )
	{
		switch ( element )
		{
			case RadioButton radio:
				radio.IsChecked = true;
				break;
			case ToggleButton toggle:
				toggle.IsChecked = toggle.IsChecked != true;
				break;
			case Button button:
				( new ButtonAutomationPeer( button ).GetPattern( PatternInterface.Invoke ) as IInvokeProvider )?.Invoke();
				break;
			case ListBoxItem item:
				item.IsSelected = true;
				break;
		}
	}

	private void OnViewModelChanged( object? sender, PropertyChangedEventArgs e )
	{
		if ( e.PropertyName != nameof( MainViewModel.SelectedGame ) || !_HasPadInput )
		{
			return;
		}

		if ( _ViewModel.SelectedGame is not null )
		{
			if ( Keyboard.FocusedElement is DependencyObject focused && IsInside( focused, _Window.LibraryPage ) )
			{
				_LibraryFocus = Keyboard.FocusedElement;
			}

			_Window.Dispatcher.BeginInvoke( DispatcherPriority.Loaded, FocusDetail );
		}
		else
		{
			_Window.Dispatcher.BeginInvoke( DispatcherPriority.Loaded, RestoreLibraryFocus );
		}
	}

	private void FocusDetail()
	{
		Button? play = FindButton( _Window.DetailPage, button => ReferenceEquals( button.Command, _ViewModel.LaunchGameCommand ) );

		if ( play is { IsVisible: true, IsEnabled: true } )
		{
			Keyboard.Focus( play );
			return;
		}

		_Window.DetailPage.MoveFocus( new TraversalRequest( FocusNavigationDirection.First ) );
	}

	private void RestoreLibraryFocus()
	{
		if ( _LibraryFocus is UIElement { IsVisible: true, IsEnabled: true } element )
		{
			Keyboard.Focus( element );
			return;
		}

		_Window.LibraryPage.MoveFocus( new TraversalRequest( FocusNavigationDirection.First ) );
	}

	private static Button? FindButton( DependencyObject parent, Func<Button, bool> predicate )
	{
		for ( int index = 0; index < VisualTreeHelper.GetChildrenCount( parent ); index++ )
		{
			DependencyObject child = VisualTreeHelper.GetChild( parent, index );

			if ( child is Button button && predicate( button ) )
			{
				return button;
			}

			Button? found = FindButton( child, predicate );

			if ( found is not null )
			{
				return found;
			}
		}

		return null;
	}

	private static bool IsInside( DependencyObject? element, DependencyObject container )
	{
		while ( element is not null )
		{
			if ( ReferenceEquals( element, container ) )
			{
				return true;
			}

			element = element is Visual ? VisualTreeHelper.GetParent( element ) : LogicalTreeHelper.GetParent( element );
		}

		return false;
	}

	private bool EnsureFocus()
	{
		if ( _Window.DetailPage.IsVisible && Keyboard.FocusedElement is DependencyObject current && IsInside( current, _Window.LibraryPage ) )
		{
			FocusDetail();
		}

		if ( Keyboard.FocusedElement is UIElement { IsVisible: true, IsEnabled: true } and not Window )
		{
			return true;
		}

		FrameworkElement? page = GetVisiblePage();

		return page is not null && page.MoveFocus( new TraversalRequest( FocusNavigationDirection.First ) );
	}
}
