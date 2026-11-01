using System.Net;
using System.Text.Json;

namespace MateLauncher.Infrastructure.Achievements;

public record AchievementInfo( string ApiName, string Name, string Description, bool IsUnlocked, DateTime? UnlockedAt, string? IconUrl, double? GlobalPercent );

public record AchievementResult( IReadOnlyList<AchievementInfo> Items, string? ErrorKey );

public record SteamAccountCheck( bool Success, string? PersonaName, bool IsPublic, string? ErrorKey );

public class SteamAchievementsService
{
	private static readonly HttpClient _HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds( 15 ) };

	private const string ApiRoot = "https://api.steampowered.com";

	public async Task<SteamAccountCheck> CheckAccountAsync( string apiKey, string steamId )
	{
		try
		{
			string url = $"{ApiRoot}/ISteamUser/GetPlayerSummaries/v2/?key={Uri.EscapeDataString( apiKey.Trim() )}&steamids={Uri.EscapeDataString( steamId.Trim() )}";
			using HttpResponseMessage response = await _HttpClient.GetAsync( url );

			if ( response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized )
			{
				return new SteamAccountCheck( false, null, false, "Steam_BadKey" );
			}

			response.EnsureSuccessStatusCode();
			using JsonDocument document = JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
			JsonElement players = document.RootElement.GetProperty( "response" ).GetProperty( "players" );

			if ( players.GetArrayLength() == 0 )
			{
				return new SteamAccountCheck( false, null, false, "Steam_BadId" );
			}

			JsonElement player = players[ 0 ];
			string? name = player.TryGetProperty( "personaname", out JsonElement nameElement ) ? nameElement.GetString() : null;
			bool isPublic = player.TryGetProperty( "communityvisibilitystate", out JsonElement visibility ) && visibility.GetInt32() == 3;
			return new SteamAccountCheck( true, name, isPublic, null );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException )
		{
			return new SteamAccountCheck( false, null, false, "Ach_Error" );
		}
	}

	public async Task<AchievementResult> GetAchievementsAsync( string apiKey, string steamId, string appId, string language )
	{
		string key = Uri.EscapeDataString( apiKey.Trim() );
		string steamLanguage = language == "fr" ? "french" : "english";

		try
		{
			string playerUrl = $"{ApiRoot}/ISteamUserStats/GetPlayerAchievements/v1/?key={key}&steamid={Uri.EscapeDataString( steamId.Trim() )}&appid={appId}&l={steamLanguage}";
			using HttpResponseMessage playerResponse = await _HttpClient.GetAsync( playerUrl );
			string playerBody = await playerResponse.Content.ReadAsStringAsync();

			if ( playerResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized )
			{
				return new AchievementResult( Array.Empty<AchievementInfo>(), "Ach_Private" );
			}

			if ( !playerResponse.IsSuccessStatusCode )
			{
				return new AchievementResult( Array.Empty<AchievementInfo>(), playerBody.Contains( "no stats", StringComparison.OrdinalIgnoreCase ) ? "Ach_NoStats" : "Ach_Error" );
			}

			using JsonDocument playerDocument = JsonDocument.Parse( playerBody );
			JsonElement playerStats = playerDocument.RootElement.GetProperty( "playerstats" );

			if ( !playerStats.TryGetProperty( "achievements", out JsonElement playerAchievements ) )
			{
				return new AchievementResult( Array.Empty<AchievementInfo>(), "Ach_NoStats" );
			}

			Task<Dictionary<string, ( string? Icon, string? IconGray )>> schemaTask = ReadSchemaAsync( key, appId, steamLanguage );
			Task<Dictionary<string, double>> globalTask = ReadGlobalPercentagesAsync( appId );
			Dictionary<string, ( string? Icon, string? IconGray )> schema = await schemaTask;
			Dictionary<string, double> percentages = await globalTask;

			List<AchievementInfo> items = playerAchievements.EnumerateArray().Select( achievement =>
			{
				string apiName = achievement.GetProperty( "apiname" ).GetString() ?? string.Empty;
				bool unlocked = achievement.GetProperty( "achieved" ).GetInt32() == 1;
				long unlockTime = achievement.TryGetProperty( "unlocktime", out JsonElement unlockElement ) ? unlockElement.GetInt64() : 0;
				string name = achievement.TryGetProperty( "name", out JsonElement nameElement ) ? nameElement.GetString() ?? apiName : apiName;
				string description = achievement.TryGetProperty( "description", out JsonElement descriptionElement ) ? descriptionElement.GetString() ?? string.Empty : string.Empty;
				schema.TryGetValue( apiName, out ( string? Icon, string? IconGray ) icons );

				return new AchievementInfo(
					apiName,
					name,
					description,
					unlocked,
					unlocked && unlockTime > 0 ? DateTimeOffset.FromUnixTimeSeconds( unlockTime ).LocalDateTime : null,
					unlocked ? icons.Icon : icons.IconGray ?? icons.Icon,
					percentages.TryGetValue( apiName, out double percent ) ? percent : null );
			} ).OrderByDescending( item => item.IsUnlocked ).ThenByDescending( item => item.UnlockedAt ).ThenBy( item => item.Name ).ToList();

			return new AchievementResult( items, null );
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException or InvalidOperationException )
		{
			return new AchievementResult( Array.Empty<AchievementInfo>(), "Ach_Error" );
		}
	}

	private static async Task<Dictionary<string, ( string? Icon, string? IconGray )>> ReadSchemaAsync( string escapedKey, string appId, string steamLanguage )
	{
		Dictionary<string, ( string? Icon, string? IconGray )> result = new Dictionary<string, ( string?, string? )>();

		try
		{
			string url = $"{ApiRoot}/ISteamUserStats/GetSchemaForGame/v2/?key={escapedKey}&appid={appId}&l={steamLanguage}";
			using JsonDocument document = JsonDocument.Parse( await _HttpClient.GetStringAsync( url ) );

			if ( document.RootElement.GetProperty( "game" ).TryGetProperty( "availableGameStats", out JsonElement stats ) && stats.TryGetProperty( "achievements", out JsonElement achievements ) )
			{
				foreach ( JsonElement achievement in achievements.EnumerateArray() )
				{
					string name = achievement.GetProperty( "name" ).GetString() ?? string.Empty;
					string? icon = achievement.TryGetProperty( "icon", out JsonElement iconElement ) ? iconElement.GetString() : null;
					string? iconGray = achievement.TryGetProperty( "icongray", out JsonElement grayElement ) ? grayElement.GetString() : null;
					result[ name ] = ( icon, iconGray );
				}
			}
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException )
		{
		}

		return result;
	}

	private static async Task<Dictionary<string, double>> ReadGlobalPercentagesAsync( string appId )
	{
		Dictionary<string, double> result = new Dictionary<string, double>();

		try
		{
			string url = $"{ApiRoot}/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid={appId}";
			using JsonDocument document = JsonDocument.Parse( await _HttpClient.GetStringAsync( url ) );

			foreach ( JsonElement achievement in document.RootElement.GetProperty( "achievementpercentages" ).GetProperty( "achievements" ).EnumerateArray() )
			{
				string name = achievement.GetProperty( "name" ).GetString() ?? string.Empty;
				JsonElement percent = achievement.GetProperty( "percent" );
				result[ name ] = percent.ValueKind == JsonValueKind.String ? double.Parse( percent.GetString()!, System.Globalization.CultureInfo.InvariantCulture ) : percent.GetDouble();
			}
		}
		catch ( Exception exception ) when ( exception is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException or FormatException )
		{
		}

		return result;
	}
}
