using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MateLauncher.App.Localization;
using MateLauncher.Core.Models;

namespace MateLauncher.App.ViewModels;

public partial class GameCardViewModel : ObservableObject
{
	public Game Game { get; }
	public string Name => Game.Name;
	public bool IsManual => Game.Platform == GamePlatform.Manual;
	public string PlatformLabel { get; }
	public Brush PlatformBrush { get; }
	public string Initials { get; }
	public Brush PlaceholderBrush { get; }

	public string InstallPath => Game.InstallPath;
	public bool HasInstallPath => Directory.Exists( Game.InstallPath );

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( HasNoCover ) )]
	private string? _CoverImagePath;

	public bool HasNoCover => string.IsNullOrEmpty( CoverImagePath );

	[ObservableProperty]
	private bool _IsFavorite;

	[ObservableProperty]
	private bool _IsRunning;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( LastPlayedText ) )]
	private DateTime? _LastPlayedAt;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( PlaytimeText ) )]
	private long _PlaytimeSeconds;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( SizeText ) )]
	private long _SizeBytes;

	public string CategoryName => Game.CategoryName ?? string.Empty;

	public string SizeText => SizeBytes <= 0 ? Localizer.Instance.Get( "Text_Unknown" ) : FormatSize( SizeBytes );

	public string LastPlayedText => LastPlayedAt is null ? Localizer.Instance.Get( "Text_Never" ) : LastPlayedAt.Value.ToString( "dd/MM/yyyy HH:mm" );

	public static string FormatSize( long bytes )
	{
		string[] unitKeys = { "Unit_Bytes", "Unit_Kilo", "Unit_Mega", "Unit_Giga" };
		double value = bytes;
		int unitIndex = 0;

		while ( value >= 1024 && unitIndex < unitKeys.Length - 1 )
		{
			value /= 1024;
			unitIndex++;
		}

		return $"{value:0.#} {Localizer.Instance.Get( unitKeys[ unitIndex ] )}";
	}

	public static string FormatDuration( long seconds )
	{
		TimeSpan duration = TimeSpan.FromSeconds( seconds );

		if ( duration.TotalMinutes < 1 )
		{
			return seconds == 0 ? Localizer.Instance.Get( "Text_None" ) : Localizer.Instance.Get( "Text_LessThanMinute" );
		}

		return duration.TotalHours < 1 ? $"{duration.Minutes} min" : $"{( int ) duration.TotalHours} h {duration.Minutes:00} min";
	}

	public string PlaytimeText => FormatDuration( PlaytimeSeconds );

	public GameCardViewModel( Game game )
	{
		Game = game;
		CoverImagePath = Game.IsRemoteCover( game.CoverImagePath ) ? null : game.CoverImagePath;
		IsFavorite = game.IsFavorite;
		LastPlayedAt = game.LastPlayedAt;
		PlaytimeSeconds = game.PlaytimeSeconds;
		SizeBytes = game.SizeBytes;
		PlatformLabel = string.IsNullOrWhiteSpace( game.GroupName ) ? GetPlatformLabel( game.Platform ) : game.GroupName;
		PlatformBrush = new SolidColorBrush( GetPlatformColor( game.Platform ) );
		Initials = BuildInitials( game.Name );
		PlaceholderBrush = BuildPlaceholderBrush( game.Name );
	}

	public static string GetPlatformLabel( GamePlatform platform )
	{
		return platform switch
		{
			GamePlatform.Steam => "Steam",
			GamePlatform.Epic => "Epic Games",
			GamePlatform.Gog => "GOG",
			GamePlatform.EaApp => "EA App",
			GamePlatform.UbisoftConnect => "Ubisoft Connect",
			GamePlatform.BattleNet => "Battle.net",
			GamePlatform.Xbox => "Xbox",
			GamePlatform.Riot => "Riot Games",
			GamePlatform.Folder => Localizer.Instance.Get( "Platform_Folder" ),
			_ => Localizer.Instance.Get( "Platform_Manual" )
		};
	}

	private static Color GetPlatformColor( GamePlatform platform )
	{
		return platform switch
		{
			GamePlatform.Steam => Color.FromRgb( 0x66, 0xC0, 0xF4 ),
			GamePlatform.Epic => Color.FromRgb( 0xE8, 0xE8, 0xE8 ),
			GamePlatform.Gog => Color.FromRgb( 0xC0, 0x6B, 0xD1 ),
			GamePlatform.EaApp => Color.FromRgb( 0xFF, 0x6B, 0x6B ),
			GamePlatform.UbisoftConnect => Color.FromRgb( 0x4D, 0xA6, 0xFF ),
			GamePlatform.BattleNet => Color.FromRgb( 0x4D, 0x9B, 0xFF ),
			GamePlatform.Xbox => Color.FromRgb( 0x5D, 0xD3, 0x5D ),
			GamePlatform.Riot => Color.FromRgb( 0xEB, 0x00, 0x29 ),
			GamePlatform.Folder => Color.FromRgb( 0xFF, 0xB0, 0x20 ),
			_ => Color.FromRgb( 0xB0, 0xB0, 0xC0 )
		};
	}

	private static string BuildInitials( string name )
	{
		string[] words = name.Split( ' ', StringSplitOptions.RemoveEmptyEntries ).Where( word => char.IsLetterOrDigit( word[ 0 ] ) ).Take( 2 ).ToArray();
		return string.Concat( words.Select( word => char.ToUpperInvariant( word[ 0 ] ) ) );
	}

	private static Brush BuildPlaceholderBrush( string name )
	{
		int hash = name.Aggregate( 17, ( current, character ) => unchecked( current * 31 + character ) );
		double hue = Math.Abs( hash % 360 );

		LinearGradientBrush brush = new LinearGradientBrush( FromHsv( hue, 0.55, 0.45 ), FromHsv( ( hue + 40 ) % 360, 0.6, 0.18 ), 60 );
		brush.Freeze();
		return brush;
	}

	private static Color FromHsv( double hue, double saturation, double value )
	{
		double chroma = value * saturation;
		double x = chroma * ( 1 - Math.Abs( hue / 60 % 2 - 1 ) );
		double match = value - chroma;

		( double red, double green, double blue ) = ( int ) ( hue / 60 ) switch
		{
			0 => ( chroma, x, 0.0 ),
			1 => ( x, chroma, 0.0 ),
			2 => ( 0.0, chroma, x ),
			3 => ( 0.0, x, chroma ),
			4 => ( x, 0.0, chroma ),
			_ => ( chroma, 0.0, x )
		};

		return Color.FromRgb( ( byte ) ( ( red + match ) * 255 ), ( byte ) ( ( green + match ) * 255 ), ( byte ) ( ( blue + match ) * 255 ) );
	}
}
