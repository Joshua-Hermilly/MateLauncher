using System.Text.Json;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;
using MateLauncher.Infrastructure.Steam;

namespace MateLauncher.Tests.Core;

public class PlaytimeImportTests
{
	[Fact]
	public void ComputeUpdates_OnlyRaisesPlaytimeAndFillsMissingLastPlayed()
	{
		Game behind = new Game { Platform = GamePlatform.Steam, PlatformGameId = "10", PlaytimeSeconds = 100 };
		Game ahead = new Game { Platform = GamePlatform.Steam, PlatformGameId = "20", PlaytimeSeconds = 9000, LastPlayedAt = new DateTime( 2026, 1, 1 ) };
		Game unknown = new Game { Platform = GamePlatform.Steam, PlatformGameId = "30" };
		Game epic = new Game { Platform = GamePlatform.Epic, PlatformGameId = "10" };
		Dictionary<string, ImportedPlaytime> steam = new Dictionary<string, ImportedPlaytime>
		{
			[ "10" ] = new ImportedPlaytime( 7200, new DateTime( 2026, 5, 5 ) ),
			[ "20" ] = new ImportedPlaytime( 3600, new DateTime( 2026, 9, 9 ) )
		};

		Dictionary<Guid, ImportedPlaytime> updates = PlaytimeImport.ComputeUpdates( new[] { behind, ahead, unknown, epic }, steam );

		Assert.Single( updates );
		Assert.Equal( 7200, updates[ behind.Id ].Seconds );
		Assert.Equal( new DateTime( 2026, 5, 5 ), updates[ behind.Id ].LastPlayedAt );
	}

	[Fact]
	public void ParseOwnedPlaytimes_ConvertsMinutesToSeconds()
	{
		JsonElement root = JsonDocument.Parse( """{ "response": { "games": [ { "appid": 620, "playtime_forever": 90, "rtime_last_played": 1700000000 }, { "appid": 440, "playtime_forever": 0 } ] } }""" ).RootElement;

		IReadOnlyDictionary<string, ImportedPlaytime>? playtimes = SteamPlaytimeService.ParseOwnedPlaytimes( root );

		Assert.NotNull( playtimes );
		Assert.Equal( 5400, playtimes[ "620" ].Seconds );
		Assert.NotNull( playtimes[ "620" ].LastPlayedAt );
		Assert.Null( playtimes[ "440" ].LastPlayedAt );
	}

	[Fact]
	public void ParseOwnedPlaytimes_ReturnsNullWhenLibraryIsPrivate()
	{
		Assert.Null( SteamPlaytimeService.ParseOwnedPlaytimes( JsonDocument.Parse( """{ "response": { } }""" ).RootElement ) );
	}
}
