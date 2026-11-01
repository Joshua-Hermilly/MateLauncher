namespace MateLauncher.Core.Services;

public static class DebugLog
{
	private static readonly object _Lock = new object();
	private static string? _FilePath;

	public static bool IsEnabled => _FilePath is not null;

	public static void Enable( string filePath )
	{
		_FilePath = filePath;
		Write( "Debug log started" );
	}

	public static void Write( string message )
	{
		if ( _FilePath is null )
		{
			return;
		}

		lock ( _Lock )
		{
			try
			{
				File.AppendAllText( _FilePath, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}" );
			}
			catch ( IOException )
			{
			}
		}
	}
}
