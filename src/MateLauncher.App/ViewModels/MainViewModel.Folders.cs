using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MateLauncher.App.Icons;
using MateLauncher.App.Localization;
using MateLauncher.App.Themes;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;
using MateLauncher.Infrastructure.Achievements;
using MateLauncher.Infrastructure.Backup;
using MateLauncher.Providers.Steam;
using MateLauncher.Infrastructure.Covers;
using MateLauncher.App.Icons;
using MateLauncher.Infrastructure.Downloads;
using MateLauncher.Infrastructure.News;
using MateLauncher.Infrastructure.Storage;
using MateLauncher.Infrastructure.Updates;
using MateLauncher.App.Startup;
using Microsoft.Win32;

namespace MateLauncher.App.ViewModels;

public partial class MainViewModel
{
	partial void OnFolderSortChanged( FolderSortMode value )
	{
		ReapplyFolderOrganization();
	}

	partial void OnMergeFoldersChanged( bool value )
	{
		ReapplyFolderOrganization();
	}

	private void ReapplyFolderOrganization()
	{
		if ( _IsLoadingSettings )
		{
			return;
		}

		SaveSettings();
		RebuildNavigation();
		ApplyFilter();
	}

	private FolderEntryViewModel CreateFolderEntry( string path, string tabName, string? iconPath = null, bool isEnabled = true, FolderScanMode scanMode = FolderScanMode.Subfolders )
	{
		FolderEntryViewModel entry = new FolderEntryViewModel( path, tabName, iconPath, isEnabled, scanMode );

		entry.PropertyChanged += async ( _, args ) =>
		{
			if ( _IsApplyingFolderEdit || args.PropertyName is nameof( FolderEntryViewModel.Icon ) or nameof( FolderEntryViewModel.HasIcon ) )
			{
				return;
			}

			SaveSettings();
			await SyncAsync();
		};

		return entry;
	}

	[RelayCommand]
	public async Task AddFolderAsync()
	{
		OpenFolderDialog dialog = new OpenFolderDialog { Title = T( "Library_AddFolder" ) };

		if ( dialog.ShowDialog() != true || Folders.Any( folder => string.Equals( folder.Path, dialog.FolderName, StringComparison.OrdinalIgnoreCase ) ) )
		{
			return;
		}

		string tabName = new DirectoryInfo( dialog.FolderName ).Name;
		Folders.Add( CreateFolderEntry( dialog.FolderName, tabName ) );
		HasNoFolders = false;
		SaveSettings();
		await SyncAsync();
	}

	[RelayCommand]
	public async Task RemoveFolderAsync( FolderEntryViewModel? entry )
	{
		if ( entry is null )
		{
			return;
		}

		Folders.Remove( entry );
		HasNoFolders = Folders.Count == 0;
		SaveSettings();
		await SyncAsync();
	}

	internal static string? PickIconFile()
	{
		OpenFileDialog dialog = new OpenFileDialog { Title = T( "Config_PickImage" ), Filter = "Images (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico" };

		if ( dialog.ShowDialog() != true )
		{
			return null;
		}

		string iconsFolder = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), "MateLauncher", "icons" );
		Directory.CreateDirectory( iconsFolder );
		string target = Path.Combine( iconsFolder, $"{Guid.NewGuid():N}{Path.GetExtension( dialog.FileName )}" );
		File.Copy( dialog.FileName, target, true );
		return target;
	}

	[RelayCommand]
	public void ChooseFolderIcon( FolderEntryViewModel? entry )
	{
		string? iconPath = entry is null ? null : PickIconFile();

		if ( entry is not null && iconPath is not null )
		{
			entry.IconPath = iconPath;
		}
	}

	[RelayCommand]
	public void ResetFolderIcon( FolderEntryViewModel? entry )
	{
		if ( entry is not null )
		{
			entry.IconPath = null;
		}
	}

	private void ChangeGroupIcon( string groupName )
	{
		string? iconPath = PickIconFile();

		if ( iconPath is null )
		{
			return;
		}

		foreach ( FolderEntryViewModel entry in Folders.Where( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) ).ToList() )
		{
			entry.IconPath = iconPath;
		}
	}

	private void RenameGroup( string groupName )
	{
		string? newName = InputDialog.Ask( T( "Dialog_Rename" ), T( "Dialog_NewName" ), groupName );

		if ( newName is null || newName == groupName )
		{
			return;
		}

		foreach ( FolderEntryViewModel entry in Folders.Where( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) ).ToList() )
		{
			entry.TabName = newName;
		}
	}

	private async Task DeleteGroupAsync( string groupName )
	{
		if ( !ConfirmDialog.Ask( T( "Msg_RemoveTitle" ), string.Format( T( "Msg_DeleteFolder" ), groupName ), T( "Btn_Remove" ) ) )
		{
			return;
		}

		foreach ( FolderEntryViewModel entry in Folders.Where( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) ).ToList() )
		{
			Folders.Remove( entry );
		}

		HasNoFolders = Folders.Count == 0;
		SaveSettings();
		await SyncAsync();
	}

	[RelayCommand]
	public void AddCategory()
	{
		string? name = InputDialog.Ask( T( "Dialog_NewCategory" ), T( "Dialog_CategoryName" ) );

		if ( name is null || _Categories.Any( category => string.Equals( category, name, StringComparison.OrdinalIgnoreCase ) ) )
		{
			return;
		}

		_Categories.Add( name );
		SaveSettings();
		RebuildNavigation();
		SelectedCategoryItem = CategoryItems.FirstOrDefault( item => item.Title == name );
	}

	private List<string> GetOrderedGroupNames()
	{
		List<string> names = _AllCards.Where( card => card.Game.Platform == GamePlatform.Folder && !string.IsNullOrWhiteSpace( card.Game.GroupName ) ).Select( card => card.Game.GroupName! ).Distinct().ToList();

		switch ( FolderSort )
		{
			case FolderSortMode.Manual:
				return names.OrderBy( FindFolderIndex ).ThenBy( name => name, StringComparer.CurrentCultureIgnoreCase ).ToList();
			case FolderSortMode.GameCount:
				return names.OrderByDescending( name => _AllCards.Count( card => card.Game.Platform == GamePlatform.Folder && card.Game.GroupName == name ) ).ThenBy( name => name, StringComparer.CurrentCultureIgnoreCase ).ToList();
			default:
				return names.OrderBy( name => name, StringComparer.CurrentCultureIgnoreCase ).ToList();
		}
	}

	private int FindFolderIndex( string groupName )
	{
		int index = Folders.ToList().FindIndex( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) );
		return index < 0 ? int.MaxValue : index;
	}

	[RelayCommand]
	public void SetFolderSort( string? mode )
	{
		if ( Enum.TryParse( mode, out FolderSortMode parsedMode ) )
		{
			FolderSort = parsedMode;
		}
	}

	[RelayCommand]
	public void MoveFolderUp( FolderEntryViewModel? entry )
	{
		MoveFolder( entry, -1 );
	}

	[RelayCommand]
	public void MoveFolderDown( FolderEntryViewModel? entry )
	{
		MoveFolder( entry, 1 );
	}

	private void MoveFolder( FolderEntryViewModel? entry, int direction )
	{
		int index = entry is null ? -1 : Folders.IndexOf( entry );
		int target = index + direction;

		if ( index < 0 || target < 0 || target >= Folders.Count )
		{
			return;
		}

		Folders.Move( index, target );
		SaveSettings();

		if ( FolderSort == FolderSortMode.Manual )
		{
			RebuildNavigation();
			ApplyFilter();
		}
		else
		{
			FolderSort = FolderSortMode.Manual;
		}
	}

	private async Task EditGroupAsync( string groupName )
	{
		List<FolderEntryViewModel> entries = Folders.Where( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) ).ToList();

		if ( entries.Count > 0 )
		{
			await EditFolderEntriesAsync( groupName, entries );
		}
	}

	[RelayCommand]
	public async Task EditFolderAsync( FolderEntryViewModel? entry )
	{
		if ( entry is not null )
		{
			await EditFolderEntriesAsync( entry.TabName, new List<FolderEntryViewModel> { entry } );
		}
	}

	private async Task EditFolderEntriesAsync( string tabName, List<FolderEntryViewModel> entries )
	{
		FolderEditWindow window = new FolderEditWindow( tabName, entries.FirstOrDefault( entry => entry.HasIcon )?.IconPath, entries ) { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() != true )
		{
			return;
		}

		_IsApplyingFolderEdit = true;

		foreach ( FolderDraftViewModel draft in window.Drafts )
		{
			if ( draft.IsRemoved )
			{
				Folders.Remove( draft.Source );
				continue;
			}

			draft.Source.TabName = window.TabName;
			draft.Source.IconPath = window.IconPath;
			draft.Source.IsEnabled = draft.IsEnabled;
			draft.Source.ScanMode = ( FolderScanMode ) draft.ScanModeIndex;
		}

		_IsApplyingFolderEdit = false;
		HasNoFolders = Folders.Count == 0;
		SaveSettings();
		await SyncAsync();
	}

	private void OpenGroupFolder( string groupName )
	{
		FolderEntryViewModel? entry = Folders.FirstOrDefault( folder => string.Equals( folder.TabName, groupName, StringComparison.OrdinalIgnoreCase ) && Directory.Exists( folder.Path ) );

		if ( entry is not null )
		{
			Process.Start( new ProcessStartInfo( "explorer.exe", $"\"{entry.Path}\"" ) { UseShellExecute = true } );
		}
	}

	private void ChangeCategoryColor( string categoryName )
	{
		_CategoryColors.TryGetValue( categoryName, out string? currentColor );
		ColorPickerWindow window = new ColorPickerWindow( categoryName, currentColor ) { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() != true )
		{
			return;
		}

		if ( window.IsReset )
		{
			_CategoryColors.Remove( categoryName );
		}
		else if ( window.SelectedColor is not null )
		{
			_CategoryColors[ categoryName ] = window.SelectedColor;
		}

		SaveSettings();
		RebuildNavigation();
		ApplyFilter();
	}

	private async Task RenameCategoryAsync( string categoryName )
	{
		string? newName = InputDialog.Ask( T( "Dialog_Rename" ), T( "Dialog_NewName" ), categoryName );

		if ( newName is null || newName == categoryName || _Categories.Contains( newName ) )
		{
			return;
		}

		_Categories[ _Categories.IndexOf( categoryName ) ] = newName;

		if ( _CategoryColors.Remove( categoryName, out string? movedColor ) )
		{
			_CategoryColors[ newName ] = movedColor;
		}

		SaveSettings();
		await _Repository.RenameCategoryAsync( categoryName, newName );
		await RefreshAsync();
	}

	private async Task DeleteCategoryAsync( string categoryName )
	{
		if ( !ConfirmDialog.Ask( T( "Msg_RemoveTitle" ), string.Format( T( "Msg_DeleteCategory" ), categoryName ), T( "Btn_Delete" ) ) )
		{
			return;
		}

		_Categories.Remove( categoryName );
		_CategoryColors.Remove( categoryName );
		SaveSettings();
		await _Repository.RenameCategoryAsync( categoryName, null );
		await RefreshAsync();
	}
}
