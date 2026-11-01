using System.Windows;

namespace MateLauncher.App;

public enum AddChoice
{
	Game,
	Library
}

public partial class AddChoiceWindow : Window
{
	public AddChoice Choice { get; private set; }

	public AddChoiceWindow()
	{
		InitializeComponent();
	}

	private void OnGameClicked( object sender, RoutedEventArgs e )
	{
		Choice = AddChoice.Game;
		DialogResult = true;
	}

	private void OnLibraryClicked( object sender, RoutedEventArgs e )
	{
		Choice = AddChoice.Library;
		DialogResult = true;
	}
}
