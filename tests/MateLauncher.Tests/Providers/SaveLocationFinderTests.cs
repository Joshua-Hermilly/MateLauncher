using MateLauncher.Providers.Common;
using MateLauncher.Providers.Saves;

namespace MateLauncher.Tests.Providers;

public class SaveLocationFinderTests : IDisposable
{
	private readonly string _Root = Path.Combine( Path.GetTempPath(), "MateLauncherTests-" + Guid.NewGuid() );

	public SaveLocationFinderTests()
	{
		Directory.CreateDirectory( _Root );
	}

	public void Dispose()
	{
		Directory.Delete( _Root, true );
	}

	private string CreateFile( string relativeFolder, string fileName, int size )
	{
		string folder = Path.Combine( _Root, relativeFolder );
		Directory.CreateDirectory( folder );
		string file = Path.Combine( folder, fileName );
		File.WriteAllBytes( file, new byte[ size ] );
		return folder;
	}

	private List<SaveLocation> Search( string gameName, string? installFolder = null )
	{
		return SaveLocationFinder.FindInRoots( new[] { new SaveSearchRoot( _Root, "Test" ) }, gameName, installFolder ).ToList();
	}

	[Theory]
	[InlineData( "Portal 2", "Portal 2", true )]
	[InlineData( "portal2", "Portal 2", true )]
	[InlineData( "The Witcher 3", "The Witcher 3: Wild Hunt", true )]
	[InlineData( "Cyberpunk 2077", "Cyberpunk 2077", true )]
	[InlineData( "Microsoft", "Portal 2", false )]
	[InlineData( "Hi", "Hi", false )]
	[InlineData( "Portal", "Portal 2", true )]
	public void IsMatch_UsesNormalizedNames( string folder, string game, bool expected )
	{
		Assert.Equal( expected, SaveLocationFinder.IsMatch( folder, ExecutableFinder.Normalize( game ), string.Empty ) );
	}

	[Fact]
	public void IsMatch_AcceptsTheInstallFolderName()
	{
		Assert.True( SaveLocationFinder.IsMatch( "Portal2", ExecutableFinder.Normalize( "Portal Two" ), ExecutableFinder.Normalize( "Portal2" ) ) );
	}

	[Fact]
	public void FindInRoots_FindsFoldersAtDepthOneAndTwo()
	{
		CreateFile( "Cyberpunk 2077", "save.dat", 100 );
		CreateFile( Path.Combine( "CD Projekt Red", "The Witcher 3" ), "slot1.sav", 250 );

		Assert.Single( Search( "Cyberpunk 2077" ) );
		Assert.Single( Search( "The Witcher 3: Wild Hunt" ) );
	}

	[Fact]
	public void FindInRoots_MeasuresSizeAndLastWrite()
	{
		CreateFile( "Cyberpunk 2077", "a.sav", 100 );
		CreateFile( Path.Combine( "Cyberpunk 2077", "sub" ), "b.sav", 300 );

		SaveLocation location = Search( "Cyberpunk 2077" ).Single();

		Assert.Equal( 400, location.SizeBytes );
		Assert.NotNull( location.LastWriteTime );
		Assert.Equal( "Test", location.Source );
	}

	[Fact]
	public void FindInRoots_IgnoresNoiseFoldersAndDoesNotDescendIntoMatches()
	{
		CreateFile( Path.Combine( "Microsoft", "Cyberpunk 2077" ), "x.dat", 10 );
		CreateFile( Path.Combine( "Cyberpunk 2077", "Cyberpunk 2077" ), "y.dat", 10 );

		List<SaveLocation> found = Search( "Cyberpunk 2077" );

		Assert.Single( found );
		Assert.EndsWith( "Cyberpunk 2077", found[ 0 ].Path );
	}

	[Fact]
	public void FindInRoots_ReturnsNothingForMissingRootsOrEmptyNames()
	{
		Assert.Empty( SaveLocationFinder.FindInRoots( new[] { new SaveSearchRoot( Path.Combine( _Root, "absent" ), "x" ) }, "Portal 2", null ) );
		Assert.Empty( Search( "!!!" ) );
	}
}
