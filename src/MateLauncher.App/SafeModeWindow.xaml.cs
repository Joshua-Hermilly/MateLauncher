using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using MateLauncher.App.Localization;
using MateLauncher.Core.Models;

namespace MateLauncher.App;

public partial class SafeFolderItem : ObservableObject
{
	public GameFolder Folder { get; }
	public string Path => Folder.Path;
	public string TabName => string.IsNullOrWhiteSpace( Folder.TabName ) ? System.IO.Path.GetFileName( Folder.Path.TrimEnd( '\\', '/' ) ) : Folder.TabName;

	[ObservableProperty]
	private bool _IsEnabled;

	[ObservableProperty]
	private bool _IsRemoved;

	public SafeFolderItem( GameFolder folder )
	{
		Folder = folder;
		_IsEnabled = folder.IsEnabled;
	}
}

public partial class SafeModeWindow : Window
{
	private readonly AppSettings _Settings;
	private readonly ObservableCollection<SafeFolderItem> _Items;

	public bool StartInSafeMode { get; private set; }

	public SafeModeWindow( AppSettings settings, bool afterInterruption )
	{
		InitializeComponent();
		_Settings = settings;
		_Items = new ObservableCollection<SafeFolderItem>( settings.GameFolders.Select( folder => new SafeFolderItem( folder ) ) );
		FolderList.ItemsSource = _Items;
		ReasonText.Text = Localizer.Instance.Get( afterInterruption ? "Safe_ReasonCrash" : "Safe_ReasonManual" );
		bool hasFolders = _Items.Count > 0;
		FolderList.Visibility = hasFolders ? Visibility.Visible : Visibility.Collapsed;
		FoldersLabel.Visibility = hasFolders ? Visibility.Visible : Visibility.Collapsed;
		NoFolderText.Visibility = hasFolders ? Visibility.Collapsed : Visibility.Visible;
	}

	private void OnRemoveClicked( object sender, RoutedEventArgs e )
	{
		if ( ( sender as FrameworkElement )?.DataContext is SafeFolderItem item )
		{
			item.IsRemoved = !item.IsRemoved;
		}
	}

	private void ApplyChanges()
	{
		foreach ( SafeFolderItem item in _Items )
		{
			item.Folder.IsEnabled = item.IsEnabled;
		}

		_Settings.GameFolders = _Items.Where( item => !item.IsRemoved ).Select( item => item.Folder ).ToList();
	}

	private void OnStartSafeClicked( object sender, RoutedEventArgs e )
	{
		ApplyChanges();
		StartInSafeMode = true;
		DialogResult = true;
	}

	private void OnStartNormalClicked( object sender, RoutedEventArgs e )
	{
		ApplyChanges();
		StartInSafeMode = false;
		DialogResult = true;
	}
}
