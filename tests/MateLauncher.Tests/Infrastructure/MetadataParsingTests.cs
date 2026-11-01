using System.Text.Json;
using MateLauncher.Core.Models;
using MateLauncher.Infrastructure.Covers;
using MateLauncher.Infrastructure.Metadata;

namespace MateLauncher.Tests.Infrastructure;

public class MetadataParsingTests
{
	private static JsonElement Parse( string json )
	{
		return JsonDocument.Parse( json ).RootElement.Clone();
	}

	[Fact]
	public void IgdbParseFirstGame_ReadsAllFields()
	{
		JsonElement root = Parse( """
			[ { "name": "Portal 2", "summary": "Puzzle game", "genres": [ { "name": "Puzzle" }, { "name": "Adventure" } ],
			    "first_release_date": 1303171200, "total_rating": 92.46,
			    "screenshots": [ { "image_id": "abc" }, { "image_id": "def" } ] } ]
			""" );

		GameMetadata? metadata = IgdbClient.ParseFirstGame( root );

		Assert.NotNull( metadata );
		Assert.Equal( "Portal 2", metadata.Name );
		Assert.Equal( "Puzzle game", metadata.Summary );
		Assert.Equal( new[] { "Puzzle", "Adventure" }, metadata.Genres );
		Assert.Equal( 2011, metadata.ReleaseDate!.Value.Year );
		Assert.Equal( 92.5, metadata.Rating );
		Assert.Equal( "https://images.igdb.com/igdb/image/upload/t_screenshot_big/abc.jpg", metadata.ScreenshotUrls[ 0 ] );
	}

	[Fact]
	public void IgdbParseFirstGame_ToleratesMissingFields()
	{
		GameMetadata? metadata = IgdbClient.ParseFirstGame( Parse( """[ { "name": "Obscure" } ]""" ) );

		Assert.NotNull( metadata );
		Assert.Empty( metadata.Genres );
		Assert.Null( metadata.ReleaseDate );
		Assert.Null( metadata.Rating );
	}

	[Fact]
	public void IgdbParseFirstGame_ReturnsNullOnEmptyResult()
	{
		Assert.Null( IgdbClient.ParseFirstGame( Parse( "[]" ) ) );
	}

	[Fact]
	public void IgdbParseCoverUrl_BuildsImageUrl()
	{
		string? url = IgdbClient.ParseCoverUrl( Parse( """[ { "cover": { "image_id": "co1abc" } } ]""" ) );

		Assert.Equal( "https://images.igdb.com/igdb/image/upload/t_cover_big_2x/co1abc.jpg", url );
	}

	[Fact]
	public void IgdbParseCoverUrl_ReturnsNullWithoutCover()
	{
		Assert.Null( IgdbClient.ParseCoverUrl( Parse( """[ { "name": "x" } ]""" ) ) );
	}

	[Fact]
	public void IgdbEscapeQuery_RemovesCharactersThatBreakTheQuery()
	{
		Assert.Equal( "Portal 2 ; fields", IgdbClient.EscapeQuery( "Portal \"2\" ; fields" ) );
	}

	[Fact]
	public void SteamGridDbReadFirstGameId_ReadsFirstResult()
	{
		Assert.Equal( 5247, SteamGridDbCoverFinder.ReadFirstGameId( Parse( """{ "success": true, "data": [ { "id": 5247, "name": "Portal 2" } ] }""" ) ) );
	}

	[Fact]
	public void SteamGridDbReadFirstGameId_ReturnsNullWhenEmpty()
	{
		Assert.Null( SteamGridDbCoverFinder.ReadFirstGameId( Parse( """{ "success": true, "data": [] }""" ) ) );
	}

	[Fact]
	public void SteamGridDbReadFirstUrl_SkipsNonHttpsLinks()
	{
		string? url = SteamGridDbCoverFinder.ReadFirstUrl( Parse( """{ "data": [ { "url": "http://insecure/x.png" }, { "url": "https://cdn.example/y.png" } ] }""" ) );

		Assert.Equal( "https://cdn.example/y.png", url );
	}
}
