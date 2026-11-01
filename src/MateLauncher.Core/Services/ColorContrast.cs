using System.Globalization;

namespace MateLauncher.Core.Services;

public static class ColorContrast
{
	public const string LightText = "#FFFFFF";
	public const string DarkText = "#14181F";

	private const double LuminanceThreshold = 0.3;

	public static string GetReadableTextColor( string backgroundHex )
	{
		string value = backgroundHex.Trim().TrimStart( '#' );

		if ( value.Length == 8 )
		{
			value = value[ 2.. ];
		}

		if ( value.Length != 6 || !int.TryParse( value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb ) )
		{
			return LightText;
		}

		double luminance = ( 0.2126 * Linearize( ( rgb >> 16 ) & 0xFF ) ) + ( 0.7152 * Linearize( ( rgb >> 8 ) & 0xFF ) ) + ( 0.0722 * Linearize( rgb & 0xFF ) );
		return luminance > LuminanceThreshold ? DarkText : LightText;
	}

	private static double Linearize( int channel )
	{
		double value = channel / 255.0;
		return value <= 0.03928 ? value / 12.92 : Math.Pow( ( value + 0.055 ) / 1.055, 2.4 );
	}
}
