using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace MateLauncher.Installer;

public partial class MainWindow : Window
{
	private const int DwmUseImmersiveDarkMode = 20;

	private readonly bool _IsUninstall;
	private readonly bool _IsUpdate;
	private bool _IsFinished;
	private string _InstalledDirectory = string.Empty;
	private bool _IsReplacing;

	public MainWindow( bool isUninstall, bool isUpdate = false )
	{
		InitializeComponent();
		_IsUninstall = isUninstall;
		_IsUpdate = isUpdate && !isUninstall;
		SourceInitialized += OnSourceInitialized;
		Title = Texts.Get( "Installation de MateLauncher", "MateLauncher Setup" );
		CancelButton.Content = Texts.Get( "Annuler", "Cancel" );

		if ( isUninstall )
		{
			PrepareUninstall();
		}
		else
		{
			PrepareInstall();

			if ( _IsUpdate )
			{
				Loaded += OnUpdateLoaded;
			}
		}
	}

	[DllImport( "dwmapi.dll" )]
	private static extern int DwmSetWindowAttribute( IntPtr windowHandle, int attribute, ref int value, int size );

	private void OnSourceInitialized( object? sender, EventArgs e )
	{
		int enabled = 1;
		DwmSetWindowAttribute( new WindowInteropHelper( this ).Handle, DwmUseImmersiveDarkMode, ref enabled, sizeof( int ) );
	}

	private void PrepareInstall()
	{
		HeaderText.Text = Texts.Get( "Installer MateLauncher", "Install MateLauncher" );
		SubHeaderText.Text = Texts.Get( "Votre bibliothèque de jeux, toutes plateformes réunies.", "Your game library, every platform in one place." );
		DirectoryLabel.Text = Texts.Get( "DOSSIER D'INSTALLATION", "INSTALLATION FOLDER" );
		BrowseButton.Content = Texts.Get( "Parcourir…", "Browse…" );
		RuntimeNote.Text = InstallerService.IsDesktopRuntimeInstalled() ? string.Empty : Texts.Get( "Le .NET 9 Desktop Runtime (environ 55 Mo) va être téléchargé et installé : Windows demandera une autorisation.", "The .NET 9 Desktop Runtime (about 55 MB) will be downloaded and installed: Windows will ask for permission." );
		RuntimeNote.Visibility = RuntimeNote.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
		DesktopBox.Content = Texts.Get( "Créer un raccourci sur le bureau", "Create a desktop shortcut" );
		StartMenuBox.Content = Texts.Get( "Ajouter au menu Démarrer", "Add to the Start menu" );
		string? installedDirectory = InstallerService.FindInstalledDirectory();
		_IsReplacing = installedDirectory is not null && File.Exists( Path.Combine( installedDirectory, InstallerService.ApplicationExecutable ) );
		DirectoryBox.Text = installedDirectory ?? InstallerService.DefaultDirectory;
		PrimaryButton.Content = Texts.Get( _IsReplacing ? "Remplacer" : "Installer", _IsReplacing ? "Replace" : "Install" );
		if ( _IsReplacing )
		{
			SubHeaderText.Text = Texts.Get( "Déjà installé : l'application sera remplacée, vos données sont conservées.", "Already installed: the application will be replaced, your data is kept." );
		}

		if ( !InstallerService.HasPayload )
		{
			WarningText.Text = Texts.Get( "Cet installateur ne contient pas l'application (build incomplet).", "This installer does not contain the application (incomplete build)." );
			WarningText.Visibility = Visibility.Visible;
			PrimaryButton.IsEnabled = false;
		}
	}

	private async void OnUpdateLoaded( object sender, RoutedEventArgs e )
	{
		string directory = DirectoryBox.Text.Trim();
		OptionsPage.Visibility = Visibility.Collapsed;
		ProgressPage.Visibility = Visibility.Visible;
		PrimaryButton.IsEnabled = false;
		CancelButton.IsEnabled = false;
		ProgressText.Text = Texts.Get( "Fermeture de MateLauncher…", "Waiting for MateLauncher to close…" );

		for ( int attempt = 0; attempt < 40 && InstallerService.IsApplicationRunning( directory ); attempt++ )
		{
			await Task.Delay( 500 );
		}

		await RunInstallAsync();
	}

	private void PrepareUninstall()
	{
		_InstalledDirectory = AppContext.BaseDirectory.TrimEnd( '\\', '/' );
		HeaderText.Text = Texts.Get( "Désinstaller MateLauncher", "Uninstall MateLauncher" );
		SubHeaderText.Text = _InstalledDirectory;
		OptionsPage.Visibility = Visibility.Collapsed;
		UninstallPage.Visibility = Visibility.Visible;
		UninstallText.Text = Texts.Get( "MateLauncher va être retiré de cet ordinateur.", "MateLauncher will be removed from this computer." );
		DeleteDataBox.Content = Texts.Get( "Supprimer aussi mes données (bibliothèque, paramètres, jaquettes)", "Also delete my data (library, settings, covers)" );
		PrimaryButton.Content = Texts.Get( "Désinstaller", "Uninstall" );
	}

	private void OnBrowseClicked( object sender, RoutedEventArgs e )
	{
		OpenFolderDialog dialog = new OpenFolderDialog { Title = Texts.Get( "Choisir le dossier d'installation", "Choose the installation folder" ) };

		if ( dialog.ShowDialog( this ) == true )
		{
			DirectoryBox.Text = Path.Combine( dialog.FolderName, InstallerService.ProductName );
		}
	}

	private void OnCancelClicked( object sender, RoutedEventArgs e )
	{
		Close();
	}

	private async void OnPrimaryClicked( object sender, RoutedEventArgs e )
	{
		if ( _IsFinished )
		{
			if ( !_IsUninstall && LaunchBox.IsChecked == true )
			{
				InstallerService.LaunchApplication( _InstalledDirectory, SafeModeBox.IsChecked == true );
			}

			Close();
			return;
		}

		if ( _IsUninstall )
		{
			RunUninstall();
			return;
		}

		await RunInstallAsync();
	}

	private async Task RunInstallAsync()
	{
		string directory = DirectoryBox.Text.Trim();

		if ( directory.Length == 0 || !Path.IsPathRooted( directory ) )
		{
			ShowWarning( Texts.Get( "Choisissez un dossier d'installation valide.", "Choose a valid installation folder." ) );
			return;
		}

		if ( InstallerService.IsApplicationRunning( directory ) )
		{
			ShowWarning( Texts.Get( "MateLauncher est en cours d'exécution : fermez-le puis réessayez.", "MateLauncher is running: close it and try again." ) );
			return;
		}

		if ( _IsReplacing && !_IsUpdate && MessageBox.Show( this, Texts.Get( "Remplacer l'installation existante de MateLauncher ? Vos données sont conservées.", "Replace the existing MateLauncher installation? Your data is kept." ), Title, MessageBoxButton.YesNo, MessageBoxImage.Question ) != MessageBoxResult.Yes )
		{
			return;
		}

		OptionsPage.Visibility = Visibility.Collapsed;
		ProgressPage.Visibility = Visibility.Visible;
		PrimaryButton.IsEnabled = false;
		CancelButton.IsEnabled = false;
		ProgressText.Text = Texts.Get( "Installation en cours…", "Installing…" );

		InstallOptions options = new InstallOptions( directory, DesktopBox.IsChecked == true, StartMenuBox.IsChecked == true );
		Progress<double> progress = new Progress<double>( value => InstallBar.Value = value );

		try
		{
			if ( !InstallerService.IsDesktopRuntimeInstalled() )
			{
				ProgressText.Text = Texts.Get( "Téléchargement et installation du .NET 9 Desktop Runtime…", "Downloading and installing the .NET 9 Desktop Runtime…" );
				await InstallerService.InstallDesktopRuntimeAsync( progress );
				ProgressText.Text = Texts.Get( "Installation en cours…", "Installing…" );
			}
			await Task.Run( () => InstallerService.Install( options, progress ) );
		}
		catch ( Exception exception )
		{
			ProgressPage.Visibility = Visibility.Collapsed;
			OptionsPage.Visibility = Visibility.Visible;
			PrimaryButton.IsEnabled = true;
			CancelButton.IsEnabled = true;
			ShowWarning( exception.Message );
			return;
		}

		_InstalledDirectory = directory;

		if ( _IsUpdate )
		{
			InstallerService.LaunchApplication( directory, false );
			Close();
			return;
		}

		ShowFinished( Texts.Get( "MateLauncher est installé.", "MateLauncher is installed." ) );
		LaunchBox.Content = Texts.Get( "Lancer MateLauncher", "Launch MateLauncher" );
		LaunchBox.Visibility = Visibility.Visible;
		SafeModeBox.Content = Texts.Get( "Lancer en mode sans échec", "Launch in safe mode" );
		SafeModeBox.Visibility = Visibility.Visible;
	}

	private void RunUninstall()
	{
		if ( !File.Exists( Path.Combine( _InstalledDirectory, InstallerService.ApplicationExecutable ) ) )
		{
			ShowWarning( Texts.Get( "Dossier d'installation introuvable.", "Installation folder not found." ) );
			return;
		}

		if ( InstallerService.IsApplicationRunning( _InstalledDirectory ) )
		{
			ShowWarning( Texts.Get( "MateLauncher est en cours d'exécution : fermez-le puis réessayez.", "MateLauncher is running: close it and try again." ) );
			return;
		}

		bool deleteUserData = DeleteDataBox.IsChecked == true;

		if ( deleteUserData )
		{
			string warning = Texts.Get( "Votre bibliothèque, vos paramètres et vos jaquettes seront définitivement supprimés. Continuer ?", "Your library, settings and covers will be permanently deleted. Continue?" );

			if ( MessageBox.Show( this, warning, Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No ) != MessageBoxResult.Yes )
			{
				return;
			}
		}

		try
		{
			InstallerService.Uninstall( _InstalledDirectory, deleteUserData );
		}
		catch ( Exception exception )
		{
			ShowWarning( exception.Message );
			return;
		}

		UninstallPage.Visibility = Visibility.Collapsed;
		ShowFinished( Texts.Get( "MateLauncher a été désinstallé.", "MateLauncher has been uninstalled." ) );
		LaunchBox.Visibility = Visibility.Collapsed;
	}

	private void ShowFinished( string message )
	{
		_IsFinished = true;
		ProgressPage.Visibility = Visibility.Collapsed;
		DonePage.Visibility = Visibility.Visible;
		DoneText.Text = message;
		PrimaryButton.IsEnabled = true;
		PrimaryButton.Content = Texts.Get( "Terminer", "Finish" );
		CancelButton.Visibility = Visibility.Collapsed;
	}

	private void ShowWarning( string message )
	{
		WarningText.Text = message;
		WarningText.Visibility = Visibility.Visible;

		if ( _IsUninstall )
		{
			UninstallText.Text = message;
		}
	}
}
