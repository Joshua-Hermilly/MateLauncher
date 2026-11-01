using System.Windows;

namespace MateLauncher.App;

public partial class ConfirmDialog : Window
{
	private ConfirmDialog( string title, string message, string confirmLabel )
	{
		InitializeComponent();
		Title = title;
		MessageText.Text = message;
		ConfirmButton.Content = confirmLabel;
	}

	public static bool Ask( string title, string message, string confirmLabel )
	{
		ConfirmDialog dialog = new ConfirmDialog( title, message, confirmLabel ) { Owner = Application.Current.MainWindow };
		return dialog.ShowDialog() == true;
	}

	private void OnConfirmClicked( object sender, RoutedEventArgs e )
	{
		DialogResult = true;
	}
}
