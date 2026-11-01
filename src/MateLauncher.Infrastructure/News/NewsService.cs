using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MateLauncher.Core.Models;

namespace MateLauncher.Infrastructure.News;

public partial class NewsService
{
	private const int MaxItemsPerFeed = 8;
	private const int MaxItems = 80;

	private static readonly HttpClient _HttpClient = CreateClient();

	private static readonly XNamespace _AtomNamespace = "http://www.w3.org/2005/Atom";
	private static readonly XNamespace _MediaNamespace = "http://search.yahoo.com/mrss/";
	private static readonly XNamespace _ContentNamespace = "http://purl.org/rss/1.0/modules/content/";

	[GeneratedRegex( "<[^>]+>" )]
	private static partial Regex TagPattern();

	[GeneratedRegex( "<img[^>]+src=[\"'](?<url>[^\"']+)[\"']", RegexOptions.IgnoreCase )]
	private static partial Regex ImagePattern();

	private static HttpClient CreateClient()
	{
		HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds( 12 ) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd( "MateLauncher/0.3 (+https://github.com/)" );
		return client;
	}

	public static string BuildSteamAppFeedUrl( string appId )
	{
		return $"https://store.steampowered.com/feeds/news/app/{appId}/";
	}

	public async Task<IReadOnlyList<NewsEntry>> FetchAsync( IEnumerable<NewsFeed> feeds )
	{
		IEnumerable<Task<IReadOnlyList<NewsEntry>>> tasks = feeds.Select( FetchFeedAsync );
		IReadOnlyList<NewsEntry>[] results = await Task.WhenAll( tasks );

		return results
			.SelectMany( entries => entries )
			.GroupBy( entry => entry.Link )
			.Select( group => group.First() )
			.OrderByDescending( entry => entry.PublishedAt )
			.Take( MaxItems )
			.ToList();
	}

	private static async Task<IReadOnlyList<NewsEntry>> FetchFeedAsync( NewsFeed feed )
	{
		try
		{
			string content = await _HttpClient.GetStringAsync( feed.Url );
			XDocument document = XDocument.Parse( content );
			XElement? root = document.Root;

			if ( root is null )
			{
				return Array.Empty<NewsEntry>();
			}

			IEnumerable<XElement> items = root.Name.LocalName == "feed" ? root.Elements( _AtomNamespace + "entry" ) : root.Descendants( "item" );
			return items.Take( MaxItemsPerFeed ).Select( item => ReadItem( feed, item ) ).Where( entry => entry is not null ).Select( entry => entry! ).ToList();
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or TaskCanceledException or System.Xml.XmlException or InvalidOperationException )
		{
			return Array.Empty<NewsEntry>();
		}
	}

	private static NewsEntry? ReadItem( NewsFeed feed, XElement item )
	{
		bool isAtom = item.Name.Namespace == _AtomNamespace;
		string title = Clean( ( isAtom ? item.Element( _AtomNamespace + "title" ) : item.Element( "title" ) )?.Value );
		string link = isAtom
			? item.Elements( _AtomNamespace + "link" ).FirstOrDefault( element => ( string? ) element.Attribute( "rel" ) is null or "alternate" )?.Attribute( "href" )?.Value ?? string.Empty
			: item.Element( "link" )?.Value.Trim() ?? string.Empty;

		if ( title.Length == 0 || !Uri.TryCreate( link, UriKind.Absolute, out Uri? uri ) || uri.Scheme is not ( "http" or "https" ) )
		{
			return null;
		}

		string dateText = ( isAtom ? ( item.Element( _AtomNamespace + "published" ) ?? item.Element( _AtomNamespace + "updated" ) ) : ( item.Element( "pubDate" ) ?? item.Element( "date" ) ) )?.Value ?? string.Empty;
		DateTime publishedAt = DateTimeOffset.TryParse( dateText, out DateTimeOffset parsedDate ) ? parsedDate.LocalDateTime : DateTime.MinValue;

		string rawBody = ( isAtom ? ( item.Element( _AtomNamespace + "content" ) ?? item.Element( _AtomNamespace + "summary" ) ) : ( item.Element( _ContentNamespace + "encoded" ) ?? item.Element( "description" ) ) )?.Value ?? string.Empty;
		string summary = Clean( rawBody );

		if ( summary.Length > 260 )
		{
			summary = summary[ ..260 ].TrimEnd() + "…";
		}

		string? imageUrl = item.Element( _MediaNamespace + "thumbnail" )?.Attribute( "url" )?.Value
			?? item.Element( _MediaNamespace + "content" )?.Attribute( "url" )?.Value
			?? item.Elements( "enclosure" ).FirstOrDefault( element => ( ( string? ) element.Attribute( "type" ) )?.StartsWith( "image" ) == true )?.Attribute( "url" )?.Value
			?? ImagePattern().Match( rawBody ).Groups[ "url" ].Value;

		if ( string.IsNullOrWhiteSpace( imageUrl ) || !imageUrl.StartsWith( "http", StringComparison.OrdinalIgnoreCase ) )
		{
			imageUrl = null;
		}

		return new NewsEntry( feed.Title, title, link, publishedAt, summary, imageUrl );
	}

	private static string Clean( string? html )
	{
		if ( string.IsNullOrWhiteSpace( html ) )
		{
			return string.Empty;
		}

		string text = WebUtility.HtmlDecode( TagPattern().Replace( html, " " ) );
		return string.Join( ' ', text.Split( ( char[]? ) null, StringSplitOptions.RemoveEmptyEntries ) );
	}
}
