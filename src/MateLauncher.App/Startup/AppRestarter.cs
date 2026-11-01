using System.Diagnostics;
using System.Windows;

namespace MateLauncher.App.Startup;

public static class AppRestarter
{
	public static void Restart( params string[] arguments )
	{
		string? executable = Environment.ProcessPath;

		if ( executable is null )
		{
			return;
		}

		Process.Start( new ProcessStartInfo( executable, string.Join( ' ', arguments ) ) { UseShellExecute = false } );
		Application.Current.Shutdown();
	}
}
