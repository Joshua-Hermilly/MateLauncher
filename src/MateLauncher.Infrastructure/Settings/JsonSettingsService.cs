using System.Text.Json;
using System.Text.Json.Nodes;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Infrastructure.Security;

namespace MateLauncher.Infrastructure.Settings;

public class JsonSettingsService : ISettingsService
{
	private static readonly string[] _SecretProperties = { nameof( AppSettings.GoogleApiKey ), nameof( AppSettings.SteamApiKey ), nameof( AppSettings.SteamGridDbApiKey ), nameof( AppSettings.IgdbClientId ), nameof( AppSettings.IgdbClientSecret ) };

	private static readonly JsonSerializerOptions _WriteOptions = new JsonSerializerOptions { WriteIndented = true };

	private readonly string _FilePath;

	public JsonSettingsService( string filePath )
	{
		_FilePath = filePath;
	}

	public AppSettings Load()
	{
		if ( !File.Exists( _FilePath ) )
		{
			return new AppSettings();
		}

		try
		{
			AppSettings settings = JsonSerializer.Deserialize<AppSettings>( File.ReadAllText( _FilePath ) ) ?? new AppSettings();
			settings.GoogleApiKey = SecretProtector.Unprotect( settings.GoogleApiKey );
			settings.SteamApiKey = SecretProtector.Unprotect( settings.SteamApiKey );
			settings.SteamGridDbApiKey = SecretProtector.Unprotect( settings.SteamGridDbApiKey );
			settings.IgdbClientId = SecretProtector.Unprotect( settings.IgdbClientId );
			settings.IgdbClientSecret = SecretProtector.Unprotect( settings.IgdbClientSecret );
			return settings;
		}
		catch ( Exception exception ) when ( exception is JsonException or IOException )
		{
			return new AppSettings();
		}
	}

	public void Save( AppSettings settings )
	{
		try
		{
			JsonNode node = JsonSerializer.SerializeToNode( settings )!;

			foreach ( string property in _SecretProperties )
			{
				string? value = node[ property ]?.GetValue<string>();

				if ( !string.IsNullOrEmpty( value ) )
				{
					node[ property ] = SecretProtector.Protect( value );
				}
			}

			File.WriteAllText( _FilePath, node.ToJsonString( _WriteOptions ) );
		}
		catch ( IOException )
		{
		}
	}
}
