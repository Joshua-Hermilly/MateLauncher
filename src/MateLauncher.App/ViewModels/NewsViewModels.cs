using CommunityToolkit.Mvvm.ComponentModel;
using MateLauncher.Core.Models;
using MateLauncher.Infrastructure.Downloads;

namespace MateLauncher.App.ViewModels;

public class NewsItemViewModel
{
	public string Source { get; }
	public string Title { get; }
	public string Summary { get; }
	public string Link { get; }
	public string? ImageUrl { get; }
	public bool HasImage => ImageUrl is not null;
	public string DateText { get; }

	public NewsItemViewModel( NewsEntry entry )
	{
		Source = entry.Source;
		Title = entry.Title;
		Summary = entry.Summary;
		Link = entry.Link;
		ImageUrl = entry.ImageUrl;
		DateText = entry.PublishedAt == DateTime.MinValue ? string.Empty : entry.PublishedAt.ToString( "dd/MM/yyyy HH:mm" );
	}
}

public partial class FeedEntryViewModel : ObservableObject
{
	public string Title { get; }
	public string Url { get; }

	public FeedEntryViewModel( string title, string url )
	{
		Title = title;
		Url = url;
	}
}

public class SteamResultViewModel
{
	public string AppId { get; }
	public string Name { get; }
	public string? ImageUrl { get; }
	public string PriceText { get; }

	public SteamResultViewModel( SteamSearchResult result )
	{
		AppId = result.AppId;
		Name = result.Name;
		ImageUrl = result.ImageUrl;
		PriceText = string.IsNullOrEmpty( result.PriceText ) ? string.Empty : result.PriceText == "0" ? "Free" : result.PriceText;
	}
}
