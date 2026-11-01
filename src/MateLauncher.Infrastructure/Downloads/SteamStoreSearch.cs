using System.Text.Json;

namespace MateLauncher.Infrastructure.Downloads;

public record SteamSearchResult( string AppId, string Name, string? ImageUrl, string PriceText );

public class SteamStoreSearch
{
	private static readonly HttpClient _HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds( 12 ) };

	public async Task<IReadOnlyList<SteamSearchResult>> SearchAsync( string term, string language )
	{
		if ( string.IsNullOrWhiteSpace( term ) )
		{
			return Array.Empty<SteamSearchResult>();
		}

		try
		{
			string steamLanguage = language == "fr" ? "french" : "english";
			string countryCode = language == "fr" ? "FR" : "US";
			string url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString( term.Trim() )}&l={steamLanguage}&cc={countryCode}";
			using JsonDocument document = JsonDocument.Parse( await _HttpClient.GetStringAsync( url ) );

			if ( !document.RootElement.TryGetProperty( "items", out JsonElement items ) )
			{
				return Array.Empty<SteamSearchResult>();
			}

			return items.EnumerateArray().Take( 12 ).Select( ReadItem ).ToList();
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException )
		{
			return Array.Empty<SteamSearchResult>();
		}
	}

	private static SteamSearchResult ReadItem( JsonElement item )
	{
		string appId = item.GetProperty( "id" ).GetInt32().ToString();
		string name = item.TryGetProperty( "name", out JsonElement nameElement ) ? nameElement.GetString() ?? string.Empty : string.Empty;
		string? image = item.TryGetProperty( "tiny_image", out JsonElement imageElement ) ? imageElement.GetString() : null;
		string price = string.Empty;

		if ( item.TryGetProperty( "price", out JsonElement priceElement ) && priceElement.TryGetProperty( "final", out JsonElement finalElement ) )
		{
			price = finalElement.GetInt32() == 0 ? "0" : ( finalElement.GetInt32() / 100.0 ).ToString( "0.00" );
		}

		return new SteamSearchResult( appId, name, image, price );
	}
}
