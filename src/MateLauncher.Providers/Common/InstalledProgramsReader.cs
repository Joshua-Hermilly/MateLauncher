using Microsoft.Win32;

namespace MateLauncher.Providers.Common;

public record InstalledProgram( string KeyName, string DisplayName, string Publisher, string InstallLocation );

public static class InstalledProgramsReader
{
	public static IReadOnlyList<InstalledProgram> ReadAll()
	{
		List<InstalledProgram> programs = new List<InstalledProgram>();

		if ( !OperatingSystem.IsWindows() )
		{
			return programs;
		}

		( RegistryHive Hive, RegistryView View )[] roots =
		{
			( RegistryHive.LocalMachine, RegistryView.Registry64 ),
			( RegistryHive.LocalMachine, RegistryView.Registry32 ),
			( RegistryHive.CurrentUser, RegistryView.Registry64 )
		};

		foreach ( ( RegistryHive hive, RegistryView view ) in roots )
		{
			using RegistryKey baseKey = RegistryKey.OpenBaseKey( hive, view );
			using RegistryKey? uninstallKey = baseKey.OpenSubKey( @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" );

			if ( uninstallKey is null )
			{
				continue;
			}

			foreach ( string keyName in uninstallKey.GetSubKeyNames() )
			{
				using RegistryKey? programKey = uninstallKey.OpenSubKey( keyName );
				string displayName = programKey?.GetValue( "DisplayName" ) as string ?? string.Empty;

				if ( displayName.Length == 0 )
				{
					continue;
				}

				programs.Add( new InstalledProgram(
					keyName,
					displayName,
					programKey?.GetValue( "Publisher" ) as string ?? string.Empty,
					( programKey?.GetValue( "InstallLocation" ) as string ?? string.Empty ).Trim( '"' ).Trim() ) );
			}
		}

		return programs;
	}
}
