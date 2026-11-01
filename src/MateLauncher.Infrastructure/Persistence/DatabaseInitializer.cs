using Microsoft.EntityFrameworkCore;

namespace MateLauncher.Infrastructure.Persistence;

public static class DatabaseInitializer
{
	public static void Initialize( LauncherDbContext context )
	{
		context.Database.EnsureCreated();
		AddColumnIfMissing( context, "LAUNCH_ARGUMENTS", "ALTER TABLE GAMES ADD COLUMN LAUNCH_ARGUMENTS TEXT NOT NULL DEFAULT ''" );
		AddColumnIfMissing( context, "RUN_AS_ADMINISTRATOR", "ALTER TABLE GAMES ADD COLUMN RUN_AS_ADMINISTRATOR INTEGER NOT NULL DEFAULT 0" );
		AddColumnIfMissing( context, "GROUP_NAME", "ALTER TABLE GAMES ADD COLUMN GROUP_NAME TEXT NULL" );
		AddColumnIfMissing( context, "IS_CUSTOMIZED", "ALTER TABLE GAMES ADD COLUMN IS_CUSTOMIZED INTEGER NOT NULL DEFAULT 0" );
		AddColumnIfMissing( context, "SIZE_BYTES", "ALTER TABLE GAMES ADD COLUMN SIZE_BYTES INTEGER NOT NULL DEFAULT 0" );
		AddColumnIfMissing( context, "CATEGORY_NAME", "ALTER TABLE GAMES ADD COLUMN CATEGORY_NAME TEXT NULL" );
		context.Database.ExecuteSqlRaw( "CREATE TABLE IF NOT EXISTS PLAYTIME_DAILY ( DAY TEXT NOT NULL, GAME_ID TEXT NOT NULL, SECONDS INTEGER NOT NULL, PRIMARY KEY ( DAY, GAME_ID ) )" );
		context.Database.ExecuteSqlRaw( "CREATE TABLE IF NOT EXISTS PLAYTIME_SESSIONS ( ID TEXT NOT NULL PRIMARY KEY, GAME_ID TEXT NOT NULL, STARTED_AT TEXT NOT NULL, SECONDS INTEGER NOT NULL )" );
		context.Database.ExecuteSqlRaw( "CREATE INDEX IF NOT EXISTS IX_PLAYTIME_SESSIONS_GAME_ID ON PLAYTIME_SESSIONS ( GAME_ID )" );
	}

	private static void AddColumnIfMissing( LauncherDbContext context, string columnName, string alterStatement )
	{
		List<string> existingColumns = context.Database.SqlQueryRaw<string>( "SELECT name AS Value FROM pragma_table_info('GAMES')" ).ToList();

		if ( !existingColumns.Contains( columnName ) )
		{
			context.Database.ExecuteSqlRaw( alterStatement );
		}
	}
}
