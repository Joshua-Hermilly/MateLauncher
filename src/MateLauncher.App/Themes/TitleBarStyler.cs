using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MateLauncher.App.Themes;

public static class TitleBarStyler
{
	private const int DwmUseImmersiveDarkMode = 20;
	private const int DwmCaptionColor = 35;
	private const int DwmBorderColor = 34;
	private const int DwmTextColor = 36;
	private const int DwmSystemBackdropType = 38;
	private const int BackdropNone = 1;
	private const int BackdropAcrylic = 3;
	private const int ColorNone = unchecked( ( int ) 0xFFFFFFFE );
	private const int MinimumBackdropBuild = 22621;

	[StructLayout( LayoutKind.Sequential )]
	private struct Margins
	{
		public int Left;
		public int Right;
		public int Top;
		public int Bottom;
	}

	[DllImport( "dwmapi.dll" )]
	private static extern int DwmSetWindowAttribute( IntPtr windowHandle, int attribute, ref int value, int size );

	[DllImport( "dwmapi.dll" )]
	private static extern int DwmExtendFrameIntoClientArea( IntPtr windowHandle, ref Margins margins );

	public static bool IsBackdropSupported => OperatingSystem.IsWindowsVersionAtLeast( 10, 0, MinimumBackdropBuild );

	public static bool BackdropEnabled { get; set; }

	public static void RegisterForAllWindows()
	{
		EventManager.RegisterClassHandler( typeof( Window ), FrameworkElement.LoadedEvent, new RoutedEventHandler( ( sender, _ ) => Apply( ( Window ) sender ) ) );
	}

	public static void ApplyToAllWindows()
	{
		foreach ( Window window in Application.Current.Windows )
		{
			Apply( window );
		}
	}

	public static void Apply( Window window )
	{
		IntPtr handle = new WindowInteropHelper( window ).Handle;

		if ( handle == IntPtr.Zero )
		{
			return;
		}

		int enabled = 1;
		DwmSetWindowAttribute( handle, DwmUseImmersiveDarkMode, ref enabled, sizeof( int ) );

		bool useBackdrop = BackdropEnabled && IsBackdropSupported && !window.AllowsTransparency;
		int captionColor = useBackdrop ? ColorNone : ToColorRef( GetBrushColor( "SidebarBrush", Colors.Black ) );
		int borderColor = ToColorRef( GetBrushColor( "BorderBrush", Colors.Gray ) );
		int textColor = ToColorRef( GetBrushColor( "TextBrush", Colors.White ) );
		DwmSetWindowAttribute( handle, DwmCaptionColor, ref captionColor, sizeof( int ) );
		DwmSetWindowAttribute( handle, DwmBorderColor, ref borderColor, sizeof( int ) );
		DwmSetWindowAttribute( handle, DwmTextColor, ref textColor, sizeof( int ) );

		if ( !IsBackdropSupported || window.AllowsTransparency )
		{
			return;
		}

		HwndSource? source = HwndSource.FromHwnd( handle );

		if ( source?.CompositionTarget is not null )
		{
			source.CompositionTarget.BackgroundColor = useBackdrop ? Colors.Transparent : Colors.Black;
		}

		Margins margins = useBackdrop ? new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 } : new Margins();
		DwmExtendFrameIntoClientArea( handle, ref margins );

		int backdrop = useBackdrop ? BackdropAcrylic : BackdropNone;
		DwmSetWindowAttribute( handle, DwmSystemBackdropType, ref backdrop, sizeof( int ) );
	}

	private static Color GetBrushColor( string resourceKey, Color fallback )
	{
		return Application.Current.Resources[ resourceKey ] is SolidColorBrush brush ? brush.Color : fallback;
	}

	private static int ToColorRef( Color color )
	{
		return color.R | ( color.G << 8 ) | ( color.B << 16 );
	}
}
