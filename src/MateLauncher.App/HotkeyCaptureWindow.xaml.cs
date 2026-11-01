using System.Windows;
using System.Windows.Input;
using MateLauncher.App.Localization;
using MateLauncher.App.Startup;

namespace MateLauncher.App;

public partial class HotkeyCaptureWindow : Window
{
	public string? Hotkey { get; private set; }

	public HotkeyCaptureWindow( string currentHotkey )
	{
		InitializeComponent();
		CombinationText.Text = currentHotkey;
	}

	private void OnPreviewKeyDown( object sender, KeyEventArgs e )
	{
		Key key = e.Key == Key.System ? e.SystemKey : e.Key;

		if ( key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.Escape or Key.Tab or Key.Return )
		{
			if ( key == Key.Escape )
			{
				return;
			}

			e.Handled = key != Key.Tab && key != Key.Return;
			return;
		}

		e.Handled = true;
		uint modifiers = 0;

		if ( ( Keyboard.Modifiers & ModifierKeys.Control ) != 0 )
		{
			modifiers |= HotkeyDefinition.ModControl;
		}

		if ( ( Keyboard.Modifiers & ModifierKeys.Alt ) != 0 )
		{
			modifiers |= HotkeyDefinition.ModAlt;
		}

		if ( ( Keyboard.Modifiers & ModifierKeys.Shift ) != 0 )
		{
			modifiers |= HotkeyDefinition.ModShift;
		}

		string text = HotkeyDefinition.Format( modifiers, key );
		CombinationText.Text = text;

		if ( modifiers == 0 )
		{
			ErrorText.Text = Localizer.Instance.Get( "Hotkey_NeedModifier" );
			ErrorText.Visibility = Visibility.Visible;
			OkButton.IsEnabled = false;
			return;
		}

		ErrorText.Visibility = Visibility.Collapsed;
		Hotkey = text;
		OkButton.IsEnabled = true;
	}

	private void OnOkClicked( object sender, RoutedEventArgs e )
	{
		DialogResult = true;
	}
}
