using System.Reflection;
using System.Runtime.Loader;

namespace MateLauncher.Infrastructure.Plugins;

internal class PluginLoadContext : AssemblyLoadContext
{
	private readonly AssemblyDependencyResolver _Resolver;

	public PluginLoadContext( string pluginPath ) : base( Path.GetFileNameWithoutExtension( pluginPath ), false )
	{
		_Resolver = new AssemblyDependencyResolver( pluginPath );
	}

	protected override Assembly? Load( AssemblyName assemblyName )
	{
		if ( assemblyName.Name == "MateLauncher.Core" )
		{
			return null;
		}

		string? path = _Resolver.ResolveAssemblyToPath( assemblyName );
		return path is null ? null : LoadFromAssemblyPath( path );
	}
}
