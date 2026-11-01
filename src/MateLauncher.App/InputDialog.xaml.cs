using System.Windows;

namespace MateLauncher.App;

public partial class InputDialog : Window
{
	public string Value => ValueBox.Text.Trim();

	private InputDialog( string title, string label, string initialValue )
	{
		InitializeComponent();
		Title = title;
		LabelText.Text = label;
		ValueBox.Text = initialValue;
		Loaded += ( _, _ ) =>
		{
			ValueBox.Focus();
			ValueBox.SelectAll();
		};
	}

	public static string? Ask( string title, string label, string initialValue = "" )
	{
		InputDialog dialog = new InputDialog( title, label, initialValue ) { Owner = Application.Current.MainWindow };
		return dialog.ShowDialog() == true && dialog.Value.Length > 0 ? dialog.Value : null;
	}

	private void OnOkClicked( object sender, RoutedEventArgs e )
	{
		DialogResult = true;
	}
}
