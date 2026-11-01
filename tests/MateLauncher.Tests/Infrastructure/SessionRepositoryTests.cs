using System.Text.Json;
using MateLauncher.Core.Models;
using MateLauncher.Infrastructure.Metadata;
using MateLauncher.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MateLauncher.Tests.Infrastructure;

public class SessionRepositoryTests : IDisposable
{
	private sealed class TestFactory : IDbContextFactory<LauncherDbContext>
	{
		private readonly DbContextOptions<LauncherDbContext> _Options;

		public TestFactory( string databasePath )
		{
			_Options = new DbContextOptionsBuilder<LauncherDbContext>().UseSqlite( $"Data Source={databasePath};Pooling=False" ).Options;
		}

		public LauncherDbContext CreateDbContext()
		{
			return new LauncherDbContext( _Options );
		}
	}

	private readonly string _Folder = Path.Combine( Path.GetTempPath(), "MateLauncherTests-" + Guid.NewGuid() );
	private readonly TestFactory _Factory;

	public SessionRepositoryTests()
	{
		Directory.CreateDirectory( _Folder );
		_Factory = new TestFactory( Path.Combine( _Folder, "library.db" ) );

		using LauncherDbContext context = _Factory.CreateDbContext();
		DatabaseInitializer.Initialize( context );
	}

	public void Dispose()
	{
		Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
		Directory.Delete( _Folder, true );
	}

	[Fact]
	public async Task GetSessionsAsync_ReturnsNewestFirstAndRespectsTake()
	{
		LibraryRepository repository = new LibraryRepository( _Factory );
		Guid gameId = Guid.NewGuid();

		await repository.AddSessionAsync( gameId, new DateTime( 2026, 1, 1, 20, 0, 0 ), 3600 );
		await repository.AddSessionAsync( gameId, new DateTime( 2026, 1, 3, 20, 0, 0 ), 1800 );
		await repository.AddSessionAsync( gameId, new DateTime( 2026, 1, 2, 20, 0, 0 ), 900 );

		IReadOnlyList<GameSession> sessions = await repository.GetSessionsAsync( gameId, 2 );

		Assert.Equal( new long[] { 1800, 900 }, sessions.Select( session => session.Seconds ).ToArray() );
	}

	[Fact]
	public async Task GetSessionsAsync_OnlyReturnsSessionsOfTheGivenGame()
	{
		LibraryRepository repository = new LibraryRepository( _Factory );
		Guid first = Guid.NewGuid();

		await repository.AddSessionAsync( first, DateTime.Now, 120 );
		await repository.AddSessionAsync( Guid.NewGuid(), DateTime.Now, 999 );

		Assert.Single( await repository.GetSessionsAsync( first, 10 ) );
	}

	[Fact]
	public void Initialize_IsIdempotentOnAnExistingDatabase()
	{
		using LauncherDbContext context = _Factory.CreateDbContext();

		DatabaseInitializer.Initialize( context );
		DatabaseInitializer.Initialize( context );

		Assert.True( context.GameSessions.Count() >= 0 );
	}

	[Fact]
	public void IgdbParseTimeToBeat_PrefersNormalThenFallsBack()
	{
		Assert.Equal( 36000, IgdbClient.ParseTimeToBeat( JsonDocument.Parse( """[ { "hastily": 18000, "normally": 36000, "completely": 90000 } ]""" ).RootElement ) );
		Assert.Equal( 18000, IgdbClient.ParseTimeToBeat( JsonDocument.Parse( """[ { "hastily": 18000 } ]""" ).RootElement ) );
		Assert.Null( IgdbClient.ParseTimeToBeat( JsonDocument.Parse( "[]" ).RootElement ) );
	}

	[Fact]
	public void IgdbParseGameId_ReadsTheFirstResult()
	{
		Assert.Equal( 1905L, IgdbClient.ParseGameId( JsonDocument.Parse( """[ { "id": 1905, "name": "x" } ]""" ).RootElement ) );
		Assert.Null( IgdbClient.ParseGameId( JsonDocument.Parse( """[ { "name": "x" } ]""" ).RootElement ) );
	}
}
