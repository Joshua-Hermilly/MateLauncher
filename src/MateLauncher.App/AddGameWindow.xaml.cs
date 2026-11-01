using System.Diagnostics;
using System.IO;
using System.Windows;
using MateLauncher.Core.Models;
using Microsoft.Win32;

namespace MateLauncher.App;

public partial class AddGameWindow : Window
{
	public Game? NewGame { get; private set; }
	public string? CoverSourceFile { get; private set; }

	public AddGameWindow()
	{
		InitializeComponent();
	}

	private void OnBrowseExecutableClicked( object sender, RoutedEventArgs e )
	{
		OpenFileDialog dialog = new OpenFileDialog
		{
			Title = "Choisir l'exécutable",
			Filter = "Applications (*.exe;*.bat;*.lnk)|*.exe;*.bat;*.lnk|Tous les fichiers|*.*"
		};

		if ( dialog.ShowDialog( this ) != true )
		{
			return;
		}

		ExecutableBox.Text = dialog.FileName;

		if ( string.IsNullOrWhiteSpace( NameBox.Text ) )
		{
			string? productName = FileVersionInfo.GetVersionInfo( dialog.FileName ).ProductName;
			NameBox.Text = string.IsNullOrWhiteSpace( productName ) ? Path.GetFileNameWithoutExtension( dialog.FileName ) : productName;
		}
	}

	private void OnBrowseCoverClicked( object sender, RoutedEventArgs e )
	{
		OpenFileDialog dialog = new OpenFileDialog
		{
			Title = "Choisir une image",
			Filter = "Images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
		};

		if ( dialog.ShowDialog( this ) == true )
		{
			CoverBox.Text = dialog.FileName;
		}
	}

	private void OnAddClicked( object sender, RoutedEventArgs e )
	{
		string executable = ExecutableBox.Text.Trim();
		string name = NameBox.Text.Trim();
		string cover = CoverBox.Text.Trim();

		if ( !File.Exists( executable ) )
		{
			ShowError( MateLauncher.App.Localization.Localizer.Instance.Get( "Config_ErrorExeMissing" ) );
			return;
		}

		if ( name.Length == 0 )
		{
			ShowError( MateLauncher.App.Localization.Localizer.Instance.Get( "Config_ErrorName" ) );
			return;
		}

		if ( cover.Length > 0 && !File.Exists( cover ) )
		{
			ShowError( MateLauncher.App.Localization.Localizer.Instance.Get( "Config_ErrorCover" ) );
			return;
		}

		NewGame = new Game
		{
			Name = name,
			LaunchCommand = executable,
			LaunchArguments = ArgumentsBox.Text.Trim(),
			InstallPath = Path.GetDirectoryName( executable ) ?? string.Empty,
			RunAsAdministrator = AdministratorBox.IsChecked == true
		};
		CoverSourceFile = cover.Length > 0 ? cover : null;
		DialogResult = true;
	}

	private void ShowError( string message )
	{
		ErrorText.Text = message;
		ErrorText.Visibility = Visibility.Visible;
	}
}
