using System.Globalization;

namespace MateLauncher.Installer;

public static class Texts
{
	private static readonly bool _IsFrench = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr";

	public static string Get( string french, string english )
	{
		return _IsFrench ? french : english;
	}
}
