using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace MateLauncher.Infrastructure.Security;

public static class SecretProtector
{
	public const string Prefix = "dpapi:";

	public static string Protect( string value )
	{
		if ( string.IsNullOrEmpty( value ) || value.StartsWith( Prefix, StringComparison.Ordinal ) )
		{
			return value;
		}

		if ( !OperatingSystem.IsWindows() )
		{
			return value;
		}

		return Prefix + Convert.ToBase64String( ProtectForCurrentUser( Encoding.UTF8.GetBytes( value ) ) );
	}

	public static string Unprotect( string value )
	{
		if ( string.IsNullOrEmpty( value ) || !value.StartsWith( Prefix, StringComparison.Ordinal ) )
		{
			return value;
		}

		if ( !OperatingSystem.IsWindows() )
		{
			return string.Empty;
		}

		try
		{
			return Encoding.UTF8.GetString( UnprotectForCurrentUser( Convert.FromBase64String( value[ Prefix.Length.. ] ) ) );
		}
		catch ( Exception exception ) when ( exception is CryptographicException or FormatException )
		{
			return string.Empty;
		}
	}

	[SupportedOSPlatform( "windows" )]
	private static byte[] ProtectForCurrentUser( byte[] data )
	{
		return ProtectedData.Protect( data, null, DataProtectionScope.CurrentUser );
	}

	[SupportedOSPlatform( "windows" )]
	private static byte[] UnprotectForCurrentUser( byte[] data )
	{
		return ProtectedData.Unprotect( data, null, DataProtectionScope.CurrentUser );
	}
}
