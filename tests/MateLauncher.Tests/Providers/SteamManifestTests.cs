using MateLauncher.Core.Models;
using MateLauncher.Providers.Steam;

namespace MateLauncher.Tests.Providers;

public class SteamManifestTests : IDisposable
{
	private readonly string _Folder = Path.Combine( Path.GetTempPath(), "MateLauncherTests-" + Guid.NewGuid() );

	public SteamManifestTests()
	{
		Directory.CreateDirectory( _Folder );
	}

	public void Dispose()
	{
		Directory.Delete( _Folder, true );
	}

	private string WriteManifest( string content )
	{
		string path = Path.Combine( _Folder, "appmanifest_1.acf" );
		File.WriteAllText( path, content );
		return path;
	}

	[Fact]
	public void ReadManifest_ReadsIdentityAndLaunchCommand()
	{
		string path = WriteManifest( "\"AppState\"\n{\n\t\"appid\"\t\t\"620\"\n\t\"name\"\t\t\"Portal 2\"\n\t\"installdir\"\t\t\"Portal 2\"\n}" );

		Game? game = SteamProvider.ReadManifest( path, _Folder, _Folder );

		Assert.NotNull( game );
		Assert.Equal( "Portal 2", game.Name );
		Assert.Equal( GamePlatform.Steam, game.Platform );
		Assert.Equal( "620", game.PlatformGameId );
		Assert.Equal( "steam://run/620", game.LaunchCommand );
		Assert.Equal( Path.Combine( _Folder, "common", "Portal 2" ), game.InstallPath );
	}

	[Fact]
	public void ReadManifest_FallsBackToSteamCdnCover()
	{
		string path = WriteManifest( "\"AppState\"\n{\n\t\"appid\"\t\"620\"\n\t\"name\"\t\"Portal 2\"\n}" );

		Game? game = SteamProvider.ReadManifest( path, _Folder, _Folder );

		Assert.Equal( "https://cdn.cloudflare.steamstatic.com/steam/apps/620/library_600x900.jpg", game!.CoverImagePath );
		Assert.Equal( string.Empty, game.InstallPath );
	}

	[Fact]
	public void ReadManifest_ReturnsNullWithoutAppId()
	{
		string path = WriteManifest( "\"AppState\"\n{\n\t\"name\"\t\"Broken\"\n}" );

		Assert.Null( SteamProvider.ReadManifest( path, _Folder, _Folder ) );
	}

	[Fact]
	public void ReadManifest_IsCaseInsensitiveOnKeys()
	{
		string path = WriteManifest( "\"AppState\"\n{\n\t\"AppID\"\t\"10\"\n\t\"Name\"\t\"Counter-Strike\"\n}" );

		Game? game = SteamProvider.ReadManifest( path, _Folder, _Folder );

		Assert.Equal( "10", game!.PlatformGameId );
	}
}
