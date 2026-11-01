namespace MateLauncher.Core.Abstractions;

public interface ICoverFinder
{
	Task<string?> FindCoverUrlAsync( string gameName );
}
