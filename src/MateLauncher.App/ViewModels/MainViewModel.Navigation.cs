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
	partial void OnSearchTextChanged( string value )
	{
		ApplyFilter();
	}

	partial void OnSelectedLibraryItemChanged( NavItemViewModel? value )
	{
		OnNavigationSelected( value, nameof( SelectedLibraryItem ) );
	}

	partial void OnSelectedToolItemChanged( NavItemViewModel? value )
	{
		OnNavigationSelected( value, nameof( SelectedToolItem ) );
	}

	partial void OnSelectedPlatformItemChanged( NavItemViewModel? value )
	{
		OnNavigationSelected( value, nameof( SelectedPlatformItem ) );
	}

	partial void OnSelectedCategoryItemChanged( NavItemViewModel? value )
	{
		OnNavigationSelected( value, nameof( SelectedCategoryItem ) );
	}

	partial void OnSelectedGroupItemChanged( NavItemViewModel? value )
	{
		OnNavigationSelected( value, nameof( SelectedGroupItem ) );
	}

	public bool IsLibraryVisible => !IsSettingsOpen && !IsStatsOpen && !IsDownloadsOpen && !IsNewsOpen && !IsFriendsOpen && !IsHomeOpen && SelectedGame is null;

	private void OnPageStateChanged( object? sender, System.ComponentModel.PropertyChangedEventArgs e )
	{
		if ( e.PropertyName is nameof( IsSettingsOpen ) or nameof( IsStatsOpen ) or nameof( IsDownloadsOpen ) or nameof( IsNewsOpen ) or nameof( IsFriendsOpen ) or nameof( IsHomeOpen ) or nameof( SelectedGame ) )
		{
			OnPropertyChanged( nameof( IsLibraryVisible ) );
			OnPropertyChanged( nameof( IsHomeVisible ) );
		}
	}

	private void OnNavigationSelected( NavItemViewModel? value, string source )
	{
		if ( _IsRebuildingNavigation )
		{
			return;
		}

		if ( value is not null )
		{
			if ( source != nameof( SelectedLibraryItem ) )
			{
				SelectedLibraryItem = null;
			}

			if ( source != nameof( SelectedToolItem ) )
			{
				SelectedToolItem = null;
			}

			if ( source != nameof( SelectedPlatformItem ) )
			{
				SelectedPlatformItem = null;
			}

			if ( source != nameof( SelectedCategoryItem ) )
			{
				SelectedCategoryItem = null;
			}

			if ( source != nameof( SelectedGroupItem ) )
			{
				SelectedGroupItem = null;
			}

			SelectedGame = null;
			IsSettingsOpen = false;
			IsStatsOpen = value.Kind == NavKind.Stats;
			IsDownloadsOpen = value.Kind == NavKind.Downloads;
			IsNewsOpen = value.Kind == NavKind.News;
			IsFriendsOpen = value.Kind == NavKind.Friends;
			IsHomeOpen = value.Kind == NavKind.Home;

			if ( IsHomeOpen )
			{
				RefreshHome();
			}


			if ( IsStatsOpen )
			{
				_ = LoadStatsAsync();
			}

			if ( IsDownloadsOpen )
			{
				_ = RefreshUpdatesAsync();
			}

			if ( IsFriendsOpen )
			{
				_ = LoadFriendsAsync();
			}

			if ( IsNewsOpen && NewsItems.Count == 0 )
			{
				_ = RefreshNewsAsync();
			}
		}

		ApplyFilter();
	}

	public void CycleNavigation( int direction )
	{
		List<NavItemViewModel> items = LibraryItems.Concat( ToolItems ).Concat( PlatformItems ).Concat( CategoryItems ).Concat( GroupItems ).ToList();
		NavItemViewModel? current = SelectedLibraryItem ?? SelectedToolItem ?? SelectedPlatformItem ?? SelectedCategoryItem ?? SelectedGroupItem;
		int index = current is null ? 0 : items.IndexOf( current );
		NavItemViewModel next = items[ ( index + direction + items.Count ) % items.Count ];

		switch ( next.Kind )
		{
			case NavKind.Platform:
				SelectedPlatformItem = next;
				break;
			case NavKind.Category:
				SelectedCategoryItem = next;
				break;
			case NavKind.Group:
				SelectedGroupItem = next;
				break;
			case NavKind.Stats:
			case NavKind.Downloads:
			case NavKind.News:
			case NavKind.Friends:
				SelectedToolItem = next;
				break;
			default:
				SelectedLibraryItem = next;
				break;
		}
	}

	private void RebuildNavigation()
	{
		GamePlatform? previousPlatform = SelectedPlatformItem?.Platform;
		string? previousGroup = SelectedGroupItem?.Title;
		string? previousCategory = SelectedCategoryItem?.Title;
		_IsRebuildingNavigation = true;

		PlatformItems.Clear();

		foreach ( GamePlatform platform in _AllCards.Select( card => card.Game.Platform ).Where( platform => platform != GamePlatform.Folder ).Distinct().OrderBy( platform => platform ) )
		{
			PlatformItems.Add( new NavItemViewModel( GameCardViewModel.GetPlatformLabel( platform ), platform == GamePlatform.Xbox ? "" : "", NavKind.Platform, platform ) { Icon = PlatformIcons.Get( platform ) } );
		}

		CategoryItems.Clear();

		foreach ( string categoryName in _Categories.Concat( _AllCards.Where( card => !string.IsNullOrEmpty( card.CategoryName ) ).Select( card => card.CategoryName ) ).Distinct().OrderBy( name => name, StringComparer.CurrentCultureIgnoreCase ) )
		{
			string capturedName = categoryName;

			CategoryItems.Add( new NavItemViewModel( capturedName, "", NavKind.Category )
			{
				ColorBrush = _CategoryColors.TryGetValue( capturedName, out string? categoryHex ) ? AppTheme.ToBrush( categoryHex ) : null,
					ColorCommand = new RelayCommand( () => ChangeCategoryColor( capturedName ) ),
					RenameCommand = new AsyncRelayCommand( () => RenameCategoryAsync( capturedName ) ),
				DeleteCommand = new AsyncRelayCommand( () => DeleteCategoryAsync( capturedName ) )
			} );
		}

		GroupItems.Clear();

		if ( MergeFolders )
		{
			if ( GetOrderedGroupNames().Count > 0 )
			{
				GroupItems.Add( new NavItemViewModel( T( "Nav_FoldersItem" ), "", NavKind.Group ) { IsMerged = true } );
			}
		}
		else
		{
			foreach ( string groupName in GetOrderedGroupNames() )
			{
				string capturedName = groupName;

				GroupItems.Add( new NavItemViewModel( capturedName, "", NavKind.Group )
				{
					Icon = Folders.FirstOrDefault( folder => string.Equals( folder.TabName, capturedName, StringComparison.OrdinalIgnoreCase ) && folder.HasIcon )?.Icon,
					EditCommand = new AsyncRelayCommand( () => EditGroupAsync( capturedName ) ),
					OpenCommand = new RelayCommand( () => OpenGroupFolder( capturedName ) ),
					DeleteCommand = new AsyncRelayCommand( () => DeleteGroupAsync( capturedName ) )
				} );
			}
		}

		HasGroups = GroupItems.Count > 0;
		SelectedPlatformItem = PlatformItems.FirstOrDefault( item => item.Platform == previousPlatform && previousPlatform is not null );
		SelectedGroupItem = GroupItems.FirstOrDefault( item => item.Title == previousGroup && previousGroup is not null );
		SelectedCategoryItem = CategoryItems.FirstOrDefault( item => item.Title == previousCategory && previousCategory is not null );

		bool nothingSelected = SelectedPlatformItem is null && SelectedGroupItem is null && SelectedCategoryItem is null && SelectedLibraryItem is null && SelectedToolItem is null;

		if ( nothingSelected && !IsSettingsOpen )
		{
			SelectedLibraryItem = LibraryItems[ 0 ];
		}

		_IsRebuildingNavigation = false;
		BuildFilters();
	}
}
