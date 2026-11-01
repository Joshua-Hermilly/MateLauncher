using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MateLauncher.Core.Models;
using MateLauncher.Core.Services;

namespace MateLauncher.App.ViewModels;

public record SessionItemViewModel( string Text );

public partial class MainViewModel
{
	private const int SessionsAnalyzed = 500;
	private const int SessionsShown = 5;

	public ObservableCollection<SessionItemViewModel> RecentSessions { get; } = new ObservableCollection<SessionItemViewModel>();

	[ObservableProperty]
	private bool _HasSessions;

	[ObservableProperty]
	private string _SessionsSummary = string.Empty;

	private async Task LoadSessionsAsync( GameCardViewModel? card )
	{
		RecentSessions.Clear();
		HasSessions = false;
		SessionsSummary = string.Empty;

		if ( card is null )
		{
			return;
		}

		IReadOnlyList<GameSession> sessions = await _Repository.GetSessionsAsync( card.Game.Id, SessionsAnalyzed );

		if ( SelectedGame != card || sessions.Count == 0 )
		{
			return;
		}

		SessionSummary summary = SessionStatistics.Compute( sessions );
		SessionsSummary = string.Format( T( "Sessions_Summary" ), summary.Count, GameCardViewModel.FormatDuration( summary.AverageSeconds ), GameCardViewModel.FormatDuration( summary.LongestSeconds ) );

		foreach ( GameSession session in sessions.Take( SessionsShown ) )
		{
			RecentSessions.Add( new SessionItemViewModel( $"{session.StartedAt:dd/MM/yyyy HH:mm}  ·  {GameCardViewModel.FormatDuration( session.Seconds )}" ) );
		}

		HasSessions = true;
	}
}
