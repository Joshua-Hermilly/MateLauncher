using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;

namespace MateLauncher.Infrastructure.Plugins;

public class PluginGameProvider : IGameProvider
{
	private readonly string _PluginsFolder;
	private IReadOnlyList<LoadedPlugin>? _Plugins;

	public PluginGameProvider( string pluginsFolder )
	{
		_PluginsFolder = pluginsFolder;
	}

	public GamePlatform Platform => GamePlatform.Plugin;

	public IReadOnlyList<Game> Detect()
	{
		if ( _Plugins is null )
		{
			PluginLoadResult result = PluginLoader.Load( _PluginsFolder );
			_Plugins = result.Plugins;

			foreach ( string error in result.Errors )
			{
				DebugLog.Write( $"Plugin load failed: {error}" );
			}
		}

		List<Game> games = new List<Game>();

		foreach ( LoadedPlugin plugin in _Plugins )
		{
			try
			{
				games.AddRange( plugin.Provider.Detect().Where( game => !string.IsNullOrWhiteSpace( game.Name ) ).Select( game => Adapt( plugin.Name, game ) ) );
			}
			catch ( Exception exception )
			{
				DebugLog.Write( $"Plugin {plugin.Name}: detection failed{Environment.NewLine}{exception}" );
			}
		}

		return games;
	}

	public static Game Adapt( string pluginName, Game game )
	{
		game.Platform = GamePlatform.Plugin;
		string prefix = pluginName + ":";

		if ( !game.PlatformGameId.StartsWith( prefix, StringComparison.Ordinal ) )
		{
			game.PlatformGameId = prefix + ( string.IsNullOrEmpty( game.PlatformGameId ) ? game.Name : game.PlatformGameId );
		}

		return game;
	}
}
