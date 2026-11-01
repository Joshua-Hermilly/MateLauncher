using System.Reflection;
using MateLauncher.Core.Abstractions;

namespace MateLauncher.Infrastructure.Plugins;

public record LoadedPlugin( string Name, IGameProvider Provider );

public record PluginLoadResult( IReadOnlyList<LoadedPlugin> Plugins, IReadOnlyList<string> Errors );

public static class PluginLoader
{
	public static PluginLoadResult Load( string pluginsFolder )
	{
		List<LoadedPlugin> plugins = new List<LoadedPlugin>();
		List<string> errors = new List<string>();

		if ( !Directory.Exists( pluginsFolder ) )
		{
			return new PluginLoadResult( plugins, errors );
		}

		foreach ( string assemblyPath in FindAssemblies( pluginsFolder ) )
		{
			try
			{
				PluginLoadContext context = new PluginLoadContext( assemblyPath );
				Assembly assembly = context.LoadFromAssemblyPath( assemblyPath );
				plugins.AddRange( CreateProviders( assembly ) );
			}
			catch ( Exception exception ) when ( exception is BadImageFormatException or FileLoadException or FileNotFoundException or ReflectionTypeLoadException or TypeLoadException or InvalidOperationException or MissingMethodException or TargetInvocationException )
			{
				errors.Add( $"{Path.GetFileName( assemblyPath )}: {exception.Message}" );
			}
		}

		return new PluginLoadResult( plugins, errors );
	}

	public static IEnumerable<string> FindAssemblies( string pluginsFolder )
	{
		foreach ( string file in Directory.GetFiles( pluginsFolder, "*.dll", SearchOption.TopDirectoryOnly ) )
		{
			yield return file;
		}

		foreach ( string folder in Directory.GetDirectories( pluginsFolder ) )
		{
			string main = Path.Combine( folder, Path.GetFileName( folder ) + ".dll" );

			if ( File.Exists( main ) )
			{
				yield return main;
			}
		}
	}

	public static IEnumerable<LoadedPlugin> CreateProviders( Assembly assembly )
	{
		string pluginName = assembly.GetName().Name ?? "plugin";

		foreach ( Type type in assembly.GetTypes().Where( IsProviderType ) )
		{
			IGameProvider provider = ( IGameProvider ) Activator.CreateInstance( type )!;
			yield return new LoadedPlugin( pluginName, provider );
		}
	}

	private static bool IsProviderType( Type type )
	{
		return type is { IsAbstract: false, IsInterface: false, IsPublic: true } && typeof( IGameProvider ).IsAssignableFrom( type ) && type.GetConstructor( Type.EmptyTypes ) is not null;
	}
}
