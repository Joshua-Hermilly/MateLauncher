using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MateLauncher.Infrastructure.Launching;

public static class ProcessFinder
{
	private const int ProcessQueryLimitedInformation = 0x1000;

	[DllImport( "kernel32.dll", SetLastError = true )]
	private static extern IntPtr OpenProcess( int desiredAccess, bool inheritHandle, int processId );

	[DllImport( "kernel32.dll", SetLastError = true )]
	private static extern bool CloseHandle( IntPtr handle );

	[DllImport( "kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true )]
	private static extern bool QueryFullProcessImageName( IntPtr process, int flags, StringBuilder exeName, ref int size );

	public static string NormalizeFolder( string folder )
	{
		return Path.GetFullPath( folder ).TrimEnd( Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar;
	}

	public static bool IsAnyRunning( string folder )
	{
		List<Process> processes = FindInFolder( folder );
		processes.ForEach( process => process.Dispose() );
		return processes.Count > 0;
	}

	public static List<Process> FindInFolder( string folder )
	{
		string normalizedFolder = NormalizeFolder( folder );
		List<Process> matches = new List<Process>();

		foreach ( Process process in Process.GetProcesses() )
		{
			string? imagePath = GetImagePath( process.Id );

			if ( imagePath is not null && imagePath.StartsWith( normalizedFolder, StringComparison.OrdinalIgnoreCase ) )
			{
				matches.Add( process );
			}
			else
			{
				process.Dispose();
			}
		}

		return matches;
	}

	private static string? GetImagePath( int processId )
	{
		IntPtr handle = OpenProcess( ProcessQueryLimitedInformation, false, processId );

		if ( handle == IntPtr.Zero )
		{
			return null;
		}

		try
		{
			StringBuilder builder = new StringBuilder( 1024 );
			int size = builder.Capacity;
			return QueryFullProcessImageName( handle, 0, builder, ref size ) ? builder.ToString() : null;
		}
		finally
		{
			CloseHandle( handle );
		}
	}
}
