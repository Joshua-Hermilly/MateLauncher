using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MateLauncher.App.ViewModels;

public partial class MainViewModel
{
	private const int HomeRowSize = 12;

	public ObservableCollection<GameCardViewModel> HomeContinue { get; } = new ObservableCollection<GameCardViewModel>();
	public ObservableCollection<GameCardViewModel> HomeBacklog { get; } = new ObservableCollection<GameCardViewModel>();
	public ObservableCollection<GameCardViewModel> HomeFavorites { get; } = new ObservableCollection<GameCardViewModel>();

	[ObservableProperty]
	private bool _IsHomeOpen;

	[ObservableProperty]
	private GameCardViewModel? _HomeHero;

	[ObservableProperty]
	private bool _HasHomeHero;

	[ObservableProperty]
	private string _HomeHeroText = string.Empty;

	public bool IsHomeVisible => IsHomeOpen && SelectedGame is null;

	public bool HasHomeContinue => HomeContinue.Count > 0;
	public bool HasHomeBacklog => HomeBacklog.Count > 0;
	public bool HasHomeFavorites => HomeFavorites.Count > 0;

	private void RefreshHome()
	{
		List<GameCardViewModel> visible = _AllCards.Where( card => !card.Game.IsHidden ).ToList();
		List<GameCardViewModel> played = visible.Where( card => card.LastPlayedAt is not null ).OrderByDescending( card => card.LastPlayedAt ).ToList();

		HomeHero = played.FirstOrDefault();
		HasHomeHero = HomeHero is not null;
		HomeHeroText = HomeHero is null ? string.Empty : string.Format( T( "Home_LastSession" ), HomeHero.LastPlayedText, HomeHero.PlaytimeText );

		Fill( HomeContinue, played.Skip( 1 ).Take( HomeRowSize ) );
		Fill( HomeBacklog, visible.Where( card => card.PlaytimeSeconds == 0 && card.LastPlayedAt is null ).OrderBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).Take( HomeRowSize ) );
		Fill( HomeFavorites, visible.Where( card => card.IsFavorite ).OrderByDescending( card => card.LastPlayedAt ).ThenBy( card => card.Name, StringComparer.CurrentCultureIgnoreCase ).Take( HomeRowSize ) );

		OnPropertyChanged( nameof( HasHomeContinue ) );
		OnPropertyChanged( nameof( HasHomeBacklog ) );
		OnPropertyChanged( nameof( HasHomeFavorites ) );
	}

	private static void Fill( ObservableCollection<GameCardViewModel> target, IEnumerable<GameCardViewModel> source )
	{
		target.Clear();

		foreach ( GameCardViewModel card in source )
		{
			target.Add( card );
		}
	}
}
