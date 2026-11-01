using MateLauncher.Core.Models;
using MateLauncher.Providers.Epic;

namespace MateLauncher.Tests.Providers;

public class EpicManifestTests : IDisposable
{
	private readonly string _Folder = Path.Combine( Path.GetTempPath(), "MateLauncherTests-" + Guid.NewGuid() );

	public EpicManifestTests()
	{
		Directory.CreateDirectory( _Folder );
	}

	public void Dispose()
	{
		Directory.Delete( _Folder, true );
	}

	private string WriteManifest( string json )
	{
		string path = Path.Combine( _Folder, "game.item" );
		File.WriteAllText( path, json );
		return path;
	}

	private static Dictionary<string, string> Covers()
	{
		return new Dictionary<string, string> { [ "item1" ] = "https://example.com/cover.jpg" };
	}

	[Fact]
	public void ReadManifest_BuildsGameWithLaunchUri()
	{
		string path = WriteManifest( """
			{ "AppName": "Fortnite", "DisplayName": "Fortnite", "CatalogNamespace": "fn", "CatalogItemId": "item1", "InstallLocation": "C:\\Games\\Fortnite", "AppCategories": [ "games" ] }
			""" );

		Game? game = EpicProvider.ReadManifest( path, Covers() );

		Assert.NotNull( game );
		Assert.Equal( GamePlatform.Epic, game.Platform );
		Assert.Equal( "https://example.com/cover.jpg", game.CoverImagePath );
		Assert.Equal( "com.epicgames.launcher://apps/fn%3Aitem1%3AFortnite?action=launch&silent=true", game.LaunchCommand );
	}

	[Fact]
	public void ReadManifest_IgnoresIncompleteInstall()
	{
		string path = WriteManifest( """{ "AppName": "A", "DisplayName": "A", "bIsIncompleteInstall": true }""" );

		Assert.Null( EpicProvider.ReadManifest( path, Covers() ) );
	}

	[Fact]
	public void ReadManifest_IgnoresAddons()
	{
		string path = WriteManifest( """{ "AppName": "DLC", "MainGameAppName": "Base", "DisplayName": "DLC" }""" );

		Assert.Null( EpicProvider.ReadManifest( path, Covers() ) );
	}

	[Fact]
	public void ReadManifest_IgnoresUnacceptedCategories()
	{
		string path = WriteManifest( """{ "AppName": "A", "DisplayName": "A", "AppCategories": [ "plugins" ] }""" );

		Assert.Null( EpicProvider.ReadManifest( path, Covers() ) );
	}

	[Fact]
	public void ReadManifest_ReturnsNullOnInvalidJson()
	{
		string path = WriteManifest( "{ not json" );

		Assert.Null( EpicProvider.ReadManifest( path, Covers() ) );
	}
}
