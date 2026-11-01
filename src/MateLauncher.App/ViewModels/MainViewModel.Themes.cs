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
	[RelayCommand]
	public void SelectTheme( ThemeOptionViewModel? option )
	{
		if ( option is null )
		{
			return;
		}

		_ThemeId = option.Theme.Id;

		foreach ( ThemeOptionViewModel themeOption in BuiltInThemeOptions.Concat( CustomThemeOptions ) )
		{
			themeOption.IsSelected = themeOption == option;
		}

		_ThemeService.Apply( _ThemeId, _AccentColor );
		SaveSettings();
	}

	[RelayCommand]
	public void SelectAccent( AccentOptionViewModel? option )
	{
		if ( option is null )
		{
			return;
		}

		_AccentColor = option.Hex;

		foreach ( AccentOptionViewModel accentOption in AccentOptions )
		{
			accentOption.IsSelected = accentOption == option;
		}

		_ThemeService.Apply( _ThemeId, _AccentColor );
		SaveSettings();
	}

	private void RebuildAccentOptions()
	{
		AccentOptions.Clear();
		AccentOptions.Add( new AccentOptionViewModel( null ) { IsSelected = _AccentColor is null } );

		foreach ( string accentColor in _ThemeService.AccentColors )
		{
			AccentOptions.Add( new AccentOptionViewModel( accentColor ) { IsSelected = string.Equals( accentColor, _AccentColor, StringComparison.OrdinalIgnoreCase ) } );
		}

		if ( _AccentColor is not null && _ThemeService.AccentColors.All( accentColor => !string.Equals( accentColor, _AccentColor, StringComparison.OrdinalIgnoreCase ) ) )
		{
			AccentOptions.Add( new AccentOptionViewModel( _AccentColor ) { IsSelected = true } );
		}
	}

	[RelayCommand]
	public void PickCustomAccent()
	{
		ColorPickerWindow window = new ColorPickerWindow( T( "Accent_Name" ), _AccentColor ?? _ThemeService.GetTheme( _ThemeId ).Accent ) { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() != true )
		{
			return;
		}

		if ( window.IsReset )
		{
			_AccentColor = null;
		}
		else if ( window.SelectedColor is not null && AppTheme.IsValidColor( window.SelectedColor ) )
		{
			_AccentColor = AppTheme.NormalizeColor( window.SelectedColor );
		}

		RebuildAccentOptions();
		_ThemeService.Apply( _ThemeId, _AccentColor );
		SaveSettings();
	}

	private void RebuildThemeOptions()
	{
		BuiltInThemeOptions.Clear();
		CustomThemeOptions.Clear();

		foreach ( AppTheme theme in _ThemeService.Themes )
		{
			ThemeOptionViewModel option = new ThemeOptionViewModel( theme ) { IsSelected = theme.Id == _ThemeId };
			( theme.IsCustom ? CustomThemeOptions : BuiltInThemeOptions ).Add( option );
		}

		HasNoCustomThemes = CustomThemeOptions.Count == 0;
	}

	[RelayCommand]
	public void ImportTheme()
	{
		Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog { Title = T( "Theme_Import" ), Filter = T( "Theme_FileFilter" ) };

		if ( dialog.ShowDialog( Application.Current.MainWindow ) != true )
		{
			return;
		}

		AppTheme? theme = null;

		try
		{
			CustomTheme? custom = System.Text.Json.JsonSerializer.Deserialize<CustomTheme>( System.IO.File.ReadAllText( dialog.FileName ) );

			if ( custom is not null )
			{
				custom.Id = "custom-" + Guid.NewGuid().ToString( "N" )[ ..8 ];
				theme = AppTheme.FromCustom( custom );
			}
		}
		catch ( Exception exception ) when ( exception is System.Text.Json.JsonException or System.IO.IOException or UnauthorizedAccessException )
		{
			theme = null;
		}

		if ( theme is null )
		{
			MessageBox.Show( Application.Current.MainWindow, T( "Theme_ImportInvalid" ), T( "Theme_Import" ), MessageBoxButton.OK, MessageBoxImage.Warning );
			return;
		}

		_ThemeService.SaveCustomTheme( theme );
		_ThemeId = theme.Id;
		_AccentColor = null;
		RebuildThemeOptions();
		RebuildAccentOptions();
		_ThemeService.Apply( _ThemeId, _AccentColor );
		SaveSettings();
	}

	[RelayCommand]
	public void CreateTheme()
	{
		OpenThemeEditor( _ThemeService.GetTheme( _ThemeId ), true );
	}

	[RelayCommand]
	public void EditTheme( ThemeOptionViewModel? option )
	{
		if ( option is not null && option.Theme.IsCustom )
		{
			OpenThemeEditor( option.Theme, false );
		}
	}

	private void OpenThemeEditor( AppTheme source, bool isNew )
	{
		ThemeEditorWindow window = new ThemeEditorWindow( source, isNew ) { Owner = Application.Current.MainWindow };

		if ( window.ShowDialog() != true )
		{
			return;
		}

		if ( window.IsDeleted )
		{
			if ( !ConfirmDialog.Ask( T( "Theme_DeleteTitle" ), string.Format( T( "Theme_DeleteMessage" ), source.Name ), T( "Btn_Delete" ) ) )
			{
				return;
			}

			_ThemeService.RemoveCustomTheme( source.Id );

			if ( _ThemeId == source.Id )
			{
				_ThemeId = ThemeService.DefaultThemeId;
			}
		}
		else if ( window.Result is not null )
		{
			_ThemeService.SaveCustomTheme( window.Result );
			_ThemeId = window.Result.Id;
			_AccentColor = null;

			foreach ( AccentOptionViewModel accentOption in AccentOptions )
			{
				accentOption.IsSelected = accentOption.Hex is null;
			}
		}

		RebuildThemeOptions();
		_ThemeService.Apply( _ThemeId, _AccentColor );
		SaveSettings();
	}
}
