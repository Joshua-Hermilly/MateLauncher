using System.Windows;
using MateLauncher.App.Localization;
using MateLauncher.App.Themes;

namespace MateLauncher.App;

public partial class ColorPickerWindow : Window
{
	public string? SelectedColor { get; private set; }
	public bool IsReset { get; private set; }

	public ColorPickerWindow( string categoryName, string? currentColor )
	{
		InitializeComponent();
		HeaderText.Text = string.Format( Localizer.Instance.Get( "Color_Header" ), categoryName );
		Picker.Hex = AppTheme.IsValidColor( currentColor ) ? currentColor! : "#A855F7";
	}

	private void OnOkClicked( object sender, RoutedEventArgs e )
	{
		SelectedColor = Picker.Hex;
		DialogResult = true;
	}

	private void OnResetClicked( object sender, RoutedEventArgs e )
	{
		IsReset = true;
		DialogResult = true;
	}
}
