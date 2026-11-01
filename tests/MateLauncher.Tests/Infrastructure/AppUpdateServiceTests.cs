using System.Security.Cryptography;
using System.Text;
using MateLauncher.Infrastructure.Updates;

namespace MateLauncher.Tests.Infrastructure;

public class AppUpdateServiceTests : IDisposable
{
	private readonly string _Folder = Path.Combine( Path.GetTempPath(), "MateLauncherTests-" + Guid.NewGuid() );

	public AppUpdateServiceTests()
	{
		Directory.CreateDirectory( _Folder );
	}

	public void Dispose()
	{
		Directory.Delete( _Folder, true );
	}

	[Theory]
	[InlineData( "Joshua-Hermilly/MateLauncher", "Joshua-Hermilly/MateLauncher" )]
	[InlineData( "https://github.com/Joshua-Hermilly/MateLauncher/", "Joshua-Hermilly/MateLauncher" )]
	[InlineData( "github.com/owner/repo.git", "owner/repo" )]
	[InlineData( "not a repository", null )]
	[InlineData( "owner/repo/extra", null )]
	public void NormalizeRepository_AcceptsOnlyOwnerSlashName( string input, string? expected )
	{
		Assert.Equal( expected, AppUpdateService.NormalizeRepository( input ) );
	}

	[Fact]
	public void ParseChecksum_ReadsHashFromShaSumsFormat()
	{
		string hash = new string( 'a', 64 );

		Assert.Equal( hash, AppUpdateService.ParseChecksum( $"{hash}  MateLauncher-Setup-1.0.0-win-x64.exe" ) );
	}

	[Fact]
	public void ParseChecksum_ReturnsNullWhenNoHash()
	{
		Assert.Null( AppUpdateService.ParseChecksum( "no hash here" ) );
	}

	[Fact]
	public async Task MatchesChecksum_AcceptsTheRightHashRegardlessOfCase()
	{
		string path = Path.Combine( _Folder, "file.bin" );
		byte[] data = Encoding.UTF8.GetBytes( "installer content" );
		await File.WriteAllBytesAsync( path, data );
		string hash = Convert.ToHexString( SHA256.HashData( data ) );

		Assert.True( await AppUpdateService.MatchesChecksumAsync( path, hash.ToLowerInvariant() ) );
		Assert.True( await AppUpdateService.MatchesChecksumAsync( path, hash.ToUpperInvariant() ) );
	}

	[Fact]
	public async Task MatchesChecksum_RejectsTamperedFile()
	{
		string path = Path.Combine( _Folder, "file.bin" );
		await File.WriteAllTextAsync( path, "tampered" );

		Assert.False( await AppUpdateService.MatchesChecksumAsync( path, new string( '0', 64 ) ) );
	}
}
