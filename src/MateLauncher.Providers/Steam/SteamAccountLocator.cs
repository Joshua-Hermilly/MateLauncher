using System.Text.RegularExpressions;

namespace MateLauncher.Providers.Steam;

public static partial class SteamAccountLocator
{
	[GeneratedRegex( "\"(?<id>\\d{17})\"\\s*\\{(?<body>[^}]*)\\}", RegexOptions.Singleline )]
	private static partial Regex AccountPattern();

	[GeneratedRegex( "\"MostRecent\"\\s+\"1\"" )]
	private static partial Regex MostRecentPattern();

	public static string? FindSteamId()
	{
		string? steamPath = SteamProvider.FindSteamPath();

		if ( steamPath is null )
		{
			return null;
		}

		string usersFile = Path.Combine( steamPath, "config", "loginusers.vdf" );

		if ( !File.Exists( usersFile ) )
		{
			return null;
		}

		try
		{
			MatchCollection accounts = AccountPattern().Matches( File.ReadAllText( usersFile ) );
			Match? mostRecent = accounts.FirstOrDefault( account => MostRecentPattern().IsMatch( account.Groups[ "body" ].Value ) );
			return ( mostRecent ?? accounts.FirstOrDefault() )?.Groups[ "id" ].Value;
		}
		catch ( IOException )
		{
			return null;
		}
	}
}
