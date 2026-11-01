using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MateLauncher.Core.Models;

namespace MateLauncher.App.Icons;

public static class GeneratedCoverFactory
{
	private const int Width = 600;
	private const int Height = 900;

	private static readonly Dictionary<string, ( string Top, string Bottom, string Accent )> _NamedPalettes = new Dictionary<string, ( string, string, string )>( StringComparer.OrdinalIgnoreCase )
	{
		[ "VALORANT" ] = ( "#FF4655", "#0F1923", "#FFFFFF" ),
		[ "League of Legends" ] = ( "#1E3A5F", "#0A1428", "#C8AA6E" ),
		[ "Teamfight Tactics" ] = ( "#3B2A7A", "#0E1330", "#F2C94C" ),
		[ "Legends of Runeterra" ] = ( "#2A6FB5", "#0A1A33", "#E3C16F" ),
		[ "2XKO" ] = ( "#8A2BE2", "#10051F", "#FF5CA8" )
	};

	public static string Create( string name, GamePlatform platform )
	{
		( Color top, Color bottom, Color accent ) = GetPalette( name, platform );
		DrawingVisual visual = new DrawingVisual();

		using ( DrawingContext context = visual.RenderOpen() )
		{
			LinearGradientBrush background = new LinearGradientBrush( top, bottom, new Point( 0.2, 0 ), new Point( 0.8, 1 ) );
			context.DrawRectangle( background, null, new Rect( 0, 0, Width, Height ) );
			context.DrawEllipse( new SolidColorBrush( Color.FromArgb( 34, 255, 255, 255 ) ), null, new Point( Width, 40 ), 380, 380 );
			context.DrawEllipse( new SolidColorBrush( Color.FromArgb( 22, 0, 0, 0 ) ), null, new Point( 20, Height - 40 ), 340, 340 );

			ImageSource? icon = PlatformIcons.Get( platform );

			if ( icon is not null )
			{
				context.PushOpacity( 0.9 );
				context.DrawImage( icon, new Rect( 48, 48, 88, 88 ) );
				context.Pop();
			}

			FormattedText title = BuildTitle( name );
			double titleTop = Height - 130 - title.Height;
			context.DrawRectangle( new SolidColorBrush( accent ), null, new Rect( 48, titleTop - 26, 96, 6 ) );
			context.DrawText( title, new Point( 48, titleTop ) );

			FormattedText footer = new FormattedText( GameFooter( platform ), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface( "Segoe UI" ), 22, new SolidColorBrush( Color.FromArgb( 200, 255, 255, 255 ) ), 1.0 );
			context.DrawText( footer, new Point( 48, Height - 90 ) );
		}

		RenderTargetBitmap bitmap = new RenderTargetBitmap( Width, Height, 96, 96, PixelFormats.Pbgra32 );
		bitmap.Render( visual );
		PngBitmapEncoder encoder = new PngBitmapEncoder();
		encoder.Frames.Add( BitmapFrame.Create( bitmap ) );

		string path = Path.Combine( Path.GetTempPath(), $"matelauncher-cover-{Guid.NewGuid():N}.png" );

		using ( FileStream stream = File.Create( path ) )
		{
			encoder.Save( stream );
		}

		return path;
	}

	private static FormattedText BuildTitle( string name )
	{
		double size = name.Length switch
		{
			<= 10 => 76,
			<= 18 => 62,
			<= 28 => 50,
			_ => 40
		};

		return new FormattedText( name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface( new FontFamily( "Segoe UI Variable Display, Segoe UI" ), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal ), size, Brushes.White, 1.0 )
		{
			MaxTextWidth = Width - 96,
			MaxTextHeight = 420,
			Trimming = TextTrimming.CharacterEllipsis
		};
	}

	private static string GameFooter( GamePlatform platform )
	{
		return platform switch
		{
			GamePlatform.Riot => "RIOT GAMES",
			GamePlatform.Xbox => "XBOX",
			_ => platform.ToString().ToUpperInvariant()
		};
	}

	private static ( Color Top, Color Bottom, Color Accent ) GetPalette( string name, GamePlatform platform )
	{
		if ( _NamedPalettes.TryGetValue( name, out ( string Top, string Bottom, string Accent ) named ) )
		{
			return ( Parse( named.Top ), Parse( named.Bottom ), Parse( named.Accent ) );
		}

		return platform switch
		{
			GamePlatform.Xbox => ( Parse( "#107C10" ), Parse( "#031A03" ), Parse( "#9BF00B" ) ),
			GamePlatform.Riot => ( Parse( "#EB0029" ), Parse( "#1A0508" ), Parse( "#FFFFFF" ) ),
			_ => ( Parse( "#3B82F6" ), Parse( "#0B1020" ), Parse( "#FFFFFF" ) )
		};
	}

	private static Color Parse( string hex )
	{
		return ( Color ) ColorConverter.ConvertFromString( hex );
	}
}
