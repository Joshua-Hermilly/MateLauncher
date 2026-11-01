using System.Text.Json;
using MateLauncher.Infrastructure.Friends;

namespace MateLauncher.Tests.Infrastructure;

public class SteamFriendsServiceTests
{
	private static JsonElement Parse( string json )
	{
		return JsonDocument.Parse( json ).RootElement.Clone();
	}

	[Fact]
	public void ParseFriendIds_ReadsOnlyNumericIdentifiers()
	{
		List<string> ids = SteamFriendsService.ParseFriendIds( Parse( """{ "friendslist": { "friends": [ { "steamid": "76561198000000001" }, { "steamid": "abc" }, { "steamid": "76561198000000002" } ] } }""" ) );

		Assert.Equal( new[] { "76561198000000001", "76561198000000002" }, ids );
	}

	[Fact]
	public void ParseFriendIds_ReturnsEmptyWhenListIsMissing()
	{
		Assert.Empty( SteamFriendsService.ParseFriendIds( Parse( "{}" ) ) );
	}

	[Fact]
	public void ParsePlayers_DerivesStateFromPersonaAndGame()
	{
		List<SteamFriend> friends = SteamFriendsService.ParsePlayers( Parse( """
			{ "response": { "players": [
				{ "steamid": "1", "personaname": "Offline", "personastate": 0 },
				{ "steamid": "2", "personaname": "Online", "personastate": 1, "avatarmedium": "https://cdn/a.jpg" },
				{ "steamid": "3", "personaname": "Gamer", "personastate": 1, "gameextrainfo": "Portal 2", "gameid": "620" }
			] } }
			""" ) );

		Assert.Equal( FriendState.Offline, friends[ 0 ].State );
		Assert.Equal( FriendState.Online, friends[ 1 ].State );
		Assert.Equal( "https://cdn/a.jpg", friends[ 1 ].AvatarUrl );
		Assert.Equal( FriendState.InGame, friends[ 2 ].State );
		Assert.Equal( "Portal 2", friends[ 2 ].GameName );
		Assert.Equal( "620", friends[ 2 ].GameId );
	}

	[Fact]
	public void ParsePlayers_RejectsNonHttpsAvatarsAndInvalidIdentifiers()
	{
		List<SteamFriend> friends = SteamFriendsService.ParsePlayers( Parse( """
			{ "response": { "players": [
				{ "steamid": "1", "personaname": "A", "avatarmedium": "http://insecure/a.jpg" },
				{ "steamid": "x;y", "personaname": "Bad" }
			] } }
			""" ) );

		Assert.Single( friends );
		Assert.Null( friends[ 0 ].AvatarUrl );
	}

	[Fact]
	public void ParsePlayers_UsesIdentifierWhenNameIsMissing()
	{
		List<SteamFriend> friends = SteamFriendsService.ParsePlayers( Parse( """{ "response": { "players": [ { "steamid": "42" } ] } }""" ) );

		Assert.Equal( "42", friends[ 0 ].Name );
	}

	[Fact]
	public void Sort_PutsPlayersFirstThenOnlineThenOfflineAlphabetically()
	{
		List<SteamFriend> unsorted = new List<SteamFriend>
		{
			new SteamFriend( "1", "Zed", null, FriendState.Offline, null, null ),
			new SteamFriend( "2", "bob", null, FriendState.Online, null, null ),
			new SteamFriend( "3", "Amy", null, FriendState.Online, null, null ),
			new SteamFriend( "4", "Max", null, FriendState.InGame, "X", "1" )
		};

		Assert.Equal( new[] { "Max", "Amy", "bob", "Zed" }, SteamFriendsService.Sort( unsorted ).Select( friend => friend.Name ).ToArray() );
	}

	[Fact]
	public void ParseOwnedAppIds_ReturnsSetOfIdentifiers()
	{
		IReadOnlySet<string>? ids = SteamFriendsService.ParseOwnedAppIds( Parse( """{ "response": { "game_count": 2, "games": [ { "appid": 620 }, { "appid": 440 } ] } }""" ) );

		Assert.NotNull( ids );
		Assert.Contains( "620", ids );
		Assert.Contains( "440", ids );
	}

	[Fact]
	public void ParseOwnedAppIds_ReturnsNullWhenLibraryIsPrivate()
	{
		Assert.Null( SteamFriendsService.ParseOwnedAppIds( Parse( """{ "response": { } }""" ) ) );
	}
}
