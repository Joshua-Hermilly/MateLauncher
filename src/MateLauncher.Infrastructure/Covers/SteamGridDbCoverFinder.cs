using System.Net.Http.Headers;
using System.Text.Json;
using MateLauncher.Core.Abstractions;

namespace MateLauncher.Infrastructure.Covers;

public class SteamGridDbCoverFinder : ICoverFinder
{
	private const string ApiRoot = "https://www.steamgriddb.com/api/v2";

	private static readonly HttpClient _HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds( 15 ) };

	private readonly ISettingsService _SettingsService;

	public SteamGridDbCoverFinder( ISettingsService settingsService )
	{
		_SettingsService = settingsService;
	}

	public async Task<string?> FindCoverUrlAsync( string gameName )
	{
		string apiKey = _SettingsService.Load().SteamGridDbApiKey.Trim();

		if ( apiKey.Length == 0 )
		{
			return null;
		}

		try
		{
			int? gameId = await FindGameIdAsync( apiKey, gameName );

			if ( gameId is null )
			{
				return null;
			}

			using JsonDocument grids = await GetJsonAsync( apiKey, $"{ApiRoot}/grids/game/{gameId}?dimensions=600x900&types=static&nsfw=false&humor=false" );
			return ReadFirstUrl( grids.RootElement );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or KeyNotFoundException )
		{
			return null;
		}
	}

	public static string? ReadFirstUrl( JsonElement root )
	{
		if ( !root.TryGetProperty( "data", out JsonElement data ) || data.ValueKind != JsonValueKind.Array )
		{
			return null;
		}

		foreach ( JsonElement item in data.EnumerateArray() )
		{
			if ( item.TryGetProperty( "url", out JsonElement url ) && url.GetString() is { Length: > 0 } value && value.StartsWith( "https://", StringComparison.OrdinalIgnoreCase ) )
			{
				return value;
			}
		}

		return null;
	}

	public static int? ReadFirstGameId( JsonElement root )
	{
		if ( !root.TryGetProperty( "data", out JsonElement data ) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0 )
		{
			return null;
		}

		return data[ 0 ].TryGetProperty( "id", out JsonElement id ) && id.TryGetInt32( out int value ) ? value : null;
	}

	private static async Task<int?> FindGameIdAsync( string apiKey, string gameName )
	{
		using JsonDocument search = await GetJsonAsync( apiKey, $"{ApiRoot}/search/autocomplete/{Uri.EscapeDataString( gameName )}" );
		return ReadFirstGameId( search.RootElement );
	}

	private static async Task<JsonDocument> GetJsonAsync( string apiKey, string url )
	{
		using HttpRequestMessage request = new HttpRequestMessage( HttpMethod.Get, url );
		request.Headers.Authorization = new AuthenticationHeaderValue( "Bearer", apiKey );
		using HttpResponseMessage response = await _HttpClient.SendAsync( request );
		response.EnsureSuccessStatusCode();
		return JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
	}
}
