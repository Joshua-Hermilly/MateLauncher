using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MateLauncher.Providers.Saves;

namespace MateLauncher.App.ViewModels;

public partial class MainViewModel
{
	public ObservableCollection<SaveLocationViewModel> SaveLocations { get; } = new ObservableCollection<SaveLocationViewModel>();

	[ObservableProperty]
	private bool _HasSaveLocations;

	[ObservableProperty]
	private bool _IsSearchingSaves;

	[ObservableProperty]
	private string _SavesStatus = string.Empty;

	private async Task LoadSavesAsync( GameCardViewModel? card )
	{
		SaveLocations.Clear();
		HasSaveLocations = false;
		SavesStatus = string.Empty;

		if ( card is null )
		{
			IsSearchingSaves = false;
			return;
		}

		IsSearchingSaves = true;
		SavesStatus = T( "Saves_Searching" );
		IReadOnlyList<SaveLocation> locations = await Task.Run( () => SaveLocationFinder.Find( card.Game ) );

		if ( SelectedGame != card )
		{
			return;
		}

		foreach ( SaveLocation location in locations )
		{
			SaveLocations.Add( new SaveLocationViewModel( location ) );
		}

		IsSearchingSaves = false;
		HasSaveLocations = SaveLocations.Count > 0;
		SavesStatus = HasSaveLocations ? T( "Saves_Hint" ) : T( "Saves_None" );
	}

	[RelayCommand]
	public void OpenSaveFolder( string? path )
	{
		if ( string.IsNullOrWhiteSpace( path ) || !Directory.Exists( path ) )
		{
			return;
		}

		try
		{
			Process.Start( new ProcessStartInfo( "explorer.exe", $"\"{path}\"" ) { UseShellExecute = true } );
		}
		catch ( System.ComponentModel.Win32Exception )
		{
		}
	}

	[RelayCommand]
	public void CopySavePath( string? path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
		{
			return;
		}

		try
		{
			Clipboard.SetText( path );
		}
		catch ( System.Runtime.InteropServices.COMException )
		{
		}
	}
}
