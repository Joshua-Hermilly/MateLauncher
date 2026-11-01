namespace MateLauncher.Core.Models;

public record GameMetadata( string Name, string Summary, IReadOnlyList<string> Genres, DateTime? ReleaseDate, double? Rating, IReadOnlyList<string> ScreenshotUrls, int? TimeToBeatSeconds = null );
