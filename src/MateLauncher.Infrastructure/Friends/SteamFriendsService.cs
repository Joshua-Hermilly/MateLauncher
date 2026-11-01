using System.Net;
using System.Text.Json;

namespace MateLauncher.Infrastructure.Friends;

public enum FriendState
{
	InGame,
	Online,
	Offline
}

public record SteamFriend( string SteamId, string Name, string? AvatarUrl, FriendState State, string? GameName, string? GameId );

public record FriendsResult( IReadOnlyList<SteamFriend> Friends, string? ErrorKey );

public record OwnedGamesResult( IReadOnlySet<string>? AppIds, string? ErrorKey );

public class SteamFriendsService
{
	private const string ApiRoot = "https://api.steampowered.com";
	private const int SummaryBatchSize = 100;

	private static readonly HttpClient _HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds( 15 ) };

	public async Task<FriendsResult> GetFriendsAsync( string apiKey, string steamId )
	{
		string key = Uri.EscapeDataString( apiKey.Trim() );

		try
		{
			string listUrl = $"{ApiRoot}/ISteamUser/GetFriendList/v1/?key={key}&steamid={Uri.EscapeDataString( steamId.Trim() )}&relationship=friend";
			using HttpResponseMessage listResponse = await _HttpClient.GetAsync( listUrl );

			if ( listResponse.StatusCode is HttpStatusCode.Forbidden )
			{
				return new FriendsResult( Array.Empty<SteamFriend>(), "Steam_BadKey" );
			}

			if ( listResponse.StatusCode is HttpStatusCode.Unauthorized )
			{
				return new FriendsResult( Array.Empty<SteamFriend>(), "Friends_Private" );
			}

			listResponse.EnsureSuccessStatusCode();
			using JsonDocument listDocument = JsonDocument.Parse( await listResponse.Content.ReadAsStringAsync() );
			List<string> friendIds = ParseFriendIds( listDocument.RootElement );
			List<SteamFriend> friends = new List<SteamFriend>();

			foreach ( string[] batch in friendIds.Chunk( SummaryBatchSize ) )
			{
				string summariesUrl = $"{ApiRoot}/ISteamUser/GetPlayerSummaries/v2/?key={key}&steamids={string.Join( ',', batch )}";
				using JsonDocument summaries = JsonDocument.Parse( await _HttpClient.GetStringAsync( summariesUrl ) );
				friends.AddRange( ParsePlayers( summaries.RootElement ) );
			}

			return new FriendsResult( Sort( friends ), null );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException or InvalidOperationException )
		{
			return new FriendsResult( Array.Empty<SteamFriend>(), "Friends_Error" );
		}
	}

	public async Task<OwnedGamesResult> GetOwnedAppIdsAsync( string apiKey, string steamId )
	{
		try
		{
			string url = $"{ApiRoot}/IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString( apiKey.Trim() )}&steamid={Uri.EscapeDataString( steamId.Trim() )}&include_played_free_games=1";
			using HttpResponseMessage response = await _HttpClient.GetAsync( url );

			if ( !response.IsSuccessStatusCode )
			{
				return new OwnedGamesResult( null, "Friends_Private" );
			}

			using JsonDocument document = JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
			IReadOnlySet<string>? appIds = ParseOwnedAppIds( document.RootElement );
			return new OwnedGamesResult( appIds, appIds is null ? "Friends_Private" : null );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException or InvalidOperationException )
		{
			return new OwnedGamesResult( null, "Friends_Error" );
		}
	}

	public static List<string> ParseFriendIds( JsonElement root )
	{
		if ( !root.TryGetProperty( "friendslist", out JsonElement list ) || !list.TryGetProperty( "friends", out JsonElement friends ) || friends.ValueKind != JsonValueKind.Array )
		{
			return new List<string>();
		}

		return friends.EnumerateArray()
			.Select( friend => friend.TryGetProperty( "steamid", out JsonElement id ) ? id.GetString() ?? string.Empty : string.Empty )
			.Where( id => id.Length > 0 && id.All( char.IsDigit ) )
			.ToList();
	}

	public static List<SteamFriend> ParsePlayers( JsonElement root )
	{
		List<SteamFriend> friends = new List<SteamFriend>();

		if ( !root.TryGetProperty( "response", out JsonElement response ) || !response.TryGetProperty( "players", out JsonElement players ) || players.ValueKind != JsonValueKind.Array )
		{
			return friends;
		}

		foreach ( JsonElement player in players.EnumerateArray() )
		{
			string steamId = ReadString( player, "steamid" );

			if ( steamId.Length == 0 || !steamId.All( char.IsDigit ) )
			{
				continue;
			}

			int personaState = player.TryGetProperty( "personastate", out JsonElement stateElement ) && stateElement.TryGetInt32( out int state ) ? state : 0;
			string gameName = ReadString( player, "gameextrainfo" );
			string gameId = ReadString( player, "gameid" );
			bool isInGame = gameName.Length > 0 || gameId.Length > 0;
			string avatar = ReadString( player, "avatarmedium" );
			string name = ReadString( player, "personaname" );

			friends.Add( new SteamFriend(
				steamId,
				name.Length > 0 ? name : steamId,
				avatar.StartsWith( "https://", StringComparison.OrdinalIgnoreCase ) ? avatar : null,
				isInGame ? FriendState.InGame : personaState == 0 ? FriendState.Offline : FriendState.Online,
				gameName.Length > 0 ? gameName : null,
				gameId.Length > 0 ? gameId : null ) );
		}

		return friends;
	}

	public static IReadOnlySet<string>? ParseOwnedAppIds( JsonElement root )
	{
		if ( !root.TryGetProperty( "response", out JsonElement response ) || !response.TryGetProperty( "games", out JsonElement games ) || games.ValueKind != JsonValueKind.Array )
		{
			return null;
		}

		return games.EnumerateArray()
			.Select( game => game.TryGetProperty( "appid", out JsonElement appId ) && appId.TryGetInt64( out long value ) ? value.ToString() : string.Empty )
			.Where( appId => appId.Length > 0 )
			.ToHashSet();
	}

	public static IReadOnlyList<SteamFriend> Sort( IEnumerable<SteamFriend> friends )
	{
		return friends.OrderBy( friend => friend.State ).ThenBy( friend => friend.Name, StringComparer.CurrentCultureIgnoreCase ).ToList();
	}

	private static string ReadString( JsonElement element, string propertyName )
	{
		if ( !element.TryGetProperty( propertyName, out JsonElement value ) )
		{
			return string.Empty;
		}

		return value.ValueKind switch
		{
			JsonValueKind.String => value.GetString() ?? string.Empty,
			JsonValueKind.Number => value.GetRawText(),
			_ => string.Empty
		};
	}
}
