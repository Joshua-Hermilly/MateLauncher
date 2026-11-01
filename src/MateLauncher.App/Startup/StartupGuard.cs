using System.IO;

namespace MateLauncher.App.Startup;

public static class StartupGuard
{
	public static string DataFolder { get; } = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), "MateLauncher" );

	private static string CrashFlagPath => Path.Combine( DataFolder, "crash.flag" );

	public static bool WasInterrupted()
	{
		if ( !File.Exists( CrashFlagPath ) )
		{
			return false;
		}

		try
		{
			File.Delete( CrashFlagPath );
		}
		catch ( IOException )
		{
		}

		return true;
	}

	public static void MarkCrash()
	{
		try
		{
			File.WriteAllText( CrashFlagPath, DateTime.Now.ToString( "O" ) );
		}
		catch ( IOException )
		{
		}
	}
}
