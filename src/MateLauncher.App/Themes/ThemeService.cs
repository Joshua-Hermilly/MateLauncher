using System.Windows;
using System.Windows.Media;
using MateLauncher.Core.Models;

namespace MateLauncher.App.Themes;

public class ThemeService
{
	public const string DefaultThemeId = "matezone";

	private readonly List<AppTheme> _Themes = new List<AppTheme>
	{
		new AppTheme( "matezone", "MateZone", "#12161D", "#0D1015", "#1A2029", "#232B37", "#2A313C", "#FF3B30", "#E8ECF1", "#9BA5B3" ),
		new AppTheme( "violet", "Violet", "#16161B", "#0F0F13", "#1F1F27", "#2A2A35", "#2C2C38", "#A855F7", "#F2F2F7", "#8E8EA0" ),
		new AppTheme( "ocean", "Océan", "#0E1621", "#0A111A", "#162232", "#1E2F45", "#24364D", "#3B9EFF", "#E6EEF8", "#8FA3BA" ),
		new AppTheme( "foret", "Forêt", "#111A16", "#0B120F", "#182620", "#21352B", "#26382F", "#2FD08A", "#E5F2EA", "#8FA89A" ),
		new AppTheme( "graphite", "Graphite", "#151515", "#0D0D0D", "#212121", "#2C2C2C", "#333333", "#FFB020", "#EDEDED", "#9A9A9A" )
	};

	public IReadOnlyList<AppTheme> Themes => _Themes;

	public IReadOnlyList<string> AccentColors { get; } = new List<string> { "#FF3B30", "#FF5CA8", "#A855F7", "#3B9EFF", "#19D3DA", "#2FD08A", "#FFB020" };

	public AppTheme GetTheme( string themeId )
	{
		return _Themes.FirstOrDefault( theme => theme.Id == themeId ) ?? _Themes[ 0 ];
	}

	public void SetCustomThemes( IEnumerable<CustomTheme> customThemes )
	{
		_Themes.RemoveAll( theme => theme.IsCustom );

		foreach ( CustomTheme custom in customThemes )
		{
			AppTheme? theme = AppTheme.FromCustom( custom );

			if ( theme is not null && _Themes.All( existing => existing.Id != theme.Id ) )
			{
				_Themes.Add( theme );
			}
		}
	}

	public IReadOnlyList<CustomTheme> GetCustomThemes()
	{
		return _Themes.Where( theme => theme.IsCustom ).Select( theme => theme.ToCustom() ).ToList();
	}

	public void SaveCustomTheme( AppTheme theme )
	{
		int index = _Themes.FindIndex( existing => existing.Id == theme.Id );

		if ( index >= 0 )
		{
			_Themes[ index ] = theme;
		}
		else
		{
			_Themes.Add( theme );
		}
	}

	public void RemoveCustomTheme( string themeId )
	{
		_Themes.RemoveAll( theme => theme.IsCustom && theme.Id == themeId );
	}

	public void Apply( string themeId, string? accentOverride )
	{
		AppTheme theme = GetTheme( themeId );
		string accent = string.IsNullOrWhiteSpace( accentOverride ) ? theme.Accent : accentOverride;
		ResourceDictionary resources = Application.Current.Resources;

		resources[ "BackgroundColor" ] = ( Color ) ColorConverter.ConvertFromString( theme.Background );
		resources[ "AccentColor" ] = ( Color ) ColorConverter.ConvertFromString( accent );
		resources[ "BackgroundBrush" ] = AppTheme.ToBrush( theme.Background );
		resources[ "SidebarBrush" ] = AppTheme.ToBrush( theme.Sidebar );
		resources[ "CardBrush" ] = AppTheme.ToBrush( theme.Card );
		resources[ "CardHoverBrush" ] = AppTheme.ToBrush( theme.CardHover );
		resources[ "BorderBrush" ] = AppTheme.ToBrush( theme.Border );
		resources[ "AccentBrush" ] = AppTheme.ToBrush( accent );
		resources[ "TextBrush" ] = AppTheme.ToBrush( theme.Text );
		resources[ "MutedTextBrush" ] = AppTheme.ToBrush( theme.Muted );
	}
}
