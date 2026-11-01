using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MateLauncher.Infrastructure.Updates;

public enum UpdateCheckStatus
{
	UpToDate,
	Available,
	InvalidRepository,
	NotFound,
	NoInstaller,
	Failed
}

public record AppUpdateInfo( string Version, string Notes, string PageUrl, string DownloadUrl, string FileName, long Size );

public record UpdateCheckResult( UpdateCheckStatus Status, AppUpdateInfo? Update );

public static partial class AppUpdateService
{
	public const string Repository = "Joshua-Hermilly/MateLauncher";

	private static readonly HttpClient _HttpClient = CreateClient();

	[GeneratedRegex( @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$" )]
	private static partial Regex RepositoryPattern();

	[GeneratedRegex( @"^MateLauncher-Setup-.*\.exe$", RegexOptions.IgnoreCase )]
	private static partial Regex InstallerPattern();

	public static string? NormalizeRepository( string input )
	{
		string value = input.Trim().TrimEnd( '/' );

		foreach ( string prefix in new[] { "https://github.com/", "http://github.com/", "github.com/" } )
		{
			if ( value.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
			{
				value = value[ prefix.Length.. ];
				break;
			}
		}

		if ( value.EndsWith( ".git", StringComparison.OrdinalIgnoreCase ) )
		{
			value = value[ ..^4 ];
		}

		return RepositoryPattern().IsMatch( value ) ? value : null;
	}

	public static async Task<UpdateCheckResult> CheckAsync( string repositoryInput, string currentVersion )
	{
		string? repository = NormalizeRepository( repositoryInput );

		if ( repository is null )
		{
			return new UpdateCheckResult( UpdateCheckStatus.InvalidRepository, null );
		}

		try
		{
			using HttpResponseMessage response = await _HttpClient.GetAsync( $"https://api.github.com/repos/{repository}/releases/latest" );

			if ( response.StatusCode == HttpStatusCode.NotFound )
			{
				return new UpdateCheckResult( UpdateCheckStatus.NotFound, null );
			}

			response.EnsureSuccessStatusCode();
			using JsonDocument document = JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
			JsonElement root = document.RootElement;
			string tag = root.GetProperty( "tag_name" ).GetString() ?? string.Empty;

			if ( !TryParseVersion( tag, out Version? latest ) || !TryParseVersion( currentVersion, out Version? current ) || latest <= current )
			{
				return new UpdateCheckResult( UpdateCheckStatus.UpToDate, null );
			}

			foreach ( JsonElement asset in root.GetProperty( "assets" ).EnumerateArray() )
			{
				string fileName = asset.GetProperty( "name" ).GetString() ?? string.Empty;
				string downloadUrl = asset.GetProperty( "browser_download_url" ).GetString() ?? string.Empty;

				if ( InstallerPattern().IsMatch( fileName ) && IsTrustedDownload( downloadUrl ) )
				{
					string notes = root.TryGetProperty( "body", out JsonElement body ) ? body.GetString() ?? string.Empty : string.Empty;
					string pageUrl = root.TryGetProperty( "html_url", out JsonElement page ) ? page.GetString() ?? string.Empty : string.Empty;
					long size = asset.TryGetProperty( "size", out JsonElement sizeElement ) ? sizeElement.GetInt64() : 0;
					return new UpdateCheckResult( UpdateCheckStatus.Available, new AppUpdateInfo( latest!.ToString(), notes, pageUrl, downloadUrl, fileName, size ) );
				}
			}

			return new UpdateCheckResult( UpdateCheckStatus.NoInstaller, null );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException or InvalidOperationException )
		{
			return new UpdateCheckResult( UpdateCheckStatus.Failed, null );
		}
	}

	public static async Task<string> DownloadAsync( AppUpdateInfo update, IProgress<double> progress )
	{
		if ( !IsTrustedDownload( update.DownloadUrl ) )
		{
			throw new InvalidOperationException( "Untrusted download address" );
		}

		string folder = Path.Combine( Path.GetTempPath(), "MateLauncher-Update" );
		Directory.CreateDirectory( folder );
		string target = Path.Combine( folder, Path.GetFileName( update.FileName ) );

		using HttpResponseMessage response = await _HttpClient.GetAsync( update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead );
		response.EnsureSuccessStatusCode();
		long totalBytes = response.Content.Headers.ContentLength ?? update.Size;
		await using Stream source = await response.Content.ReadAsStreamAsync();
		await using FileStream destination = File.Create( target );
		byte[] buffer = new byte[ 81920 ];
		long downloaded = 0;
		int read;

		while ( ( read = await source.ReadAsync( buffer ) ) > 0 )
		{
			await destination.WriteAsync( buffer.AsMemory( 0, read ) );
			downloaded += read;

			if ( totalBytes > 0 )
			{
				progress.Report( Math.Min( 1, ( double ) downloaded / totalBytes ) );
			}
		}

		return target;
	}

	private static bool IsTrustedDownload( string url )
	{
		if ( !Uri.TryCreate( url, UriKind.Absolute, out Uri? uri ) || uri.Scheme != Uri.UriSchemeHttps )
		{
			return false;
		}

		return uri.Host.Equals( "github.com", StringComparison.OrdinalIgnoreCase ) || uri.Host.EndsWith( ".githubusercontent.com", StringComparison.OrdinalIgnoreCase );
	}

	private static bool TryParseVersion( string text, out Version? version )
	{
		string value = text.Trim().TrimStart( 'v', 'V' ).Split( '+' )[ 0 ].Split( '-' )[ 0 ];
		return Version.TryParse( value, out version );
	}

	private static HttpClient CreateClient()
	{
		HttpClient client = new HttpClient { Timeout = TimeSpan.FromMinutes( 10 ) };
		client.DefaultRequestHeaders.UserAgent.Add( new ProductInfoHeaderValue( "MateLauncher", "1.0" ) );
		client.DefaultRequestHeaders.Accept.ParseAdd( "*/*" );
		return client;
	}
}
