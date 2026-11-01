using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using MateLauncher.App.Icons;
using MateLauncher.App.Localization;
using MateLauncher.App.ViewModels;

namespace MateLauncher.App;

public partial class FolderEditWindow : Window
{
	private string? _IconPath;

	public ObservableCollection<FolderDraftViewModel> Drafts { get; }
	public string TabName => TabNameBox.Text.Trim();
	public string? IconPath => _IconPath;

	public FolderEditWindow( string tabName, string? iconPath, IEnumerable<FolderEntryViewModel> entries )
	{
		InitializeComponent();
		TabNameBox.Text = tabName;
		_IconPath = iconPath;
		Drafts = new ObservableCollection<FolderDraftViewModel>( entries.Select( entry => new FolderDraftViewModel( entry ) ) );
		DraftList.ItemsSource = Drafts;
		SourcesLabel.Text = Localizer.Instance.Get( "Folder_Sources" );
		RefreshIcon();
		Loaded += ( _, _ ) =>
		{
			TabNameBox.Focus();
			TabNameBox.SelectAll();
		};
	}

	private void RefreshIcon()
	{
		ImageSource? image = PlatformIcons.LoadImage( _IconPath );
		IconPreview.Source = image;
		IconPreview.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
		IconGlyph.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
		ResetIconButton.Visibility = image is null ? Visibility.Collapsed : Visibility.Visible;
	}

	private void OnChooseIconClicked( object sender, RoutedEventArgs e )
	{
		string? picked = MainViewModel.PickIconFile();

		if ( picked is null )
		{
			return;
		}

		_IconPath = picked;
		RefreshIcon();
	}

	private void OnResetIconClicked( object sender, RoutedEventArgs e )
	{
		_IconPath = null;
		RefreshIcon();
	}

	private void OnOpenDraftClicked( object sender, RoutedEventArgs e )
	{
		if ( ( sender as FrameworkElement )?.DataContext is FolderDraftViewModel draft && Directory.Exists( draft.Path ) )
		{
			Process.Start( new ProcessStartInfo( "explorer.exe", $"\"{draft.Path}\"" ) { UseShellExecute = true } );
		}
	}

	private void OnRemoveDraftClicked( object sender, RoutedEventArgs e )
	{
		if ( ( sender as FrameworkElement )?.DataContext is FolderDraftViewModel draft )
		{
			draft.IsRemoved = !draft.IsRemoved;
		}
	}

	private void OnSaveClicked( object sender, RoutedEventArgs e )
	{
		if ( TabName.Length == 0 )
		{
			TabNameBox.Focus();
			return;
		}

		DialogResult = true;
	}
}
