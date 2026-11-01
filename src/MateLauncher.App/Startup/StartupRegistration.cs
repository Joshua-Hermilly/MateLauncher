using Microsoft.Win32;

namespace MateLauncher.App.Startup;

public static class StartupRegistration
{
	private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string ValueName = "MateLauncher";

	public static bool IsEnabled()
	{
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey( RunKeyPath );
		return key?.GetValue( ValueName ) is not null;
	}

	public static void Apply( bool enabled )
	{
		using RegistryKey key = Registry.CurrentUser.CreateSubKey( RunKeyPath );

		if ( !enabled )
		{
			key.DeleteValue( ValueName, false );
			return;
		}

		string? executable = Environment.ProcessPath;

		if ( executable is not null )
		{
			key.SetValue( ValueName, $"\"{executable}\" --minimized" );
		}
	}
}
