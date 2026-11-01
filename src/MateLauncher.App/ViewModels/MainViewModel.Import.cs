using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;
using MateLauncher.Infrastructure.Steam;

namespace MateLauncher.App.ViewModels;

public partial class MainViewModel
{
	private readonly SteamPlaytimeService _SteamPlaytimeService = new SteamPlaytimeService();

	[ObservableProperty]
	private string _SteamImportStatus = string.Empty;

	[ObservableProperty]
	private bool _IsImportingPlaytime;

	[RelayCommand]
	public async Task ImportSteamPlaytimeAsync()
	{
		if ( IsImportingPlaytime )
		{
			return;
		}

		if ( !HasSteamCredentials )
		{
			SteamImportStatus = T( "Steam_MissingFields" );
			return;
		}

		IsImportingPlaytime = true;
		SteamImportStatus = T( "Import_Running" );

		try
		{
			SteamPlaytimeResult result = await _SteamPlaytimeService.GetOwnedPlaytimesAsync( SteamApiKey, SteamId );

			if ( result.Playtimes is null )
			{
				SteamImportStatus = T( result.ErrorKey ?? "Import_Error" );
				return;
			}

			Dictionary<Guid, ImportedPlaytime> updates = PlaytimeImport.ComputeUpdates( _AllCards.Select( card => card.Game ), result.Playtimes );

			if ( updates.Count == 0 )
			{
				SteamImportStatus = T( "Import_Nothing" );
				return;
			}

			int applied = await _Repository.ApplyImportedPlaytimeAsync( updates );
			await RefreshAsync();
			SteamImportStatus = string.Format( T( "Import_Done" ), applied );
		}
		finally
		{
			IsImportingPlaytime = false;
		}
	}
}
