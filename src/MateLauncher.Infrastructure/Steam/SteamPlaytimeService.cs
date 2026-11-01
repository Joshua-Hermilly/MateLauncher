using System.Text.Json;
using MateLauncher.Core.Models;

namespace MateLauncher.Infrastructure.Steam;

public record SteamPlaytimeResult( IReadOnlyDictionary<string, ImportedPlaytime>? Playtimes, string? ErrorKey );

public class SteamPlaytimeService
{
	private static readonly HttpClient _HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds( 30 ) };

	public async Task<SteamPlaytimeResult> GetOwnedPlaytimesAsync( string apiKey, string steamId )
	{
		try
		{
			string url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString( apiKey.Trim() )}&steamid={Uri.EscapeDataString( steamId.Trim() )}&include_played_free_games=1";
			using HttpResponseMessage response = await _HttpClient.GetAsync( url );

			if ( response.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized )
			{
				return new SteamPlaytimeResult( null, "Steam_BadKey" );
			}

			response.EnsureSuccessStatusCode();
			using JsonDocument document = JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
			IReadOnlyDictionary<string, ImportedPlaytime>? playtimes = ParseOwnedPlaytimes( document.RootElement );
			return playtimes is null ? new SteamPlaytimeResult( null, "Import_Private" ) : new SteamPlaytimeResult( playtimes, null );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException )
		{
			return new SteamPlaytimeResult( null, "Import_Error" );
		}
	}

	public static IReadOnlyDictionary<string, ImportedPlaytime>? ParseOwnedPlaytimes( JsonElement root )
	{
		if ( !root.TryGetProperty( "response", out JsonElement response ) || !response.TryGetProperty( "games", out JsonElement games ) || games.ValueKind != JsonValueKind.Array )
		{
			return null;
		}

		Dictionary<string, ImportedPlaytime> playtimes = new Dictionary<string, ImportedPlaytime>();

		foreach ( JsonElement game in games.EnumerateArray() )
		{
			if ( !game.TryGetProperty( "appid", out JsonElement appId ) || !appId.TryGetInt64( out long id ) )
			{
				continue;
			}

			long minutes = game.TryGetProperty( "playtime_forever", out JsonElement forever ) && forever.TryGetInt64( out long value ) ? Math.Max( 0, value ) : 0;
			DateTime? lastPlayed = game.TryGetProperty( "rtime_last_played", out JsonElement last ) && last.TryGetInt64( out long unix ) && unix > 0
				? DateTimeOffset.FromUnixTimeSeconds( unix ).LocalDateTime
				: null;

			playtimes[ id.ToString() ] = new ImportedPlaytime( minutes * 60, lastPlayed );
		}

		return playtimes;
	}
}
