using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using Microsoft.Win32;

namespace MateLauncher.Installer;

public record InstallOptions( string Directory, bool DesktopShortcut, bool StartMenuShortcut );

public static class InstallerService
{
	public const string ApplicationExecutable = "MateLauncher.App.exe";
	public const string SetupExecutable = "MateLauncher.Setup.exe";
	public const string ProductName = "MateLauncher";

	private const string RuntimeDownloadUrl = "https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe";
	private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MateLauncher";

	public static string DefaultDirectory => Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ), "Programs", ProductName );

	public static string DataDirectory => Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ), ProductName );

	private static string DesktopShortcutPath => Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.DesktopDirectory ), $"{ProductName}.lnk" );

	private static string StartMenuShortcutPath => Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.Programs ), $"{ProductName}.lnk" );

	public static bool HasPayload => Assembly.GetExecutingAssembly().GetManifestResourceStream( "payload.zip" ) is not null;

	public static bool IsDesktopRuntimeInstalled()
	{
		string[] programFilesFolders =
		{
			Environment.GetFolderPath( Environment.SpecialFolder.ProgramFiles ),
			Environment.GetFolderPath( Environment.SpecialFolder.ProgramFilesX86 )
		};

		return programFilesFolders
			.Select( folder => Path.Combine( folder, "dotnet", "shared", "Microsoft.WindowsDesktop.App" ) )
			.Where( Directory.Exists )
			.SelectMany( Directory.EnumerateDirectories )
			.Any( versionFolder => Path.GetFileName( versionFolder ).StartsWith( "9.", StringComparison.Ordinal ) );
	}

	public static async Task InstallDesktopRuntimeAsync( IProgress<double> progress )
	{
		string installerPath = Path.Combine( Path.GetTempPath(), "windowsdesktop-runtime-9-win-x64.exe" );

		using ( HttpClient client = new HttpClient { Timeout = TimeSpan.FromMinutes( 15 ) } )
		using ( HttpResponseMessage response = await client.GetAsync( RuntimeDownloadUrl, HttpCompletionOption.ResponseHeadersRead ) )
		{
			response.EnsureSuccessStatusCode();
			long? totalBytes = response.Content.Headers.ContentLength;
			await using Stream source = await response.Content.ReadAsStreamAsync();
			await using FileStream target = File.Create( installerPath );
			byte[] buffer = new byte[ 81920 ];
			long downloaded = 0;
			int read;

			while ( ( read = await source.ReadAsync( buffer ) ) > 0 )
			{
				await target.WriteAsync( buffer.AsMemory( 0, read ) );
				downloaded += read;

				if ( totalBytes is > 0 )
				{
					progress.Report( downloaded * 0.7 / totalBytes.Value );
				}
			}
		}

		progress.Report( 0.7 );

		using Process? process = Process.Start( new ProcessStartInfo( installerPath, "/install /quiet /norestart" ) { UseShellExecute = true, Verb = "runas" } );

		if ( process is null )
		{
			throw new InvalidOperationException( "Runtime installer did not start" );
		}

		await process.WaitForExitAsync();

		if ( process.ExitCode is not ( 0 or 1638 or 3010 ) )
		{
			throw new InvalidOperationException( $".NET runtime setup failed (code {process.ExitCode})" );
		}

		progress.Report( 1 );
		File.Delete( installerPath );
	}

	public static string? FindInstalledDirectory()	{
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey( UninstallKeyPath );
		return key?.GetValue( "InstallLocation" ) as string;
	}

	public static bool IsApplicationRunning( string directory )
	{
		string executablePath = Path.GetFullPath( Path.Combine( directory, ApplicationExecutable ) );

		foreach ( Process process in Process.GetProcessesByName( Path.GetFileNameWithoutExtension( ApplicationExecutable ) ) )
		{
			try
			{
				if ( string.Equals( process.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase ) )
				{
					return true;
				}
			}
			catch ( Exception exception ) when ( exception is InvalidOperationException or System.ComponentModel.Win32Exception )
			{
			}
			finally
			{
				process.Dispose();
			}
		}

		return false;
	}

	public static void Install( InstallOptions options, IProgress<double> progress )
	{
		using Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream( "payload.zip" ) ?? throw new InvalidOperationException( "Payload missing" );
		using ZipArchive archive = new ZipArchive( payload, ZipArchiveMode.Read );

		System.IO.Directory.CreateDirectory( options.Directory );
		string rootPath = Path.GetFullPath( options.Directory );
		long totalBytes = Math.Max( 1, archive.Entries.Sum( entry => entry.Length ) );
		long writtenBytes = 0;
		long installedBytes = 0;

		foreach ( ZipArchiveEntry entry in archive.Entries )
		{
			string targetPath = Path.GetFullPath( Path.Combine( rootPath, entry.FullName ) );

			if ( !targetPath.StartsWith( rootPath, StringComparison.OrdinalIgnoreCase ) )
			{
				continue;
			}

			if ( string.IsNullOrEmpty( entry.Name ) )
			{
				System.IO.Directory.CreateDirectory( targetPath );
				continue;
			}

			System.IO.Directory.CreateDirectory( Path.GetDirectoryName( targetPath )! );
			entry.ExtractToFile( targetPath, true );
			writtenBytes += entry.Length;
			installedBytes += entry.Length;
			progress.Report( writtenBytes * 0.9 / totalBytes );
		}

		CopySetup( rootPath );
		string applicationPath = Path.Combine( rootPath, ApplicationExecutable );

		if ( options.DesktopShortcut )
		{
			CreateShortcut( DesktopShortcutPath, applicationPath, rootPath );
		}

		if ( options.StartMenuShortcut )
		{
			CreateShortcut( StartMenuShortcutPath, applicationPath, rootPath );
		}

		RegisterUninstall( rootPath, applicationPath, installedBytes );
		progress.Report( 1 );
	}

	public static void Uninstall( string directory, bool deleteUserData )
	{
		DeleteFileIfExists( DesktopShortcutPath );
		DeleteFileIfExists( StartMenuShortcutPath );
		Registry.CurrentUser.DeleteSubKeyTree( UninstallKeyPath, false );

		if ( deleteUserData && System.IO.Directory.Exists( DataDirectory ) )
		{
			System.IO.Directory.Delete( DataDirectory, true );
		}

		string command = $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{directory}\"";
		Process.Start( new ProcessStartInfo( "cmd.exe", command ) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden } );
	}

	public static void LaunchApplication( string directory, bool safeMode )
	{
		string applicationPath = Path.Combine( directory, ApplicationExecutable );
		Process.Start( new ProcessStartInfo( applicationPath, safeMode ? "--safe" : string.Empty ) { UseShellExecute = true, WorkingDirectory = directory } );
	}

	private static void CopySetup( string directory )
	{
		string? currentPath = Environment.ProcessPath;
		string targetPath = Path.Combine( directory, SetupExecutable );

		if ( currentPath is null || string.Equals( Path.GetFullPath( currentPath ), Path.GetFullPath( targetPath ), StringComparison.OrdinalIgnoreCase ) )
		{
			return;
		}

		File.Copy( currentPath, targetPath, true );
	}

	private static void CreateShortcut( string shortcutPath, string targetPath, string workingDirectory )
	{
		Type? shellType = Type.GetTypeFromProgID( "WScript.Shell" );

		if ( shellType is null )
		{
			return;
		}

		dynamic shell = Activator.CreateInstance( shellType )!;
		dynamic shortcut = shell.CreateShortcut( shortcutPath );
		shortcut.TargetPath = targetPath;
		shortcut.WorkingDirectory = workingDirectory;
		shortcut.IconLocation = targetPath;
		shortcut.Description = ProductName;
		shortcut.Save();
	}

	private static void RegisterUninstall( string directory, string applicationPath, long installedBytes )
	{
		using RegistryKey key = Registry.CurrentUser.CreateSubKey( UninstallKeyPath );
		string version = FileVersionInfo.GetVersionInfo( applicationPath ).ProductVersion?.Split( '+' )[ 0 ] ?? string.Empty;
		key.SetValue( "DisplayName", ProductName );
		key.SetValue( "DisplayVersion", version );
		key.SetValue( "Publisher", "Joshua Hermilly" );
		key.SetValue( "InstallLocation", directory );
		key.SetValue( "DisplayIcon", applicationPath );
		key.SetValue( "UninstallString", $"\"{Path.Combine( directory, SetupExecutable )}\" /uninstall" );
		key.SetValue( "EstimatedSize", ( int ) ( installedBytes / 1024 ), RegistryValueKind.DWord );
		key.SetValue( "NoModify", 1, RegistryValueKind.DWord );
		key.SetValue( "NoRepair", 1, RegistryValueKind.DWord );
	}

	private static void DeleteFileIfExists( string path )
	{
		if ( File.Exists( path ) )
		{
			File.Delete( path );
		}
	}
}
