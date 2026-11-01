using System.IO.Compression;
using MateLauncher.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MateLauncher.Infrastructure.Backup;

public class BackupService
{
	private const int MaxAutomaticBackups = 10;
	private const string DatabaseFileName = "library.db";
	private const string SettingsFileName = "settings.json";
	private const string IconsFolderName = "icons";
	private const string PendingImportFolderName = "pending-import";

	private readonly IDbContextFactory<LauncherDbContext> _ContextFactory;
	private readonly string _DataFolder;

	public string BackupsFolder => Path.Combine( _DataFolder, "backups" );

	public BackupService( IDbContextFactory<LauncherDbContext> contextFactory, string dataFolder )
	{
		_ContextFactory = contextFactory;
		_DataFolder = dataFolder;
	}

	public static bool ApplyPendingImport( string dataFolder )
	{
		string pendingFolder = Path.Combine( dataFolder, PendingImportFolderName );

		if ( !Directory.Exists( pendingFolder ) )
		{
			return false;
		}

		string databasePath = Path.Combine( pendingFolder, DatabaseFileName );
		string safetyFolder = Path.Combine( dataFolder, "backups", $"before-import-{DateTime.Now:yyyyMMdd-HHmmss}" );
		Directory.CreateDirectory( safetyFolder );

		foreach ( string fileName in new[] { DatabaseFileName, SettingsFileName } )
		{
			string current = Path.Combine( dataFolder, fileName );

			if ( File.Exists( current ) )
			{
				File.Copy( current, Path.Combine( safetyFolder, fileName ), true );
			}
		}

		if ( File.Exists( databasePath ) )
		{
			foreach ( string suffix in new[] { string.Empty, "-wal", "-shm" } )
			{
				string target = Path.Combine( dataFolder, DatabaseFileName + suffix );

				if ( File.Exists( target ) )
				{
					File.Delete( target );
				}
			}

			File.Move( databasePath, Path.Combine( dataFolder, DatabaseFileName ) );
		}

		MoveIfExists( Path.Combine( pendingFolder, SettingsFileName ), Path.Combine( dataFolder, SettingsFileName ) );
		string pendingIcons = Path.Combine( pendingFolder, IconsFolderName );

		if ( Directory.Exists( pendingIcons ) )
		{
			Directory.CreateDirectory( Path.Combine( dataFolder, IconsFolderName ) );

			foreach ( string iconFile in Directory.EnumerateFiles( pendingIcons ) )
			{
				File.Copy( iconFile, Path.Combine( dataFolder, IconsFolderName, Path.GetFileName( iconFile ) ), true );
			}
		}

		Directory.Delete( pendingFolder, true );
		return true;
	}

	public async Task<string> CreateAutomaticBackupAsync()
	{
		Directory.CreateDirectory( BackupsFolder );
		string target = Path.Combine( BackupsFolder, $"backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip" );
		await WriteArchiveAsync( target );
		RemoveOldBackups();
		return target;
	}

	public async Task ExportAsync( string archivePath )
	{
		await WriteArchiveAsync( archivePath );
	}

	public string? FindLatestBackup()
	{
		if ( !Directory.Exists( BackupsFolder ) )
		{
			return null;
		}

		return Directory.EnumerateFiles( BackupsFolder, "backup-*.zip" ).OrderByDescending( path => path, StringComparer.Ordinal ).FirstOrDefault();
	}

	public void StageImport( string archivePath )
	{
		string pendingFolder = Path.Combine( _DataFolder, PendingImportFolderName );

		if ( Directory.Exists( pendingFolder ) )
		{
			Directory.Delete( pendingFolder, true );
		}

		Directory.CreateDirectory( pendingFolder );
		string rootPath = Path.GetFullPath( pendingFolder );

		using ZipArchive archive = ZipFile.OpenRead( archivePath );

		if ( !archive.Entries.Any( entry => entry.FullName == DatabaseFileName || entry.FullName == SettingsFileName ) )
		{
			throw new InvalidDataException( "Not a MateLauncher backup" );
		}

		foreach ( ZipArchiveEntry entry in archive.Entries.Where( entry => !string.IsNullOrEmpty( entry.Name ) ) )
		{
			string targetPath = Path.GetFullPath( Path.Combine( rootPath, entry.FullName ) );

			if ( !targetPath.StartsWith( rootPath, StringComparison.OrdinalIgnoreCase ) )
			{
				continue;
			}

			Directory.CreateDirectory( Path.GetDirectoryName( targetPath )! );
			entry.ExtractToFile( targetPath, true );
		}
	}

	private async Task WriteArchiveAsync( string archivePath )
	{
		string workFolder = Path.Combine( Path.GetTempPath(), $"matelauncher-backup-{Guid.NewGuid():N}" );
		Directory.CreateDirectory( workFolder );

		try
		{
			string databaseCopy = Path.Combine( workFolder, DatabaseFileName );

			await using ( LauncherDbContext context = await _ContextFactory.CreateDbContextAsync() )
			{
				await context.Database.ExecuteSqlRawAsync( $"VACUUM INTO '{databaseCopy.Replace( "'", "''" )}'" );
			}

			if ( File.Exists( archivePath ) )
			{
				File.Delete( archivePath );
			}

			using ZipArchive archive = ZipFile.Open( archivePath, ZipArchiveMode.Create );
			archive.CreateEntryFromFile( databaseCopy, DatabaseFileName );

			string settingsPath = Path.Combine( _DataFolder, SettingsFileName );

			if ( File.Exists( settingsPath ) )
			{
				archive.CreateEntryFromFile( settingsPath, SettingsFileName );
			}

			string iconsPath = Path.Combine( _DataFolder, IconsFolderName );

			if ( Directory.Exists( iconsPath ) )
			{
				foreach ( string iconFile in Directory.EnumerateFiles( iconsPath ) )
				{
					archive.CreateEntryFromFile( iconFile, $"{IconsFolderName}/{Path.GetFileName( iconFile )}" );
				}
			}
		}
		finally
		{
			Directory.Delete( workFolder, true );
		}
	}

	private void RemoveOldBackups()
	{
		foreach ( string oldBackup in Directory.EnumerateFiles( BackupsFolder, "backup-*.zip" ).OrderByDescending( path => path, StringComparer.Ordinal ).Skip( MaxAutomaticBackups ) )
		{
			File.Delete( oldBackup );
		}
	}

	private static void MoveIfExists( string source, string target )
	{
		if ( !File.Exists( source ) )
		{
			return;
		}

		File.Move( source, target, true );
	}
}
