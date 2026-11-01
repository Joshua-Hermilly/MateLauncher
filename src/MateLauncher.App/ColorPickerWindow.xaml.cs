using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MateLauncher.App.Localization;

namespace MateLauncher.App;

public record ColorSwatch( string Hex, Brush Brush );

public partial class ColorPickerWindow : Window
{
	private static readonly string[] _Palette =
	{
		"#FF3B30", "#FF6B6B", "#FF9500", "#FFB020", "#FFD60A", "#A3E635",
		"#34C759", "#2DD4BF", "#06B6D4", "#0A84FF", "#5E5CE6", "#A855F7",
		"#D946EF", "#FF2D92", "#8E8E93", "#E8ECF1"
	};

	private double _Hue;
	private double _Saturation = 1;
	private double _Value = 1;
	private bool _IsSyncing;

	public string? SelectedColor { get; private set; }
	public bool IsReset { get; private set; }

	public ColorPickerWindow( string categoryName, string? currentColor )
	{
		InitializeComponent();
		HeaderText.Text = string.Format( Localizer.Instance.Get( "Color_Header" ), categoryName );
		SwatchList.ItemsSource = _Palette.Select( hex => new ColorSwatch( hex, ( Brush ) new BrushConverter().ConvertFromString( hex )! ) ).ToList();
		HexBox.Text = currentColor ?? _Palette[ 11 ];
	}

	private void OnSwatchClicked( object sender, RoutedEventArgs e )
	{
		if ( ( sender as FrameworkElement )?.DataContext is ColorSwatch swatch )
		{
			HexBox.Text = swatch.Hex;
		}
	}

	private void OnHexChanged( object sender, TextChangedEventArgs e )
	{
		Brush? brush = TryParse( HexBox.Text );
		Preview.Background = brush ?? ( Brush ) FindResource( "CardBrush" );

		if ( !_IsSyncing && brush is SolidColorBrush solid )
		{
			( double hue, double saturation, double value ) = ToHsv( solid.Color );

			if ( saturation > 0.001 && value > 0.001 )
			{
				_Hue = hue;
			}

			_Saturation = saturation;
			_Value = value;
			UpdateThumbs();
		}
	}

	private void OnAreaSizeChanged( object sender, SizeChangedEventArgs e )
	{
		UpdateThumbs();
	}

	private void OnSvMouseDown( object sender, MouseButtonEventArgs e )
	{
		SvArea.CaptureMouse();
		PickSaturationValue( e.GetPosition( SvArea ) );
	}

	private void OnSvMouseMove( object sender, MouseEventArgs e )
	{
		if ( SvArea.IsMouseCaptured )
		{
			PickSaturationValue( e.GetPosition( SvArea ) );
		}
	}

	private void OnHueMouseDown( object sender, MouseButtonEventArgs e )
	{
		HueArea.CaptureMouse();
		PickHue( e.GetPosition( HueArea ) );
	}

	private void OnHueMouseMove( object sender, MouseEventArgs e )
	{
		if ( HueArea.IsMouseCaptured )
		{
			PickHue( e.GetPosition( HueArea ) );
		}
	}

	private void OnAreaMouseUp( object sender, MouseButtonEventArgs e )
	{
		( sender as UIElement )?.ReleaseMouseCapture();
	}

	private void PickSaturationValue( Point position )
	{
		_Saturation = Math.Clamp( position.X / Math.Max( 1, SvArea.ActualWidth ), 0, 1 );
		_Value = 1 - Math.Clamp( position.Y / Math.Max( 1, SvArea.ActualHeight ), 0, 1 );
		WriteHexFromHsv();
	}

	private void PickHue( Point position )
	{
		_Hue = Math.Clamp( position.X / Math.Max( 1, HueArea.ActualWidth ), 0, 1 ) * 360;
		WriteHexFromHsv();
	}

	private void WriteHexFromHsv()
	{
		Color color = FromHsv( _Hue, _Saturation, _Value );
		_IsSyncing = true;
		HexBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
		_IsSyncing = false;
		UpdateThumbs();
	}

	private void UpdateThumbs()
	{
		SvArea.Background = new SolidColorBrush( FromHsv( _Hue, 1, 1 ) );
		Canvas.SetLeft( SvThumb, _Saturation * SvArea.ActualWidth - SvThumb.Width / 2 );
		Canvas.SetTop( SvThumb, ( 1 - _Value ) * SvArea.ActualHeight - SvThumb.Height / 2 );
		Canvas.SetLeft( HueThumb, _Hue / 360 * HueArea.ActualWidth - HueThumb.Width / 2 );
	}

	private static Color FromHsv( double hue, double saturation, double value )
	{
		double chroma = value * saturation;
		double section = hue / 60 % 6;
		double second = chroma * ( 1 - Math.Abs( section % 2 - 1 ) );
		double match = value - chroma;

		( double red, double green, double blue ) = ( int ) section switch
		{
			0 => ( chroma, second, 0.0 ),
			1 => ( second, chroma, 0.0 ),
			2 => ( 0.0, chroma, second ),
			3 => ( 0.0, second, chroma ),
			4 => ( second, 0.0, chroma ),
			_ => ( chroma, 0.0, second )
		};

		return Color.FromRgb( ( byte ) Math.Round( ( red + match ) * 255 ), ( byte ) Math.Round( ( green + match ) * 255 ), ( byte ) Math.Round( ( blue + match ) * 255 ) );
	}

	private static ( double Hue, double Saturation, double Value ) ToHsv( Color color )
	{
		double red = color.R / 255.0;
		double green = color.G / 255.0;
		double blue = color.B / 255.0;
		double max = Math.Max( red, Math.Max( green, blue ) );
		double delta = max - Math.Min( red, Math.Min( green, blue ) );
		double hue = 0;

		if ( delta > 0 )
		{
			if ( max == red )
			{
				hue = 60 * ( ( green - blue ) / delta % 6 );
			}
			else if ( max == green )
			{
				hue = 60 * ( ( blue - red ) / delta + 2 );
			}
			else
			{
				hue = 60 * ( ( red - green ) / delta + 4 );
			}
		}

		return ( hue < 0 ? hue + 360 : hue, max == 0 ? 0 : delta / max, max );
	}

	private static Brush? TryParse( string text )
	{
		string value = text.Trim();

		if ( value.Length == 0 )
		{
			return null;
		}

		if ( !value.StartsWith( '#' ) )
		{
			value = "#" + value;
		}

		try
		{
			return ( Brush? ) new BrushConverter().ConvertFromString( value );
		}
		catch ( FormatException )
		{
			return null;
		}
	}

	private void OnOkClicked( object sender, RoutedEventArgs e )
	{
		if ( TryParse( HexBox.Text ) is null )
		{
			HexBox.Focus();
			return;
		}

		string value = HexBox.Text.Trim();
		SelectedColor = value.StartsWith( '#' ) ? value : "#" + value;
		DialogResult = true;
	}

	private void OnResetClicked( object sender, RoutedEventArgs e )
	{
		IsReset = true;
		DialogResult = true;
	}
}
