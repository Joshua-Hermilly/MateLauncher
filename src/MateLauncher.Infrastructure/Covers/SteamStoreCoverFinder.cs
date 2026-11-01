using System.Text.Json;
using MateLauncher.Core.Abstractions;

namespace MateLauncher.Infrastructure.Covers;

public class SteamStoreCoverFinder : ICoverFinder
{
	private static readonly HttpClient _HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds( 15 ) };

	public async Task<string?> FindCoverUrlAsync( string gameName )
	{
		try
		{
			string searchUrl = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString( gameName )}&l=english&cc=US";
			using JsonDocument document = JsonDocument.Parse( await _HttpClient.GetStringAsync( searchUrl ) );

			if ( !document.RootElement.TryGetProperty( "items", out JsonElement items ) )
			{
				return null;
			}

			string normalizedName = Normalize( gameName );
			List<( int Id, string Name )> results = items.EnumerateArray()
				.Select( item => ( Id: item.GetProperty( "id" ).GetInt32(), Name: item.GetProperty( "name" ).GetString() ?? string.Empty ) )
				.ToList();

			( int Id, string Name ) match = results.FirstOrDefault( result => Normalize( result.Name ) == normalizedName );

			if ( match.Id == 0 )
			{
				match = results.FirstOrDefault( result => IsCloseMatch( Normalize( result.Name ), normalizedName ) );
			}

			return match.Id == 0 ? null : await FindExistingImageAsync( match.Id );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or KeyNotFoundException )
		{
			return null;
		}
	}

	private static async Task<string?> FindExistingImageAsync( int appId )
	{
		string[] imageNames = { "library_600x900.jpg", "header.jpg" };

		foreach ( string imageName in imageNames )
		{
			string imageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/{imageName}";
			using HttpRequestMessage request = new HttpRequestMessage( HttpMethod.Head, imageUrl );
			using HttpResponseMessage response = await _HttpClient.SendAsync( request );

			if ( response.IsSuccessStatusCode )
			{
				return imageUrl;
			}
		}

		return null;
	}

	private static bool IsCloseMatch( string candidate, string wanted )
	{
		return wanted.Length >= 4 && ( candidate.StartsWith( wanted ) || wanted.StartsWith( candidate ) );
	}

	private static string Normalize( string value )
	{
		return new string( value.Where( char.IsLetterOrDigit ).Select( char.ToLowerInvariant ).ToArray() );
	}
}
