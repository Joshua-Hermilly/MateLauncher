using System.Text.RegularExpressions;
using System.Windows.Media;
using MateLauncher.Core.Models;

namespace MateLauncher.App.Themes;

public partial class AppTheme
{
	public string Id { get; }
	public string Name { get; }
	public string Background { get; }
	public string Sidebar { get; }
	public string Card { get; }
	public string CardHover { get; }
	public string Border { get; }
	public string Accent { get; }
	public string Text { get; }
	public string Muted { get; }
	public bool IsCustom { get; }

	public Brush BackgroundPreview => ToBrush( Background );
	public Brush SidebarPreview => ToBrush( Sidebar );
	public Brush CardPreview => ToBrush( Card );
	public Brush AccentPreview => ToBrush( Accent );

	public AppTheme( string id, string name, string background, string sidebar, string card, string cardHover, string border, string accent, string text, string muted, bool isCustom = false )
	{
		Id = id;
		Name = name;
		Background = background;
		Sidebar = sidebar;
		Card = card;
		CardHover = cardHover;
		Border = border;
		Accent = accent;
		Text = text;
		Muted = muted;
		IsCustom = isCustom;
	}

	[GeneratedRegex( "^#?([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$" )]
	private static partial Regex HexPattern();

	public static bool IsValidColor( string? hex )
	{
		return hex is not null && HexPattern().IsMatch( hex.Trim() );
	}

	public static string NormalizeColor( string hex )
	{
		string value = hex.Trim().TrimStart( '#' );
		return "#" + value.ToUpperInvariant();
	}

	public static AppTheme? FromCustom( CustomTheme custom )
	{
		string[] colors = { custom.Background, custom.Sidebar, custom.Card, custom.CardHover, custom.Border, custom.Accent, custom.Text, custom.Muted };

		if ( string.IsNullOrWhiteSpace( custom.Id ) || colors.Any( color => !IsValidColor( color ) ) )
		{
			return null;
		}

		return new AppTheme( custom.Id, string.IsNullOrWhiteSpace( custom.Name ) ? custom.Id : custom.Name, NormalizeColor( custom.Background ), NormalizeColor( custom.Sidebar ), NormalizeColor( custom.Card ), NormalizeColor( custom.CardHover ), NormalizeColor( custom.Border ), NormalizeColor( custom.Accent ), NormalizeColor( custom.Text ), NormalizeColor( custom.Muted ), true );
	}

	public CustomTheme ToCustom()
	{
		return new CustomTheme { Id = Id, Name = Name, Background = Background, Sidebar = Sidebar, Card = Card, CardHover = CardHover, Border = Border, Accent = Accent, Text = Text, Muted = Muted };
	}

	public static Brush ToBrush( string hex )
	{
		SolidColorBrush brush = new SolidColorBrush( ( Color ) ColorConverter.ConvertFromString( hex ) );
		brush.Freeze();
		return brush;
	}
}
