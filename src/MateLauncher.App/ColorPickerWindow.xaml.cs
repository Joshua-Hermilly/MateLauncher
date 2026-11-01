using System.Windows;
using System.Windows.Controls;
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
