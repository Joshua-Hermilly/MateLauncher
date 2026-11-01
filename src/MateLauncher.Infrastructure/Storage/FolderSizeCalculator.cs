namespace MateLauncher.Infrastructure.Storage;

public static class FolderSizeCalculator
{
	private static readonly EnumerationOptions _Options = new EnumerationOptions
	{
		RecurseSubdirectories = true,
		IgnoreInaccessible = true,
		AttributesToSkip = FileAttributes.ReparsePoint
	};

	public static Task<long> CalculateAsync( string folder )
	{
		return Task.Run( () =>
		{
			if ( !Directory.Exists( folder ) )
			{
				return 0L;
			}

			long total = 0;

			foreach ( string file in Directory.EnumerateFiles( folder, "*", _Options ) )
			{
				try
				{
					total += new FileInfo( file ).Length;
				}
				catch ( IOException )
				{
				}
			}

			return total;
		} );
	}
}
