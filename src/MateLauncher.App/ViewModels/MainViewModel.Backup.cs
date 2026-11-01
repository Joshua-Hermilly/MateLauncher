using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MateLauncher.App.Icons;
using MateLauncher.App.Localization;
using MateLauncher.App.Themes;
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;
using MateLauncher.Infrastructure.Achievements;
using MateLauncher.Infrastructure.Backup;
using MateLauncher.Providers.Steam;
using MateLauncher.Infrastructure.Covers;
using MateLauncher.App.Icons;
using MateLauncher.Infrastructure.Downloads;
using MateLauncher.Infrastructure.News;
using MateLauncher.Infrastructure.Storage;
using MateLauncher.Infrastructure.Updates;
using MateLauncher.App.Startup;
using Microsoft.Win32;

namespace MateLauncher.App.ViewModels;

public partial class MainViewModel
{
	public async Task RunAutomaticBackupAsync()
	{
		AppSettings settings = _SettingsService.Load();

		if ( settings.LastBackupAt is not null && ( DateTime.Now - settings.LastBackupAt.Value ).TotalHours < 24 )
		{
			return;
		}

		try
		{
			await _BackupService.CreateAutomaticBackupAsync();
			settings.LastBackupAt = DateTime.Now;
			_SettingsService.Save( settings );
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException )
		{
			DebugLog.Write( $"Automatic backup failed{Environment.NewLine}{exception}" );
		}
	}

	[RelayCommand]
	public async Task BackupNowAsync()
	{
		try
		{
			string path = await _BackupService.CreateAutomaticBackupAsync();
			BackupStatus = string.Format( T( "Backup_Done" ), Path.GetFileName( path ) );
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException )
		{
			BackupStatus = exception.Message;
		}
	}

	[RelayCommand]
	public async Task ExportBackupAsync()
	{
		SaveFileDialog dialog = new SaveFileDialog { Title = T( "Backup_Export" ), Filter = "MateLauncher backup (*.zip)|*.zip", FileName = $"MateLauncher-{DateTime.Now:yyyyMMdd}.zip" };

		if ( dialog.ShowDialog() != true )
		{
			return;
		}

		try
		{
			await _BackupService.ExportAsync( dialog.FileName );
			BackupStatus = string.Format( T( "Backup_Done" ), Path.GetFileName( dialog.FileName ) );
		}
		catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException )
		{
			BackupStatus = exception.Message;
		}
	}

	[RelayCommand]
	public void ImportBackup()
	{
		OpenFileDialog dialog = new OpenFileDialog { Title = T( "Backup_Import" ), Filter = "MateLauncher backup (*.zip)|*.zip" };

		if ( dialog.ShowDialog() != true || !ConfirmDialog.Ask( T( "Backup_Import" ), T( "Backup_ImportConfirm" ), T( "Backup_ImportButton" ) ) )
		{
			return;
		}

		try
		{
			_BackupService.StageImport( dialog.FileName );
		}
		catch ( Exception exception ) when ( exception is IOException or InvalidDataException or UnauthorizedAccessException )
		{
			BackupStatus = exception.Message;
			return;
		}

		AppRestarter.Restart();
	}

	[RelayCommand]
	public void OpenBackupsFolder()
	{
		Directory.CreateDirectory( _BackupService.BackupsFolder );
		Process.Start( new ProcessStartInfo( "explorer.exe", $"\"{_BackupService.BackupsFolder}\"" ) { UseShellExecute = true } );
	}
}
