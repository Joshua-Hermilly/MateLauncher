using System.Text.Json;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;

namespace MateLauncher.Infrastructure.Settings;

public class JsonSettingsService : ISettingsService
{
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
			return JsonSerializer.Deserialize<AppSettings>( File.ReadAllText( _FilePath ) ) ?? new AppSettings();
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
			File.WriteAllText( _FilePath, JsonSerializer.Serialize( settings, new JsonSerializerOptions { WriteIndented = true } ) );
		}
		catch ( IOException )
		{
		}
	}
}
