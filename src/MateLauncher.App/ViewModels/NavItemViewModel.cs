using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MateLauncher.App.Localization;
using MateLauncher.Core.Models;

namespace MateLauncher.App.ViewModels;

public enum NavKind
{
	All,
	Favorites,
	Recent,
	Platform,
	Group,
	Category,
	Unplayed,
	Large,
	Stats,
	Downloads,
	News,
	Friends,
	Home
}

public partial class NavItemViewModel : ObservableObject
{
	public string? TitleKey { get; }
	public string Glyph { get; }
	public NavKind Kind { get; }
	public GamePlatform? Platform { get; }
	public ImageSource? Icon { get; init; }
	public ICommand? RenameCommand { get; init; }
	public ICommand? DeleteCommand { get; init; }
	public ICommand? ChangeIconCommand { get; init; }
	public ICommand? EditCommand { get; init; }
	public ICommand? OpenCommand { get; init; }
	public bool IsMerged { get; init; }
	public ICommand? ColorCommand { get; init; }
	public Brush? ColorBrush { get; init; }
	public bool HasColor => ColorBrush is not null;
	public bool CanChangeColor => ColorCommand is not null;
	public bool HasIcon => Icon is not null;
	public bool CanEdit => RenameCommand is not null || EditCommand is not null || ColorCommand is not null;
	public bool CanRename => RenameCommand is not null;
	public bool CanEditDetails => EditCommand is not null;
	public bool CanOpen => OpenCommand is not null;
	public bool CanChangeIcon => ChangeIconCommand is not null;

	[ObservableProperty]
	private string _Title;

	[ObservableProperty]
	private int _Count;

	public NavItemViewModel( string title, string glyph, NavKind kind, GamePlatform? platform = null, string? titleKey = null )
	{
		TitleKey = titleKey;
		_Title = titleKey is null ? title : Localizer.Instance.Get( titleKey );
		Glyph = glyph;
		Kind = kind;
		Platform = platform;
	}

	public void RefreshTitle()
	{
		if ( TitleKey is not null )
		{
			Title = Localizer.Instance.Get( TitleKey );
		}
	}
}
