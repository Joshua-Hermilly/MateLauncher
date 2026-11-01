using MateLauncher.Core.Abstractions;

namespace MateLauncher.Infrastructure.Covers;

public class CompositeCoverFinder : ICoverFinder
{
	private readonly IReadOnlyList<ICoverFinder> _Finders;

	public CompositeCoverFinder( params ICoverFinder[] finders )
	{
		_Finders = finders;
	}

	public async Task<string?> FindCoverUrlAsync( string gameName )
	{
		foreach ( ICoverFinder finder in _Finders )
		{
			string? coverUrl = await finder.FindCoverUrlAsync( gameName );

			if ( coverUrl is not null )
			{
				return coverUrl;
			}
		}

		return null;
	}
}
