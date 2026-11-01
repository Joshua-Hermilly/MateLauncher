using System.Text.Json;
using MateLauncher.Core.Abstractions;

namespace MateLauncher.Infrastructure.Covers;

public class GoogleImageCoverFinder : ICoverFinder
{
	private static readonly HttpClient _HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds( 15 ) };

	private readonly ISettingsService _SettingsService;

	public GoogleImageCoverFinder( ISettingsService settingsService )
	{
		_SettingsService = settingsService;
	}

	public async Task<string?> FindCoverUrlAsync( string gameName )
	{
		MateLauncher.Core.Models.AppSettings settings = _SettingsService.Load();

		if ( string.IsNullOrWhiteSpace( settings.GoogleApiKey ) || string.IsNullOrWhiteSpace( settings.GoogleSearchEngineId ) )
		{
			return null;
		}

		try
		{
			string query = Uri.EscapeDataString( $"{gameName} video game cover art" );
			string url = $"https://www.googleapis.com/customsearch/v1?key={Uri.EscapeDataString( settings.GoogleApiKey.Trim() )}&cx={Uri.EscapeDataString( settings.GoogleSearchEngineId.Trim() )}&searchType=image&num=3&safe=active&q={query}";
			using JsonDocument document = JsonDocument.Parse( await _HttpClient.GetStringAsync( url ) );

			if ( !document.RootElement.TryGetProperty( "items", out JsonElement items ) )
			{
				return null;
			}

			return items.EnumerateArray()
				.Select( item => item.TryGetProperty( "link", out JsonElement link ) ? link.GetString() : null )
				.FirstOrDefault( link => link is not null && link.StartsWith( "https://", StringComparison.OrdinalIgnoreCase ) );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException )
		{
			return null;
		}
	}
}
