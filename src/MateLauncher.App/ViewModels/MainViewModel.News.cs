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
	[RelayCommand]
	public void ToggleNewsPanel()
	{
		IsNewsPanelOpen = !IsNewsPanelOpen;
	}

	partial void OnIncludeGameNewsChanged( bool value )
	{
		if ( _IsLoadingSettings )
		{
			return;
		}

		SaveSettings();

		if ( IsNewsOpen )
		{
			_ = RefreshNewsAsync();
		}
	}

	private static List<NewsFeed> BuildDefaultFeeds()
	{
		return new List<NewsFeed>
		{
			new NewsFeed { Title = "Steam", Url = "https://store.steampowered.com/feeds/news.xml" },
			new NewsFeed { Title = "Rock Paper Shotgun", Url = "https://www.rockpapershotgun.com/feed" },
			new NewsFeed { Title = "PC Gamer", Url = "https://www.pcgamer.com/rss/" }
		};
	}

	[RelayCommand]
	public async Task RefreshNewsAsync()
	{
		IsNewsLoading = true;
		NewsStatus = T( "News_Loading" );
		List<NewsFeed> feeds = NewsFeedEntries.Select( feed => new NewsFeed { Title = feed.Title, Url = feed.Url } ).ToList();

		if ( IncludeGameNews )
		{
			IEnumerable<GameCardViewModel> steamCards = _AllCards
				.Where( card => card.Game.Platform == GamePlatform.Steam && !card.Game.IsHidden && long.TryParse( card.Game.PlatformGameId, out _ ) )
				.OrderByDescending( card => card.PlaytimeSeconds )
				.ThenBy( card => card.Name )
				.Take( 8 );

			foreach ( GameCardViewModel card in steamCards )
			{
				feeds.Add( new NewsFeed { Title = card.Name, Url = NewsService.BuildSteamAppFeedUrl( card.Game.PlatformGameId ) } );
			}
		}

		IReadOnlyList<NewsEntry> entries = await _NewsService.FetchAsync( feeds );
		NewsItems.Clear();

		foreach ( NewsEntry entry in entries )
		{
			NewsItems.Add( new NewsItemViewModel( entry ) );
		}

		HasNoNews = NewsItems.Count == 0;
		NewsStatus = string.Format( T( "News_Updated" ), DateTime.Now.ToString( "HH:mm" ), feeds.Count );
		IsNewsLoading = false;
	}

	[RelayCommand]
	public async Task AddFeedAsync()
	{
		string? url = InputDialog.Ask( T( "News_AddFeed" ), T( "News_FeedUrl" ), "https://" );

		if ( url is null || !Uri.TryCreate( url, UriKind.Absolute, out Uri? uri ) || uri.Scheme is not ( "http" or "https" ) || NewsFeedEntries.Any( feed => string.Equals( feed.Url, url, StringComparison.OrdinalIgnoreCase ) ) )
		{
			return;
		}

		string title = InputDialog.Ask( T( "News_AddFeed" ), T( "News_FeedTitle" ), uri.Host ) ?? uri.Host;
		NewsFeedEntries.Add( new FeedEntryViewModel( title, url ) );
		SaveSettings();
		await RefreshNewsAsync();
	}

	[RelayCommand]
	public async Task RemoveFeedAsync( FeedEntryViewModel? feed )
	{
		if ( feed is null )
		{
			return;
		}

		NewsFeedEntries.Remove( feed );
		SaveSettings();
		await RefreshNewsAsync();
	}
}
