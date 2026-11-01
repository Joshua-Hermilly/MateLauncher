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
	partial void OnSortModeChanged( GameSortMode value )
	{
		if ( _IsLoadingSettings )
		{
			return;
		}

		SaveSettings();
		ApplyFilter();
	}

	[RelayCommand]
	public void SetSort( string? mode )
	{
		if ( Enum.TryParse( mode, out GameSortMode parsedMode ) )
		{
			SortMode = parsedMode;
		}
	}

	[RelayCommand]
	public void ClearFilters()
	{
		_IsBuildingFilters = true;

		foreach ( FilterOptionViewModel option in FilterGroups.SelectMany( group => group.Options ) )
		{
			option.IsSelected = false;
		}

		_SelectedFilterKeys.Clear();
		_IsBuildingFilters = false;
		RefreshActiveFilters();
		ApplyFilter();
	}

	[RelayCommand]
	public void RemoveFilter( FilterOptionViewModel? option )
	{
		if ( option is not null )
		{
			option.IsSelected = false;
		}
	}

	[RelayCommand]
	public void ToggleFilterPanel()
	{
		IsFilterPanelOpen = !IsFilterPanelOpen;
	}

	private void OnFilterOptionChanged( FilterOptionViewModel option )
	{
		if ( _IsBuildingFilters )
		{
			return;
		}

		if ( option.IsSelected )
		{
			_SelectedFilterKeys.Add( option.Key );
		}
		else
		{
			_SelectedFilterKeys.Remove( option.Key );
		}

		RefreshActiveFilters();
		ApplyFilter();
	}

	private void RefreshActiveFilters()
	{
		ActiveFilters.Clear();

		foreach ( FilterOptionViewModel option in FilterGroups.SelectMany( group => group.Options ).Where( option => option.IsSelected ) )
		{
			ActiveFilters.Add( option );
		}

		ActiveFilterCount = ActiveFilters.Count;
		HasActiveFilters = ActiveFilters.Count > 0;
	}

	private FilterGroupViewModel AddFilterGroup( string id, string titleKey )
	{
		FilterGroupViewModel group = new FilterGroupViewModel( id, T( titleKey ) ) { IsExpanded = !_CollapsedFilterGroups.Contains( id ) };
		group.PropertyChanged += ( _, args ) =>
		{
			if ( args.PropertyName == nameof( FilterGroupViewModel.IsExpanded ) )
			{
				if ( group.IsExpanded )
				{
					_CollapsedFilterGroups.Remove( id );
				}
				else
				{
					_CollapsedFilterGroups.Add( id );
				}
			}
		};
		FilterGroups.Add( group );
		return group;
	}

	private void AddFilterOption( FilterGroupViewModel group, string key, string label )
	{
		FilterOptionViewModel option = new FilterOptionViewModel( key, label ) { IsSelected = _SelectedFilterKeys.Contains( key ) };
		option.Changed = OnFilterOptionChanged;
		group.Options.Add( option );
	}

	private void BuildFilters()
	{
		_IsBuildingFilters = true;
		FilterGroups.Clear();
		List<GameCardViewModel> visibleCards = _AllCards.Where( card => !card.Game.IsHidden ).ToList();

		FilterGroupViewModel platformGroup = AddFilterGroup( "platform", "Filter_Platforms" );

		foreach ( GamePlatform platform in visibleCards.Select( card => card.Game.Platform ).Distinct().OrderBy( platform => platform ) )
		{
			AddFilterOption( platformGroup, $"platform:{platform}", GameCardViewModel.GetPlatformLabel( platform ) );
		}

		FilterGroupViewModel statusGroup = AddFilterGroup( "status", "Filter_Status" );
		AddFilterOption( statusGroup, "status:favorites", T( "Filter_Favorites" ) );
		AddFilterOption( statusGroup, "status:played", T( "Filter_Played" ) );
		AddFilterOption( statusGroup, "status:unplayed", T( "Filter_Unplayed" ) );
		AddFilterOption( statusGroup, "status:running", T( "Filter_Running" ) );

		List<string> categoryNames = _Categories.Concat( visibleCards.Where( card => !string.IsNullOrEmpty( card.CategoryName ) ).Select( card => card.CategoryName ) ).Distinct().OrderBy( name => name, StringComparer.CurrentCultureIgnoreCase ).ToList();

		if ( categoryNames.Count > 0 )
		{
			FilterGroupViewModel categoryGroup = AddFilterGroup( "category", "Filter_Categories" );

			foreach ( string categoryName in categoryNames )
			{
				AddFilterOption( categoryGroup, $"category:{categoryName}", categoryName );
			}
		}

		List<string> folderNames = GetOrderedGroupNames();

		if ( folderNames.Count > 0 )
		{
			FilterGroupViewModel folderGroup = AddFilterGroup( "folder", "Filter_Folders" );

			foreach ( string folderName in folderNames )
			{
				AddFilterOption( folderGroup, $"folder:{folderName}", folderName );
			}
		}

		FilterGroupViewModel playtimeGroup = AddFilterGroup( "playtime", "Filter_Playtime" );
		AddFilterOption( playtimeGroup, "playtime:lt1", T( "Playtime_Lt1" ) );
		AddFilterOption( playtimeGroup, "playtime:1to10", T( "Playtime_1to10" ) );
		AddFilterOption( playtimeGroup, "playtime:10to50", T( "Playtime_10to50" ) );
		AddFilterOption( playtimeGroup, "playtime:gt50", T( "Playtime_Gt50" ) );

		FilterGroupViewModel sizeGroup = AddFilterGroup( "size", "Filter_Size" );
		AddFilterOption( sizeGroup, "size:lt1", T( "Size_Lt1" ) );
		AddFilterOption( sizeGroup, "size:1to10", T( "Size_1to10" ) );
		AddFilterOption( sizeGroup, "size:10to50", T( "Size_10to50" ) );
		AddFilterOption( sizeGroup, "size:gt50", T( "Size_Gt50" ) );

		FilterGroupViewModel recentGroup = AddFilterGroup( "recent", "Filter_LastPlayed" );
		AddFilterOption( recentGroup, "recent:7d", T( "Recent_7d" ) );
		AddFilterOption( recentGroup, "recent:30d", T( "Recent_30d" ) );
		AddFilterOption( recentGroup, "recent:older", T( "Recent_Older" ) );
		AddFilterOption( recentGroup, "recent:never", T( "Recent_Never" ) );

		_IsBuildingFilters = false;
		RefreshActiveFilters();
		UpdateFilterCounts();
	}

	private void UpdateFilterCounts()
	{
		List<GameCardViewModel> visibleCards = _AllCards.Where( card => !card.Game.IsHidden ).ToList();

		foreach ( FilterOptionViewModel option in FilterGroups.SelectMany( group => group.Options ) )
		{
			option.Count = visibleCards.Count( card => CardMatchesKey( card, option.Key ) );
		}
	}

	private bool MatchesFilters( GameCardViewModel card )
	{
		if ( _SelectedFilterKeys.Count == 0 )
		{
			return true;
		}

		return _SelectedFilterKeys
			.GroupBy( key => key[ ..key.IndexOf( ':' ) ] )
			.All( group => group.Any( key => CardMatchesKey( card, key ) ) );
	}

	private static bool CardMatchesKey( GameCardViewModel card, string key )
	{
		int separator = key.IndexOf( ':' );
		string kind = key[ ..separator ];
		string value = key[ ( separator + 1 ).. ];
		double hours = card.PlaytimeSeconds / 3600.0;
		double gigabytes = card.SizeBytes / 1073741824.0;
		DateTime now = DateTime.Now;

		return kind switch
		{
			"platform" => card.Game.Platform.ToString() == value,
			"category" => card.CategoryName == value,
			"folder" => card.Game.Platform == GamePlatform.Folder && card.Game.GroupName == value,
			"status" => value switch
			{
				"favorites" => card.IsFavorite,
				"played" => card.PlaytimeSeconds > 0,
				"unplayed" => card.PlaytimeSeconds == 0,
				"running" => card.IsRunning,
				_ => false
			},
			"playtime" => value switch
			{
				"lt1" => hours < 1,
				"1to10" => hours >= 1 && hours < 10,
				"10to50" => hours >= 10 && hours < 50,
				"gt50" => hours >= 50,
				_ => false
			},
			"size" => card.SizeBytes > 0 && value switch
			{
				"lt1" => gigabytes < 1,
				"1to10" => gigabytes >= 1 && gigabytes < 10,
				"10to50" => gigabytes >= 10 && gigabytes < 50,
				"gt50" => gigabytes >= 50,
				_ => false
			},
			"recent" => value switch
			{
				"7d" => card.LastPlayedAt is not null && card.LastPlayedAt >= now.AddDays( -7 ),
				"30d" => card.LastPlayedAt is not null && card.LastPlayedAt >= now.AddDays( -30 ),
				"older" => card.LastPlayedAt is not null && card.LastPlayedAt < now.AddDays( -30 ),
				"never" => card.LastPlayedAt is null,
				_ => false
			},
			_ => false
		};
	}

	private void ApplyFilter()
	{
		List<GameCardViewModel> visibleCards = _AllCards.Where( card => !card.Game.IsHidden ).ToList();

		foreach ( NavItemViewModel libraryItem in LibraryItems )
		{
			libraryItem.Count = libraryItem.Kind switch
			{
				NavKind.All => visibleCards.Count,
				NavKind.Favorites => visibleCards.Count( card => card.IsFavorite ),
				NavKind.Recent => visibleCards.Count( card => card.LastPlayedAt is not null ),
				_ => 0
			};
		}

		foreach ( NavItemViewModel platformItem in PlatformItems )
		{
			platformItem.Count = visibleCards.Count( card => card.Game.Platform == platformItem.Platform );
		}

		foreach ( NavItemViewModel categoryItem in CategoryItems )
		{
			categoryItem.Count = visibleCards.Count( card => card.CategoryName == categoryItem.Title );
		}

		foreach ( NavItemViewModel groupItem in GroupItems )
		{
			groupItem.Count = visibleCards.Count( card => card.Game.Platform == GamePlatform.Folder && ( groupItem.IsMerged || card.Game.GroupName == groupItem.Title ) );
		}

		IEnumerable<GameCardViewModel> filtered = visibleCards;
		bool sortByRecent = false;

		if ( SelectedPlatformItem is not null )
		{
			filtered = filtered.Where( card => card.Game.Platform == SelectedPlatformItem.Platform );
		}
		else if ( SelectedCategoryItem is not null )
		{
			filtered = filtered.Where( card => card.CategoryName == SelectedCategoryItem.Title );
		}
		else if ( SelectedGroupItem is not null )
		{
			filtered = filtered.Where( card => card.Game.Platform == GamePlatform.Folder && ( SelectedGroupItem.IsMerged || card.Game.GroupName == SelectedGroupItem.Title ) );
		}
		else if ( SelectedLibraryItem?.Kind == NavKind.Favorites )
		{
			filtered = filtered.Where( card => card.IsFavorite );
		}
		else if ( SelectedLibraryItem?.Kind == NavKind.Recent )
		{
			filtered = filtered.Where( card => card.LastPlayedAt is not null );
			sortByRecent = true;
		}

		if ( SelectedLibraryItem?.Kind == NavKind.Unplayed )
		{
			filtered = filtered.Where( card => card.PlaytimeSeconds == 0 );
		}
		else if ( SelectedLibraryItem?.Kind == NavKind.Large )
		{
			filtered = filtered.Where( card => card.SizeBytes >= LargeGameBytes );
		}

		filtered = filtered.Where( MatchesFilters );

		filtered = filtered.Where( card => card.Name.Contains( SearchText, StringComparison.OrdinalIgnoreCase ) );		List<GameCardViewModel> result = ( sortByRecent ? GameSortMode.LastPlayed : SortMode ) switch
		{
			GameSortMode.Playtime => filtered.OrderByDescending( card => card.PlaytimeSeconds ).ThenBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).ToList(),
			GameSortMode.LastPlayed => filtered.OrderByDescending( card => card.LastPlayedAt ).ThenBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).ToList(),
			GameSortMode.Size => filtered.OrderByDescending( card => card.SizeBytes ).ThenBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).ToList(),
			GameSortMode.Platform => filtered.OrderBy( card => card.Game.Platform ).ThenBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).ToList(),
			_ => filtered.OrderBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).ToList()
		};
		Games.Clear();

		foreach ( GameCardViewModel card in result )
		{
			Games.Add( card );
		}

		UpdateFilterCounts();
		StatusText = string.Format( T( "Status_Items" ), Games.Count );
	}
}
