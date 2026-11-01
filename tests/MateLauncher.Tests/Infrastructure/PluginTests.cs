using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Infrastructure.Plugins;

namespace MateLauncher.Tests.Infrastructure;

public class SamplePluginProvider : IGameProvider
{
	public GamePlatform Platform => GamePlatform.Manual;

	public IReadOnlyList<Game> Detect()
	{
		return new[] { new Game { Name = "Sample Game", PlatformGameId = "1" }, new Game { Name = " ", PlatformGameId = "2" } };
	}
}

public class PluginTests : IDisposable
{
	private readonly string _Folder = Path.Combine( Path.GetTempPath(), "MateLauncherTests-" + Guid.NewGuid() );

	public PluginTests()
	{
		Directory.CreateDirectory( _Folder );
	}

	public void Dispose()
	{
		Directory.Delete( _Folder, true );
	}

	[Fact]
	public void Adapt_ForcesPluginPlatformAndPrefixesIdentifier()
	{
		Game game = PluginGameProvider.Adapt( "MyPlugin", new Game { Name = "Game", PlatformGameId = "42", Platform = GamePlatform.Steam } );

		Assert.Equal( GamePlatform.Plugin, game.Platform );
		Assert.Equal( "MyPlugin:42", game.PlatformGameId );
	}

	[Fact]
	public void Adapt_DoesNotPrefixTwice()
	{
		Game game = PluginGameProvider.Adapt( "MyPlugin", new Game { Name = "Game", PlatformGameId = "MyPlugin:42" } );

		Assert.Equal( "MyPlugin:42", game.PlatformGameId );
	}

	[Fact]
	public void Adapt_FallsBackToNameWhenIdentifierIsEmpty()
	{
		Game game = PluginGameProvider.Adapt( "MyPlugin", new Game { Name = "Game" } );

		Assert.Equal( "MyPlugin:Game", game.PlatformGameId );
	}

	[Fact]
	public void CreateProviders_FindsPublicProvidersWithDefaultConstructor()
	{
		List<LoadedPlugin> plugins = PluginLoader.CreateProviders( typeof( SamplePluginProvider ).Assembly ).ToList();

		Assert.Contains( plugins, plugin => plugin.Provider is SamplePluginProvider );
	}

	[Fact]
	public void FindAssemblies_ReadsFlatAndFolderLayouts()
	{
		File.WriteAllText( Path.Combine( _Folder, "Flat.dll" ), string.Empty );
		Directory.CreateDirectory( Path.Combine( _Folder, "Nested" ) );
		File.WriteAllText( Path.Combine( _Folder, "Nested", "Nested.dll" ), string.Empty );
		File.WriteAllText( Path.Combine( _Folder, "Nested", "Dependency.dll" ), string.Empty );

		List<string> found = PluginLoader.FindAssemblies( _Folder ).Select( Path.GetFileName ).OrderBy( name => name ).ToList()!;

		Assert.Equal( new[] { "Flat.dll", "Nested.dll" }, found );
	}

	[Fact]
	public void Load_ReportsInvalidAssembliesWithoutThrowing()
	{
		File.WriteAllText( Path.Combine( _Folder, "Broken.dll" ), "not an assembly" );

		PluginLoadResult result = PluginLoader.Load( _Folder );

		Assert.Empty( result.Plugins );
		Assert.Single( result.Errors );
	}

	[Fact]
	public void Load_ReturnsNothingWhenFolderDoesNotExist()
	{
		PluginLoadResult result = PluginLoader.Load( Path.Combine( _Folder, "missing" ) );

		Assert.Empty( result.Plugins );
		Assert.Empty( result.Errors );
	}

	[Fact]
	public void Detect_SkipsNamelessGamesAndAdaptsTheRest()
	{
		PluginGameProvider provider = new PluginGameProvider( Path.Combine( _Folder, "missing" ) );
		List<Game> games = PluginLoader.CreateProviders( typeof( SamplePluginProvider ).Assembly )
			.Where( plugin => plugin.Provider is SamplePluginProvider )
			.SelectMany( plugin => plugin.Provider.Detect().Where( game => !string.IsNullOrWhiteSpace( game.Name ) ).Select( game => PluginGameProvider.Adapt( plugin.Name, game ) ) )
			.ToList();

		Assert.Empty( provider.Detect() );
		Assert.Single( games );
		Assert.Equal( GamePlatform.Plugin, games[ 0 ].Platform );
		Assert.Equal( "MateLauncher.Tests:1", games[ 0 ].PlatformGameId );
	}
}
