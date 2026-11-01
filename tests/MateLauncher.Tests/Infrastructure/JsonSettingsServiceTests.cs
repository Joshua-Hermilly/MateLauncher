using MateLauncher.Core.Models;
using MateLauncher.Infrastructure.Settings;

namespace MateLauncher.Tests.Infrastructure;

public class JsonSettingsServiceTests : IDisposable
{
	private readonly string _Folder = Path.Combine( Path.GetTempPath(), "MateLauncherTests-" + Guid.NewGuid() );

	public JsonSettingsServiceTests()
	{
		Directory.CreateDirectory( _Folder );
	}

	public void Dispose()
	{
		Directory.Delete( _Folder, true );
	}

	[Fact]
	public void Save_DoesNotWriteApiKeysInClear()
	{
		string path = Path.Combine( _Folder, "settings.json" );
		JsonSettingsService service = new JsonSettingsService( path );

		service.Save( new AppSettings { SteamApiKey = "STEAMKEY123", GoogleApiKey = "GOOGLEKEY456" } );

		string content = File.ReadAllText( path );
		Assert.DoesNotContain( "STEAMKEY123", content );
		Assert.DoesNotContain( "GOOGLEKEY456", content );
	}

	[Fact]
	public void Load_RestoresSavedApiKeys()
	{
		string path = Path.Combine( _Folder, "settings.json" );
		JsonSettingsService service = new JsonSettingsService( path );

		service.Save( new AppSettings { SteamApiKey = "STEAMKEY123", SteamGridDbApiKey = "GRID", Language = "en" } );
		AppSettings loaded = service.Load();

		Assert.Equal( "STEAMKEY123", loaded.SteamApiKey );
		Assert.Equal( "GRID", loaded.SteamGridDbApiKey );
		Assert.Equal( "en", loaded.Language );
	}

	[Fact]
	public void Load_ReadsLegacyClearKeys()
	{
		string path = Path.Combine( _Folder, "settings.json" );
		File.WriteAllText( path, """{ "SteamApiKey": "OLDKEY" }""" );

		AppSettings loaded = new JsonSettingsService( path ).Load();

		Assert.Equal( "OLDKEY", loaded.SteamApiKey );
	}

	[Fact]
	public void Load_ReturnsDefaultsOnCorruptedFile()
	{
		string path = Path.Combine( _Folder, "settings.json" );
		File.WriteAllText( path, "{ broken" );

		Assert.Equal( "fr", new JsonSettingsService( path ).Load().Language );
	}

	[Fact]
	public void Save_DoesNotMutateTheGivenSettings()
	{
		JsonSettingsService service = new JsonSettingsService( Path.Combine( _Folder, "settings.json" ) );
		AppSettings settings = new AppSettings { SteamApiKey = "KEY" };

		service.Save( settings );

		Assert.Equal( "KEY", settings.SteamApiKey );
	}
}
