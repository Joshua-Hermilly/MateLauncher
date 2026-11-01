using System.Text.Json;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;

namespace MateLauncher.Providers.Epic;

public class EpicProvider : IGameProvider
{
	private static readonly string[] _AcceptedCategories = { "games", "software" };
	private static readonly string[] _CoverImageTypes = { "DieselGameBoxTall", "OfferImageTall" };

	public GamePlatform Platform => GamePlatform.Epic;

	public IReadOnlyList<Game> Detect()
	{
		List<Game> games = new List<Game>();
		string manifestsPath = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData ), "Epic", "EpicGamesLauncher", "Data", "Manifests" );

		if ( !Directory.Exists( manifestsPath ) )
		{
			return games;
		}

		Dictionary<string, string> coverUrls = ReadCatalogCovers();

		foreach ( string manifestFile in Directory.EnumerateFiles( manifestsPath, "*.item" ) )
		{
			Game? game = ReadManifest( manifestFile, coverUrls );

			if ( game is not null )
			{
				games.Add( game );
			}
		}

		return games;
	}

	private static Dictionary<string, string> ReadCatalogCovers()
	{
		Dictionary<string, string> coverUrls = new Dictionary<string, string>();
		string catalogFile = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData ), "Epic", "EpicGamesLauncher", "Data", "Catalog", "catcache.bin" );

		if ( !File.Exists( catalogFile ) )
		{
			return coverUrls;
		}

		try
		{
			using JsonDocument catalog = JsonDocument.Parse( Convert.FromBase64String( File.ReadAllText( catalogFile ) ) );

			foreach ( JsonElement item in catalog.RootElement.EnumerateArray() )
			{
				string catalogItemId = ReadString( item, "id" );
				string? coverUrl = FindCoverUrl( item );

				if ( catalogItemId.Length > 0 && coverUrl is not null )
				{
					coverUrls[ catalogItemId ] = coverUrl;
				}
			}
		}
		catch ( Exception exception ) when ( exception is JsonException or FormatException or IOException or InvalidOperationException )
		{
			return coverUrls;
		}

		return coverUrls;
	}

	private static string? FindCoverUrl( JsonElement item )
	{
		if ( !item.TryGetProperty( "keyImages", out JsonElement images ) || images.ValueKind != JsonValueKind.Array )
		{
			return null;
		}

		foreach ( string imageType in _CoverImageTypes )
		{
			foreach ( JsonElement image in images.EnumerateArray() )
			{
				if ( ReadString( image, "type" ) == imageType )
				{
					return ReadString( image, "url" );
				}
			}
		}

		return null;
	}

	private static Game? ReadManifest( string manifestFile, Dictionary<string, string> coverUrls )
	{
		try
		{
			using JsonDocument document = JsonDocument.Parse( File.ReadAllText( manifestFile ) );
			JsonElement root = document.RootElement;

			string appName = ReadString( root, "AppName" );
			string mainGameAppName = ReadString( root, "MainGameAppName" );
			string displayName = ReadString( root, "DisplayName" );

			bool isIncomplete = root.TryGetProperty( "bIsIncompleteInstall", out JsonElement incomplete ) && incomplete.ValueKind == JsonValueKind.True;
			bool isAddon = mainGameAppName.Length > 0 && !string.Equals( mainGameAppName, appName, StringComparison.OrdinalIgnoreCase );

			if ( appName.Length == 0 || displayName.Length == 0 || isIncomplete || isAddon || !HasAcceptedCategory( root ) )
			{
				return null;
			}

			string catalogNamespace = ReadString( root, "CatalogNamespace" );
			string catalogItemId = ReadString( root, "CatalogItemId" );

			return new Game
			{
				Name = displayName,
				Platform = GamePlatform.Epic,
				PlatformGameId = appName,
				InstallPath = ReadString( root, "InstallLocation" ),
				CoverImagePath = coverUrls.GetValueOrDefault( catalogItemId ),
				LaunchCommand = $"com.epicgames.launcher://apps/{catalogNamespace}%3A{catalogItemId}%3A{appName}?action=launch&silent=true"
			};
		}
		catch ( JsonException )
		{
			return null;
		}
	}

	private static bool HasAcceptedCategory( JsonElement root )
	{
		if ( !root.TryGetProperty( "AppCategories", out JsonElement categories ) || categories.ValueKind != JsonValueKind.Array )
		{
			return true;
		}

		return categories.EnumerateArray().Any( category => _AcceptedCategories.Contains( category.GetString(), StringComparer.OrdinalIgnoreCase ) );
	}

	private static string ReadString( JsonElement root, string propertyName )
	{
		return root.TryGetProperty( propertyName, out JsonElement value ) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
	}
}
