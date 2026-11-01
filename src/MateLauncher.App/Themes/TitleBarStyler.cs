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

	[DllImport( "dwmapi.dll" )]
	private static extern int DwmSetWindowAttribute( IntPtr windowHandle, int attribute, ref int value, int size );

	public static void RegisterForAllWindows()
	{
		EventManager.RegisterClassHandler( typeof( Window ), FrameworkElement.LoadedEvent, new RoutedEventHandler( ( sender, _ ) => Apply( ( Window ) sender ) ) );
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

		int captionColor = ToColorRef( GetBrushColor( "SidebarBrush", Colors.Black ) );
		int borderColor = ToColorRef( GetBrushColor( "BorderBrush", Colors.Gray ) );
		int textColor = ToColorRef( GetBrushColor( "TextBrush", Colors.White ) );
		DwmSetWindowAttribute( handle, DwmCaptionColor, ref captionColor, sizeof( int ) );
		DwmSetWindowAttribute( handle, DwmBorderColor, ref borderColor, sizeof( int ) );
		DwmSetWindowAttribute( handle, DwmTextColor, ref textColor, sizeof( int ) );
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
