using MateLauncher.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace MateLauncher.Infrastructure.Persistence;

public class LauncherDbContext : DbContext
{
	public DbSet<Game> Games => Set<Game>();
	public DbSet<DailyPlaytime> DailyPlaytimes => Set<DailyPlaytime>();

	public LauncherDbContext( DbContextOptions<LauncherDbContext> options ) : base( options )
	{
	}

	protected override void OnModelCreating( ModelBuilder modelBuilder )
	{
		modelBuilder.Entity<Game>( entity =>
		{
			entity.ToTable( "GAMES" );
			entity.HasKey( game => game.Id );
			entity.Property( game => game.Id ).HasColumnName( "ID" );
			entity.Property( game => game.Name ).HasColumnName( "NAME" ).IsRequired();
			entity.Property( game => game.Platform ).HasColumnName( "PLATFORM" ).HasConversion<string>();
			entity.Property( game => game.PlatformGameId ).HasColumnName( "PLATFORM_GAME_ID" );
			entity.Property( game => game.InstallPath ).HasColumnName( "INSTALL_PATH" );
			entity.Property( game => game.LaunchCommand ).HasColumnName( "LAUNCH_COMMAND" );
			entity.Property( game => game.LaunchArguments ).HasColumnName( "LAUNCH_ARGUMENTS" );
			entity.Property( game => game.RunAsAdministrator ).HasColumnName( "RUN_AS_ADMINISTRATOR" );
			entity.Property( game => game.CoverImagePath ).HasColumnName( "COVER_IMAGE_PATH" );
			entity.Property( game => game.PlaytimeSeconds ).HasColumnName( "PLAYTIME_SECONDS" );
			entity.Property( game => game.LastPlayedAt ).HasColumnName( "LAST_PLAYED_AT" );
			entity.Property( game => game.IsFavorite ).HasColumnName( "IS_FAVORITE" );
			entity.Property( game => game.IsHidden ).HasColumnName( "IS_HIDDEN" );
			entity.Property( game => game.GroupName ).HasColumnName( "GROUP_NAME" );
			entity.Property( game => game.IsCustomized ).HasColumnName( "IS_CUSTOMIZED" );
			entity.Property( game => game.SizeBytes ).HasColumnName( "SIZE_BYTES" );
			entity.Property( game => game.CategoryName ).HasColumnName( "CATEGORY_NAME" );
			entity.HasIndex( game => new { game.Platform, game.PlatformGameId } );
		} );

		modelBuilder.Entity<DailyPlaytime>( entity =>
		{
			entity.ToTable( "PLAYTIME_DAILY" );
			entity.HasKey( daily => new { daily.Day, daily.GameId } );
			entity.Property( daily => daily.Day ).HasColumnName( "DAY" );
			entity.Property( daily => daily.GameId ).HasColumnName( "GAME_ID" );
			entity.Property( daily => daily.Seconds ).HasColumnName( "SECONDS" );
		} );
	}
}
