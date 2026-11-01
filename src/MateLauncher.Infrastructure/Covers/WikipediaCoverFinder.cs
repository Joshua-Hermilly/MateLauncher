using System.Text.Json;
using MateLauncher.Core.Abstractions;

namespace MateLauncher.Infrastructure.Covers;

public class WikipediaCoverFinder : ICoverFinder
{
	private static readonly HttpClient _HttpClient = CreateHttpClient();

	private static HttpClient CreateHttpClient()
	{
		HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds( 15 ) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd( "MateLauncher/0.2 (local game launcher)" );
		return client;
	}

	public async Task<string?> FindCoverUrlAsync( string gameName )
	{
		try
		{
			string url = $"https://en.wikipedia.org/w/api.php?action=query&generator=search&gsrsearch={Uri.EscapeDataString( gameName + " video game" )}&gsrlimit=5&prop=pageimages&piprop=thumbnail&pithumbsize=600&format=json";
			using JsonDocument document = JsonDocument.Parse( await _HttpClient.GetStringAsync( url ) );

			if ( !document.RootElement.TryGetProperty( "query", out JsonElement query ) )
			{
				return null;
			}

			string normalizedName = Normalize( gameName );

			return query.GetProperty( "pages" ).EnumerateObject()
				.Select( page => page.Value )
				.OrderBy( page => page.GetProperty( "index" ).GetInt32() )
				.Where( page => Normalize( page.GetProperty( "title" ).GetString() ?? string.Empty ) == normalizedName )
				.Where( page => page.TryGetProperty( "thumbnail", out _ ) )
				.Select( page => page.GetProperty( "thumbnail" ).GetProperty( "source" ).GetString() )
				.FirstOrDefault();
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or KeyNotFoundException )
		{
			return null;
		}
	}

	private static string Normalize( string value )
	{
		return new string( value.Where( char.IsLetterOrDigit ).Select( char.ToLowerInvariant ).ToArray() );
	}
}
