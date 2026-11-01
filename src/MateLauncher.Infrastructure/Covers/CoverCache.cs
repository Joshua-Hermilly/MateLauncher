using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;

namespace MateLauncher.Infrastructure.Covers;

public class CoverCache : ICoverCache
{
	private static readonly string[] _ImageExtensions = { ".jpg", ".jpeg", ".png" };
	private static readonly HttpClient _HttpClient = CreateHttpClient();

	private readonly string _CoversFolder;

	public CoverCache( string coversFolder )
	{
		_CoversFolder = coversFolder;
	}

	private static HttpClient CreateHttpClient()
	{
		HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds( 30 ) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd( "Mozilla/5.0 (Windows NT 10.0; Win64; x64) MateLauncher/0.2" );
		return client;
	}

	public async Task<string?> ImportAsync( Game game, string sourceFile )
	{
		try
		{
			string extension = Path.GetExtension( sourceFile ).ToLowerInvariant();
			string filePath = Path.Combine( _CoversFolder, $"{game.Id}{( _ImageExtensions.Contains( extension ) ? extension : ".jpg" )}" );

			Directory.CreateDirectory( _CoversFolder );
			await using FileStream source = File.OpenRead( sourceFile );
			await using FileStream destination = File.Create( filePath );
			await source.CopyToAsync( destination );
			return filePath;
		}
		catch ( IOException )
		{
			return null;
		}
	}

	public async Task<string?> CacheAsync( Game game )
	{
		if ( !Game.IsRemoteCover( game.CoverImagePath ) )
		{
			return null;
		}

		try
		{
			Uri uri = new Uri( game.CoverImagePath! );
			byte[] content = await _HttpClient.GetByteArrayAsync( uri );

			string extension = Path.GetExtension( uri.AbsolutePath ).ToLowerInvariant();
			string filePath = Path.Combine( _CoversFolder, $"{game.Id}{( _ImageExtensions.Contains( extension ) ? extension : ".jpg" )}" );

			Directory.CreateDirectory( _CoversFolder );
			await File.WriteAllBytesAsync( filePath, content );
			return filePath;
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or IOException or UriFormatException or TaskCanceledException )
		{
			return null;
		}
	}
}
