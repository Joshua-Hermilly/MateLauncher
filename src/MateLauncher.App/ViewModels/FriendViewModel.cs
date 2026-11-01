using System.Windows.Media;
using MateLauncher.App.Localization;
using MateLauncher.Infrastructure.Friends;

namespace MateLauncher.App.ViewModels;

public class FriendViewModel
{
	public string SteamId { get; }
	public string Name { get; }
	public string? AvatarUrl { get; }
	public bool HasAvatar => AvatarUrl is not null;
	public FriendState State { get; }
	public string? GameName { get; }
	public string? GameId { get; }
	public string StatusText { get; }
	public Brush StatusBrush { get; }
	public string ChatUri => $"steam://friends/message/{SteamId}";
	public string ProfileUri => $"https://steamcommunity.com/profiles/{SteamId}";

	public FriendViewModel( SteamFriend friend )
	{
		SteamId = friend.SteamId;
		Name = friend.Name;
		AvatarUrl = friend.AvatarUrl;
		State = friend.State;
		GameName = friend.GameName;
		GameId = friend.GameId;
		StatusText = friend.State switch
		{
			FriendState.InGame => string.Format( Localizer.Instance.Get( "Friends_InGame" ), friend.GameName ?? friend.GameId ?? string.Empty ),
			FriendState.Online => Localizer.Instance.Get( "Friends_Online" ),
			_ => Localizer.Instance.Get( "Friends_Offline" )
		};
		StatusBrush = new SolidColorBrush( friend.State switch
		{
			FriendState.InGame => Color.FromRgb( 0x5D, 0xD3, 0x5D ),
			FriendState.Online => Color.FromRgb( 0x66, 0xC0, 0xF4 ),
			_ => Color.FromRgb( 0x7A, 0x82, 0x8F )
		} );
		StatusBrush.Freeze();
	}
}
