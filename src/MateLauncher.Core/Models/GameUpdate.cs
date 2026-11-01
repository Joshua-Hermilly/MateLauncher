namespace MateLauncher.Core.Models;

public record GameUpdate( string AppId, string GameName, GamePlatform Platform, long BytesToDownload, long BytesDownloaded, bool IsInstalled, long StagingBytes = 0 );
