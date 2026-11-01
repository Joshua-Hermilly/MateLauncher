using System.Windows;
using System.Windows.Media;
using MateLauncher.Core.Models;

namespace MateLauncher.App.ViewModels;

public record DayLabel( double X, string Text );

public record ChartDot( double X, double Y, string Tooltip );

public record GridLine( double Y, double Width, string Label );

public record TopGameItem( string Name, string PlaytimeText, double Ratio, Brush Brush );

public record PlatformSlice( Geometry Geometry, Brush Brush, string Label, string PercentText );

public class StatsViewModel
{
	public const double ChartHeight = 200;
	private const double LeftPadding = 70;
	private const double RightPadding = 18;
	private const double TopPadding = 14;
	private const double BottomPadding = 12;
	private const int DayCount = 14;
	private const double MinimumScaleSeconds = 600;

	public double ChartWidth { get; private init; } = 640;
	public bool HasData { get; private init; }
	public string TotalText { get; private init; } = string.Empty;
	public string GamesCountText { get; private init; } = string.Empty;
	public string PlayedCountText { get; private init; } = string.Empty;
	public string TodayText { get; private init; } = string.Empty;
	public PointCollection CurvePoints { get; private init; } = new PointCollection();
	public PointCollection AreaPoints { get; private init; } = new PointCollection();
	public IReadOnlyList<DayLabel> DayLabels { get; private init; } = new List<DayLabel>();
	public IReadOnlyList<ChartDot> Dots { get; private init; } = new List<ChartDot>();
	public IReadOnlyList<GridLine> GridLines { get; private init; } = new List<GridLine>();
	public IReadOnlyList<TopGameItem> TopGames { get; private init; } = new List<TopGameItem>();
	public IReadOnlyList<PlatformSlice> Slices { get; private init; } = new List<PlatformSlice>();

	public static StatsViewModel Build( IReadOnlyList<GameCardViewModel> cards, IReadOnlyList<DailyPlaytime> dailyPlaytimes, double chartWidth )
	{
		double width = Math.Max( 320, chartWidth );
		long totalSeconds = cards.Sum( card => card.PlaytimeSeconds );
		DateTime today = DateTime.Now.Date;
		double[] perDay = new double[ DayCount ];

		foreach ( DailyPlaytime daily in dailyPlaytimes )
		{
			if ( DateTime.TryParse( daily.Day, out DateTime day ) )
			{
				int index = DayCount - 1 - ( int ) ( today - day.Date ).TotalDays;

				if ( index >= 0 && index < DayCount )
				{
					perDay[ index ] += daily.Seconds;
				}
			}
		}

		double scaleMaximum = Math.Max( MinimumScaleSeconds, RoundUpScale( perDay.Max() ) );
		double plotHeight = ChartHeight - TopPadding - BottomPadding;
		double baselineY = ChartHeight - BottomPadding;
		double step = ( width - LeftPadding - RightPadding ) / ( DayCount - 1 );
		PointCollection curve = new PointCollection();
		PointCollection area = new PointCollection { new Point( LeftPadding, baselineY ) };
		List<ChartDot> dots = new List<ChartDot>();
		List<DayLabel> labels = new List<DayLabel>();

		for ( int index = 0; index < DayCount; index++ )
		{
			double x = LeftPadding + index * step;
			double y = baselineY - perDay[ index ] / scaleMaximum * plotHeight;
			DateTime day = today.AddDays( index - ( DayCount - 1 ) );

			curve.Add( new Point( x, y ) );
			area.Add( new Point( x, y ) );
			dots.Add( new ChartDot( x, y, $"{day:dd/MM} · {GameCardViewModel.FormatDuration( ( long ) perDay[ index ] )}" ) );

			if ( index % 2 == 0 || index == DayCount - 1 )
			{
				labels.Add( new DayLabel( x, day.ToString( "dd/MM" ) ) );
			}
		}

		area.Add( new Point( LeftPadding + ( DayCount - 1 ) * step, baselineY ) );
		curve.Freeze();
		area.Freeze();

		List<GridLine> gridLines = new List<GridLine>();

		for ( int line = 0; line <= 3; line++ )
		{
			double ratio = line / 3.0;
			gridLines.Add( new GridLine( baselineY - ratio * plotHeight, width - LeftPadding - RightPadding, FormatAxis( scaleMaximum * ratio ) ) );
		}

		double topMaximum = cards.Count == 0 ? 0 : cards.Max( card => card.PlaytimeSeconds );
		List<TopGameItem> topGames = cards
			.Where( card => card.PlaytimeSeconds > 0 )
			.OrderByDescending( card => card.PlaytimeSeconds )
			.Take( 6 )
			.Select( card => new TopGameItem( card.Name, card.PlaytimeText, Math.Max( 3, card.PlaytimeSeconds / topMaximum * 100 ), card.PlatformBrush ) )
			.ToList();

		return new StatsViewModel
		{
			ChartWidth = width,
			HasData = totalSeconds > 0,
			TotalText = GameCardViewModel.FormatDuration( totalSeconds ),
			GamesCountText = cards.Count.ToString(),
			PlayedCountText = cards.Count( card => card.PlaytimeSeconds > 0 ).ToString(),
			TodayText = GameCardViewModel.FormatDuration( ( long ) perDay[ DayCount - 1 ] ),
			CurvePoints = curve,
			AreaPoints = area,
			DayLabels = labels,
			Dots = dots,
			GridLines = gridLines,
			TopGames = topGames,
			Slices = BuildSlices( cards, totalSeconds )
		};
	}

	private static double RoundUpScale( double seconds )
	{
		double[] steps = { 600, 1800, 3600, 7200, 14400, 21600, 43200, 86400 };
		return steps.FirstOrDefault( step => step >= seconds, Math.Ceiling( seconds / 3600 ) * 3600 );
	}

	private static string FormatAxis( double seconds )
	{
		if ( seconds <= 0 )
		{
			return "0 min";
		}

		TimeSpan duration = TimeSpan.FromSeconds( seconds );
		return duration.TotalHours < 1 ? $"{( int ) duration.TotalMinutes} min" : duration.Minutes == 0 ? $"{( int ) duration.TotalHours} h" : $"{( int ) duration.TotalHours} h {duration.Minutes:00}";
	}

	private static List<PlatformSlice> BuildSlices( IReadOnlyList<GameCardViewModel> cards, long totalSeconds )
	{
		List<PlatformSlice> slices = new List<PlatformSlice>();

		if ( totalSeconds <= 0 )
		{
			return slices;
		}

		const double center = 90;
		const double radius = 66;
		double startAngle = -90;

		foreach ( IGrouping<GamePlatform, GameCardViewModel> group in cards.Where( card => card.PlaytimeSeconds > 0 ).GroupBy( card => card.Game.Platform ).OrderByDescending( group => group.Sum( card => card.PlaytimeSeconds ) ) )
		{
			double share = group.Sum( card => card.PlaytimeSeconds ) / ( double ) totalSeconds;
			double sweep = Math.Min( share * 360, 359.9 );
			double endAngle = startAngle + sweep;

			StreamGeometry geometry = new StreamGeometry();

			using ( StreamGeometryContext context = geometry.Open() )
			{
				context.BeginFigure( PointOnCircle( center, radius, startAngle ), false, false );
				context.ArcTo( PointOnCircle( center, radius, endAngle ), new Size( radius, radius ), 0, sweep > 180, SweepDirection.Clockwise, true, false );
			}

			geometry.Freeze();
			slices.Add( new PlatformSlice( geometry, group.First().PlatformBrush, GameCardViewModel.GetPlatformLabel( group.Key ), $"{share:P0}" ) );
			startAngle = endAngle;
		}

		return slices;
	}

	private static Point PointOnCircle( double center, double radius, double angleDegrees )
	{
		double radians = angleDegrees * Math.PI / 180;
		return new Point( center + radius * Math.Cos( radians ), center + radius * Math.Sin( radians ) );
	}
}
