using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;

namespace MateLauncher.Infrastructure.Metadata;

public class IgdbClient : IGameMetadataProvider, ICoverFinder
{
	private const string ImageRoot = "https://images.igdb.com/igdb/image/upload";

	private static readonly HttpClient _HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds( 15 ) };

	private readonly ISettingsService _SettingsService;
	private readonly string _CacheFilePath;
	private readonly SemaphoreSlim _TokenLock = new SemaphoreSlim( 1, 1 );
	private readonly Dictionary<string, GameMetadata?> _Cache = new Dictionary<string, GameMetadata?>( StringComparer.OrdinalIgnoreCase );
	private string? _AccessToken;
	private DateTime _TokenExpiresAt = DateTime.MinValue;
	private string? _TokenClientId;
	private bool _IsCacheLoaded;

	public IgdbClient( ISettingsService settingsService, string cacheFilePath )
	{
		_SettingsService = settingsService;
		_CacheFilePath = cacheFilePath;
	}

	public async Task<GameMetadata?> GetMetadataAsync( string gameName )
	{
		AppSettings settings = _SettingsService.Load();

		if ( string.IsNullOrWhiteSpace( settings.IgdbClientId ) || string.IsNullOrWhiteSpace( settings.IgdbClientSecret ) )
		{
			return null;
		}

		LoadCache();

		if ( _Cache.TryGetValue( gameName, out GameMetadata? cached ) )
		{
			return cached;
		}

		try
		{
			string body = $"search \"{EscapeQuery( gameName )}\"; fields id,name,summary,genres.name,first_release_date,total_rating,cover.image_id,screenshots.image_id; limit 1;";
			using JsonDocument document = await PostAsync( settings, "games", body );
			GameMetadata? metadata = ParseFirstGame( document.RootElement );

			if ( metadata is not null && ParseGameId( document.RootElement ) is long gameId )
			{
				metadata = metadata with { TimeToBeatSeconds = await FindTimeToBeatAsync( settings, gameId ) };
			}

			_Cache[ gameName ] = metadata;
			SaveCache();
			return metadata;
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or KeyNotFoundException )
		{
			return null;
		}
	}

	public async Task<string?> FindCoverUrlAsync( string gameName )
	{
		AppSettings settings = _SettingsService.Load();

		if ( string.IsNullOrWhiteSpace( settings.IgdbClientId ) || string.IsNullOrWhiteSpace( settings.IgdbClientSecret ) )
		{
			return null;
		}

		try
		{
			string body = $"search \"{EscapeQuery( gameName )}\"; fields cover.image_id; where cover != null; limit 1;";
			using JsonDocument document = await PostAsync( settings, "games", body );
			return ParseCoverUrl( document.RootElement );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or KeyNotFoundException )
		{
			return null;
		}
	}

	public static GameMetadata? ParseFirstGame( JsonElement root )
	{
		if ( root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 )
		{
			return null;
		}

		JsonElement game = root[ 0 ];
		string name = game.TryGetProperty( "name", out JsonElement nameElement ) ? nameElement.GetString() ?? string.Empty : string.Empty;
		string summary = game.TryGetProperty( "summary", out JsonElement summaryElement ) ? summaryElement.GetString() ?? string.Empty : string.Empty;
		List<string> genres = new List<string>();

		if ( game.TryGetProperty( "genres", out JsonElement genresElement ) && genresElement.ValueKind == JsonValueKind.Array )
		{
			genres.AddRange( genresElement.EnumerateArray().Select( genre => genre.TryGetProperty( "name", out JsonElement genreName ) ? genreName.GetString() ?? string.Empty : string.Empty ).Where( genre => genre.Length > 0 ) );
		}

		DateTime? releaseDate = game.TryGetProperty( "first_release_date", out JsonElement dateElement ) && dateElement.TryGetInt64( out long seconds )
			? DateTimeOffset.FromUnixTimeSeconds( seconds ).UtcDateTime
			: null;
		double? rating = game.TryGetProperty( "total_rating", out JsonElement ratingElement ) && ratingElement.TryGetDouble( out double value ) ? Math.Round( value, 1 ) : null;
		List<string> screenshots = new List<string>();

		if ( game.TryGetProperty( "screenshots", out JsonElement screenshotsElement ) && screenshotsElement.ValueKind == JsonValueKind.Array )
		{
			screenshots.AddRange( screenshotsElement.EnumerateArray().Select( screenshot => screenshot.TryGetProperty( "image_id", out JsonElement imageId ) ? imageId.GetString() : null ).Where( imageId => !string.IsNullOrEmpty( imageId ) ).Take( 5 ).Select( imageId => $"{ImageRoot}/t_screenshot_big/{imageId}.jpg" ) );
		}

		return new GameMetadata( name, summary, genres, releaseDate, rating, screenshots );
	}

	public static long? ParseGameId( JsonElement root )
	{
		if ( root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 )
		{
			return null;
		}

		return root[ 0 ].TryGetProperty( "id", out JsonElement id ) && id.TryGetInt64( out long value ) ? value : null;
	}

	public static int? ParseTimeToBeat( JsonElement root )
	{
		if ( root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 )
		{
			return null;
		}

		JsonElement entry = root[ 0 ];

		foreach ( string property in new[] { "normally", "hastily", "completely" } )
		{
			if ( entry.TryGetProperty( property, out JsonElement value ) && value.TryGetInt32( out int seconds ) && seconds > 0 )
			{
				return seconds;
			}
		}

		return null;
	}

	public static string? ParseCoverUrl( JsonElement root )
	{
		if ( root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0 )
		{
			return null;
		}

		if ( !root[ 0 ].TryGetProperty( "cover", out JsonElement cover ) || !cover.TryGetProperty( "image_id", out JsonElement imageId ) || string.IsNullOrEmpty( imageId.GetString() ) )
		{
			return null;
		}

		return $"{ImageRoot}/t_cover_big_2x/{imageId.GetString()}.jpg";
	}

	public static string EscapeQuery( string value )
	{
		return value.Replace( "\\", string.Empty ).Replace( "\"", string.Empty );
	}

	private async Task<int?> FindTimeToBeatAsync( AppSettings settings, long gameId )
	{
		try
		{
			using JsonDocument document = await PostAsync( settings, "game_time_to_beats", $"fields hastily,normally,completely; where game_id = {gameId}; limit 1;" );
			return ParseTimeToBeat( document.RootElement );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or InvalidOperationException or KeyNotFoundException )
		{
			return null;
		}
	}

	private async Task<JsonDocument> PostAsync( AppSettings settings, string endpoint, string body )
	{
		string token = await GetAccessTokenAsync( settings );
		using HttpRequestMessage request = new HttpRequestMessage( HttpMethod.Post, $"https://api.igdb.com/v4/{endpoint}" );
		request.Headers.Add( "Client-ID", settings.IgdbClientId.Trim() );
		request.Headers.Authorization = new AuthenticationHeaderValue( "Bearer", token );
		request.Content = new StringContent( body, Encoding.UTF8, "text/plain" );
		using HttpResponseMessage response = await _HttpClient.SendAsync( request );
		response.EnsureSuccessStatusCode();
		return JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
	}

	private async Task<string> GetAccessTokenAsync( AppSettings settings )
	{
		await _TokenLock.WaitAsync();

		try
		{
			string clientId = settings.IgdbClientId.Trim();

			if ( _AccessToken is not null && _TokenClientId == clientId && DateTime.UtcNow < _TokenExpiresAt )
			{
				return _AccessToken;
			}

			string url = $"https://id.twitch.tv/oauth2/token?client_id={Uri.EscapeDataString( clientId )}&client_secret={Uri.EscapeDataString( settings.IgdbClientSecret.Trim() )}&grant_type=client_credentials";
			using HttpResponseMessage response = await _HttpClient.PostAsync( url, null );
			response.EnsureSuccessStatusCode();
			using JsonDocument document = JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
			_AccessToken = document.RootElement.GetProperty( "access_token" ).GetString() ?? throw new InvalidOperationException( "No access token" );
			int lifetime = document.RootElement.TryGetProperty( "expires_in", out JsonElement expires ) ? expires.GetInt32() : 3600;
			_TokenExpiresAt = DateTime.UtcNow.AddSeconds( Math.Max( 60, lifetime - 60 ) );
			_TokenClientId = clientId;
			return _AccessToken;
		}
		finally
		{
			_TokenLock.Release();
		}
	}

	private void LoadCache()
	{
		if ( _IsCacheLoaded )
		{
			return;
		}

		_IsCacheLoaded = true;

		try
		{
			if ( File.Exists( _CacheFilePath ) )
			{
				Dictionary<string, GameMetadata?>? stored = JsonSerializer.Deserialize<Dictionary<string, GameMetadata?>>( File.ReadAllText( _CacheFilePath ) );

				foreach ( KeyValuePair<string, GameMetadata?> entry in stored ?? new Dictionary<string, GameMetadata?>() )
				{
					_Cache[ entry.Key ] = entry.Value;
				}
			}
		}
		catch ( Exception exception ) when ( exception is JsonException or IOException )
		{
		}
	}

	private void SaveCache()
	{
		try
		{
			Directory.CreateDirectory( Path.GetDirectoryName( _CacheFilePath )! );
			File.WriteAllText( _CacheFilePath, JsonSerializer.Serialize( _Cache ) );
		}
		catch ( IOException )
		{
		}
	}
}
