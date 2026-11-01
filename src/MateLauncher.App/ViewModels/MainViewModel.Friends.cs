using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MateLauncher.App.Localization;
using MateLauncher.Core.Models;
using MateLauncher.Infrastructure.Friends;

namespace MateLauncher.App.ViewModels;

public partial class MainViewModel
{
	private const int MaximumFriendsChecked = 150;
	private const int MaximumOwnerNames = 6;
	private static readonly TimeSpan _FriendsCacheLifetime = TimeSpan.FromMinutes( 5 );
	private static readonly TimeSpan _OwnedGamesCacheLifetime = TimeSpan.FromMinutes( 15 );

	private readonly SteamFriendsService _FriendsService = new SteamFriendsService();
	private readonly Dictionary<string, ( DateTime LoadedAt, IReadOnlySet<string>? AppIds )> _OwnedGamesCache = new Dictionary<string, ( DateTime LoadedAt, IReadOnlySet<string>? AppIds )>();
	private IReadOnlyList<SteamFriend> _FriendList = Array.Empty<SteamFriend>();
	private DateTime _FriendListLoadedAt = DateTime.MinValue;
	private string? _FriendsErrorKey;

	public ObservableCollection<FriendViewModel> Friends { get; } = new ObservableCollection<FriendViewModel>();

	[ObservableProperty]
	private bool _IsFriendsOpen;

	[ObservableProperty]
	private bool _HasNoFriends = true;

	[ObservableProperty]
	private string _FriendsStatus = string.Empty;

	[ObservableProperty]
	private bool _HasGameFriends;

	[ObservableProperty]
	private string _GameFriendsText = string.Empty;

	[RelayCommand]
	public async Task RefreshFriendsAsync()
	{
		_FriendListLoadedAt = DateTime.MinValue;
		_OwnedGamesCache.Clear();
		await LoadFriendsAsync();
		await LoadGameFriendsAsync( SelectedGame );
	}

	private bool HasSteamCredentials => !string.IsNullOrWhiteSpace( SteamApiKey ) && !string.IsNullOrWhiteSpace( SteamId );

	private async Task<IReadOnlyList<SteamFriend>> LoadFriendsAsync()
	{
		if ( !HasSteamCredentials )
		{
			Friends.Clear();
			HasNoFriends = true;
			FriendsStatus = T( "Friends_NeedKey" );
			return Array.Empty<SteamFriend>();
		}

		if ( DateTime.UtcNow - _FriendListLoadedAt < _FriendsCacheLifetime && _FriendsErrorKey is null )
		{
			return _FriendList;
		}

		FriendsStatus = T( "Friends_Loading" );
		FriendsResult result = await _FriendsService.GetFriendsAsync( SteamApiKey, SteamId );
		_FriendsErrorKey = result.ErrorKey;
		_FriendList = result.Friends;
		_FriendListLoadedAt = result.ErrorKey is null ? DateTime.UtcNow : DateTime.MinValue;

		Friends.Clear();

		foreach ( SteamFriend friend in result.Friends )
		{
			Friends.Add( new FriendViewModel( friend ) );
		}

		HasNoFriends = Friends.Count == 0;
		FriendsStatus = result.ErrorKey is not null
			? T( result.ErrorKey )
			: string.Format( T( "Friends_Summary" ), result.Friends.Count( friend => friend.State == FriendState.Online ), result.Friends.Count( friend => friend.State == FriendState.InGame ), result.Friends.Count );
		return result.Friends;
	}

	private async Task LoadGameFriendsAsync( GameCardViewModel? card )
	{
		HasGameFriends = false;
		GameFriendsText = string.Empty;

		if ( card is null || card.Game.Platform != GamePlatform.Steam || !HasSteamCredentials || !card.Game.PlatformGameId.All( char.IsDigit ) )
		{
			return;
		}

		IReadOnlyList<SteamFriend> friends = await LoadFriendsAsync();

		if ( friends.Count == 0 || SelectedGame != card )
		{
			return;
		}

		string appId = card.Game.PlatformGameId;
		List<string> playingNow = friends.Where( friend => friend.GameId == appId ).Select( friend => friend.Name ).ToList();
		List<string> owners = await FindOwnersAsync( friends.Take( MaximumFriendsChecked ).ToList(), appId );

		if ( SelectedGame != card )
		{
			return;
		}

		List<string> lines = new List<string>();

		if ( playingNow.Count > 0 )
		{
			lines.Add( string.Format( T( "Friends_PlayingNow" ), FormatNames( playingNow ) ) );
		}

		if ( owners.Count > 0 )
		{
			lines.Add( string.Format( T( "Friends_Owners" ), owners.Count, FormatNames( owners ) ) );
		}

		GameFriendsText = string.Join( Environment.NewLine, lines );
		HasGameFriends = lines.Count > 0;
	}

	private async Task<List<string>> FindOwnersAsync( IReadOnlyList<SteamFriend> friends, string appId )
	{
		using SemaphoreSlim limiter = new SemaphoreSlim( 4 );
		List<Task<string?>> checks = friends.Select( async friend =>
		{
			await limiter.WaitAsync();

			try
			{
				IReadOnlySet<string>? appIds = await GetOwnedAppIdsCachedAsync( friend.SteamId );
				return appIds is not null && appIds.Contains( appId ) ? friend.Name : null;
			}
			finally
			{
				limiter.Release();
			}
		} ).ToList();

		string?[] results = await Task.WhenAll( checks );
		return results.Where( name => name is not null ).Select( name => name! ).OrderBy( name => name, StringComparer.CurrentCultureIgnoreCase ).ToList();
	}

	private async Task<IReadOnlySet<string>?> GetOwnedAppIdsCachedAsync( string friendSteamId )
	{
		lock ( _OwnedGamesCache )
		{
			if ( _OwnedGamesCache.TryGetValue( friendSteamId, out ( DateTime LoadedAt, IReadOnlySet<string>? AppIds ) cached ) && DateTime.UtcNow - cached.LoadedAt < _OwnedGamesCacheLifetime )
			{
				return cached.AppIds;
			}
		}

		OwnedGamesResult result = await _FriendsService.GetOwnedAppIdsAsync( SteamApiKey, friendSteamId );

		lock ( _OwnedGamesCache )
		{
			_OwnedGamesCache[ friendSteamId ] = ( DateTime.UtcNow, result.AppIds );
		}

		return result.AppIds;
	}

	private static string FormatNames( IReadOnlyList<string> names )
	{
		string joined = string.Join( ", ", names.Take( MaximumOwnerNames ) );
		return names.Count > MaximumOwnerNames ? $"{joined}…" : joined;
	}
}
