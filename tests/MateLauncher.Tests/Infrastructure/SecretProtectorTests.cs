using MateLauncher.Infrastructure.Security;

namespace MateLauncher.Tests.Infrastructure;

public class SecretProtectorTests
{
	[Fact]
	public void Protect_ThenUnprotect_ReturnsOriginal()
	{
		string protectedValue = SecretProtector.Protect( "my-secret-key" );

		Assert.StartsWith( SecretProtector.Prefix, protectedValue );
		Assert.DoesNotContain( "my-secret-key", protectedValue );
		Assert.Equal( "my-secret-key", SecretProtector.Unprotect( protectedValue ) );
	}

	[Fact]
	public void Protect_IsIdempotentOnProtectedValues()
	{
		string once = SecretProtector.Protect( "abc" );

		Assert.Equal( once, SecretProtector.Protect( once ) );
	}

	[Fact]
	public void Unprotect_ReturnsLegacyPlainValuesUnchanged()
	{
		Assert.Equal( "plain-key", SecretProtector.Unprotect( "plain-key" ) );
	}

	[Fact]
	public void Unprotect_ReturnsEmptyOnCorruptedPayload()
	{
		Assert.Equal( string.Empty, SecretProtector.Unprotect( SecretProtector.Prefix + "not-base64!!" ) );
	}

	[Fact]
	public void EmptyValues_StayEmpty()
	{
		Assert.Equal( string.Empty, SecretProtector.Protect( string.Empty ) );
		Assert.Equal( string.Empty, SecretProtector.Unprotect( string.Empty ) );
	}
}
