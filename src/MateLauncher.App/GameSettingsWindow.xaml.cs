using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MateLauncher.App.Localization;
using MateLauncher.App.ViewModels;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using Microsoft.Win32;

namespace MateLauncher.App;

public partial class GameSettingsWindow : Window
{
	private readonly Game _Game;
	private readonly ICoverFinder _CoverFinder;
	private string? _SelectedCategory;

	public Game? EditedGame { get; private set; }
	public bool ResetRequested { get; private set; }

	public GameSettingsWindow( Game game, ICoverFinder coverFinder, IReadOnlyList<string> categories )
	{
		InitializeComponent();
		_Game = game;
		_CoverFinder = coverFinder;
		_SelectedCategory = game.CategoryName;

		TitleText.Text = game.Name;
		PlatformText.Text = GameCardViewModel.GetPlatformLabel( game.Platform );
		NameBox.Text = game.Name;
		ExecutableBox.Text = game.LaunchCommand;
		ArgumentsBox.Text = game.LaunchArguments;
		InstallPathBox.Text = game.InstallPath;
		CoverBox.Text = game.CoverImagePath ?? string.Empty;
		AdministratorBox.IsChecked = game.RunAsAdministrator;
		ResetButton.Visibility = game.Platform == GamePlatform.Manual ? Visibility.Collapsed : Visibility.Visible;

		AddCategoryChip( Localizer.Instance.Get( "Config_NoCategory" ), null );

		foreach ( string category in categories )
		{
			AddCategoryChip( category, category );
		}
	}

	private void AddCategoryChip( string label, string? category )
	{
		RadioButton chip = new RadioButton
		{
			Content = label,
			Style = ( Style ) FindResource( "ChipStyle" ),
			GroupName = "Category",
			IsChecked = category == _SelectedCategory
		};

		chip.Checked += ( _, _ ) => _SelectedCategory = category;
		CategoryPanel.Children.Add( chip );
	}

	private void OnBrowseExecutableClicked( object sender, RoutedEventArgs e )
	{
		OpenFileDialog dialog = new OpenFileDialog
		{
			Title = Localizer.Instance.Get( "Config_PickExe" ),
			Filter = "Applications (*.exe;*.bat;*.lnk)|*.exe;*.bat;*.lnk|All files|*.*",
			InitialDirectory = Directory.Exists( InstallPathBox.Text ) ? InstallPathBox.Text : string.Empty
		};

		if ( dialog.ShowDialog( this ) == true )
		{
			ExecutableBox.Text = dialog.FileName;
		}
	}

	private void OnBrowseInstallPathClicked( object sender, RoutedEventArgs e )
	{
		OpenFolderDialog dialog = new OpenFolderDialog { Title = Localizer.Instance.Get( "Config_PickFolder" ) };

		if ( dialog.ShowDialog( this ) == true )
		{
			InstallPathBox.Text = dialog.FolderName;
		}
	}

	private void OnBrowseCoverClicked( object sender, RoutedEventArgs e )
	{
		OpenFileDialog dialog = new OpenFileDialog
		{
			Title = Localizer.Instance.Get( "Config_PickImage" ),
			Filter = "Images (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
		};

		if ( dialog.ShowDialog( this ) == true )
		{
			CoverBox.Text = dialog.FileName;
		}
	}

	private async void OnSearchCoverClicked( object sender, RoutedEventArgs e )
	{
		SearchCoverButton.IsEnabled = false;
		ErrorText.Visibility = Visibility.Collapsed;

		string? coverUrl = await _CoverFinder.FindCoverUrlAsync( NameBox.Text.Trim() );

		if ( coverUrl is null )
		{
			ShowError( Localizer.Instance.Get( "Config_NoCoverFound" ) );
		}
		else
		{
			CoverBox.Text = coverUrl;
		}

		SearchCoverButton.IsEnabled = true;
	}

	private void OnGoogleClicked( object sender, RoutedEventArgs e )
	{
		string query = Uri.EscapeDataString( $"{NameBox.Text.Trim()} game cover" );
		Process.Start( new ProcessStartInfo( $"https://www.google.com/search?tbm=isch&q={query}" ) { UseShellExecute = true } );
	}

	private void OnPasteClicked( object sender, RoutedEventArgs e )
	{
		ErrorText.Visibility = Visibility.Collapsed;

		if ( Clipboard.ContainsImage() && Clipboard.GetImage() is BitmapSource image )
		{
			string filePath = Path.Combine( Path.GetTempPath(), $"matelauncher-cover-{Guid.NewGuid():N}.png" );
			PngBitmapEncoder encoder = new PngBitmapEncoder();
			encoder.Frames.Add( BitmapFrame.Create( image ) );

			using ( FileStream stream = File.Create( filePath ) )
			{
				encoder.Save( stream );
			}

			CoverBox.Text = filePath;
			return;
		}

		string text = Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty;

		if ( Game.IsRemoteCover( text ) )
		{
			CoverBox.Text = text;
			return;
		}

		ShowError( Localizer.Instance.Get( "Config_PasteFailed" ) );
	}

	private void OnResetClicked( object sender, RoutedEventArgs e )
	{
		ResetRequested = true;
		EditedGame = CreateEditedGame( _Game.Name, _Game.LaunchCommand, _Game.LaunchArguments, _Game.InstallPath, _Game.CoverImagePath, _Game.RunAsAdministrator );
		DialogResult = true;
	}

	private void OnSaveClicked( object sender, RoutedEventArgs e )
	{
		string name = NameBox.Text.Trim();
		string executable = ExecutableBox.Text.Trim();
		string cover = CoverBox.Text.Trim();

		if ( name.Length == 0 )
		{
			ShowError( Localizer.Instance.Get( "Config_ErrorName" ) );
			return;
		}

		if ( executable.Length == 0 )
		{
			ShowError( Localizer.Instance.Get( "Config_ErrorExe" ) );
			return;
		}

		if ( !executable.Contains( "://" ) && !File.Exists( executable ) )
		{
			ShowError( Localizer.Instance.Get( "Config_ErrorExeMissing" ) );
			return;
		}

		if ( cover.Length > 0 && !Game.IsRemoteCover( cover ) && !File.Exists( cover ) )
		{
			ShowError( Localizer.Instance.Get( "Config_ErrorCover" ) );
			return;
		}

		EditedGame = CreateEditedGame( name, executable, ArgumentsBox.Text.Trim(), InstallPathBox.Text.Trim(), cover.Length > 0 ? cover : null, AdministratorBox.IsChecked == true );
		DialogResult = true;
	}

	private Game CreateEditedGame( string name, string executable, string arguments, string installPath, string? cover, bool runAsAdministrator )
	{
		return new Game
		{
			Id = _Game.Id,
			Platform = _Game.Platform,
			PlatformGameId = _Game.PlatformGameId,
			Name = name,
			LaunchCommand = executable,
			LaunchArguments = arguments,
			InstallPath = installPath,
			CoverImagePath = cover,
			RunAsAdministrator = runAsAdministrator,
			IsHidden = _Game.IsHidden,
			CategoryName = _SelectedCategory,
			IsCustomized = true
		};
	}

	private void ShowError( string message )
	{
		ErrorText.Text = message;
		ErrorText.Visibility = Visibility.Visible;
	}
}
