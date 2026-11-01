namespace MateLauncher.Core.Models;

public record NewsEntry( string Source, string Title, string Link, DateTime PublishedAt, string Summary, string? ImageUrl );
