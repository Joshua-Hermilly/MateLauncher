using System.Diagnostics;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;

namespace MateLauncher.Infrastructure.Launching;

public class PerformanceMode : IPerformanceMode
{
	private static readonly TimeSpan _CloseTimeout = TimeSpan.FromSeconds( 3 );

	private static readonly HashSet<string> _ProtectedNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
	{
		"explorer", "MateLauncher", "MateLauncher.App", "MateLauncher.Setup", "dwm", "winlogon", "csrss", "wininit", "services", "lsass", "svchost", "system", "smss", "taskmgr"
	};

	private readonly ISettingsService _SettingsService;
	private readonly object _Lock = new object();
	private readonly List<string> _ClosedExecutables = new List<string>();

	public PerformanceMode( ISettingsService settingsService )
	{
		_SettingsService = settingsService;
	}

	public static List<string> ParseProgramNames( string? text )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
		{
			return new List<string>();
		}

		return text.Split( new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
			.Select( name => name.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) ? name[ ..^4 ] : name )
			.Where( name => name.Length > 0 && name.IndexOfAny( Path.GetInvalidFileNameChars() ) < 0 && !_ProtectedNames.Contains( name ) )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToList();
	}

	public async Task PrepareAsync( Game game )
	{
		AppSettings settings = _SettingsService.Load();

		if ( !settings.PerformanceModeEnabled )
		{
			return;
		}

		List<string> programs = ParseProgramNames( string.Join( ';', settings.PerformancePrograms ) );
		List<string> closed = await Task.Run( () => CloseGracefully( programs, game ) );

		lock ( _Lock )
		{
			_ClosedExecutables.AddRange( closed.Where( path => !_ClosedExecutables.Contains( path, StringComparer.OrdinalIgnoreCase ) ) );
		}

		if ( closed.Count > 0 )
		{
			DebugLog.Write( $"Performance mode: closed {closed.Count} program(s) for {game.Name}" );
		}
	}

	public void ApplyToRunningGame( Game game )
	{
		if ( !_SettingsService.Load().PerformanceModeEnabled || !Directory.Exists( game.InstallPath ) )
		{
			return;
		}

		foreach ( Process process in ProcessFinder.FindInFolder( game.InstallPath ) )
		{
			using ( process )
			{
				try
				{
					process.PriorityClass = ProcessPriorityClass.High;
				}
				catch ( Exception exception ) when ( exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException )
				{
				}
			}
		}
	}

	public async Task RestoreAsync( Game game )
	{
		List<string> toRestart;

		lock ( _Lock )
		{
			toRestart = _ClosedExecutables.ToList();
			_ClosedExecutables.Clear();
		}

		if ( toRestart.Count == 0 )
		{
			return;
		}

		await Task.Run( () =>
		{
			foreach ( string executable in toRestart )
			{
				try
				{
					if ( File.Exists( executable ) && !IsRunning( executable ) )
					{
						Process.Start( new ProcessStartInfo( executable ) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName( executable ) ?? string.Empty } );
					}
				}
				catch ( Exception exception ) when ( exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException )
				{
				}
			}
		} );
	}

	private static List<string> CloseGracefully( IReadOnlyList<string> names, Game game )
	{
		List<string> closed = new List<string>();
		string? gameFolder = Directory.Exists( game.InstallPath ) ? ProcessFinder.NormalizeFolder( game.InstallPath ) : null;

		foreach ( string name in names )
		{
			foreach ( Process process in Process.GetProcessesByName( name ) )
			{
				using ( process )
				{
					try
					{
						string? path = process.MainModule?.FileName;

						if ( path is null || ( gameFolder is not null && path.StartsWith( gameFolder, StringComparison.OrdinalIgnoreCase ) ) )
						{
							continue;
						}

						if ( process.CloseMainWindow() && process.WaitForExit( ( int ) _CloseTimeout.TotalMilliseconds ) )
						{
							closed.Add( path );
						}
					}
					catch ( Exception exception ) when ( exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException )
					{
					}
				}
			}
		}

		return closed.Distinct( StringComparer.OrdinalIgnoreCase ).ToList();
	}

	private static bool IsRunning( string executable )
	{
		foreach ( Process process in Process.GetProcessesByName( Path.GetFileNameWithoutExtension( executable ) ) )
		{
			process.Dispose();
			return true;
		}

		return false;
	}
}
