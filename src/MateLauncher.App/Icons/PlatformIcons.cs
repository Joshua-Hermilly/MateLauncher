using System.Runtime.InteropServices;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MateLauncher.Core.Models;
using Microsoft.Win32;

namespace MateLauncher.App.Icons;

public static class PlatformIcons
{
	private static readonly Dictionary<GamePlatform, ImageSource?> _Cache = new Dictionary<GamePlatform, ImageSource?>();

	public static ImageSource? Get( GamePlatform platform )
	{
		if ( _Cache.TryGetValue( platform, out ImageSource? cached ) )
		{
			return cached;
		}

		if ( platform == GamePlatform.Xbox )
		{
			_Cache[ platform ] = CreateXboxLogo();
			return _Cache[ platform ];
		}

		string? executable = FindClientExecutable( platform );
		ImageSource? icon = executable is null ? null : ExtractIcon( executable );
		_Cache[ platform ] = icon;
		return icon;
	}

	private static ImageSource CreateXboxLogo()
	{
		SolidColorBrush green = new SolidColorBrush( Color.FromRgb( 0x10, 0x7C, 0x10 ) );
		Pen whitePen = new Pen( Brushes.White, 2.6 ) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
		DrawingGroup group = new DrawingGroup();
		group.Children.Add( new GeometryDrawing( green, null, new EllipseGeometry( new Point( 10, 10 ), 10, 10 ) ) );
		group.Children.Add( new GeometryDrawing( null, whitePen, new LineGeometry( new Point( 5.6, 5.2 ), new Point( 14.4, 15.4 ) ) ) );
		group.Children.Add( new GeometryDrawing( null, whitePen, new LineGeometry( new Point( 14.4, 5.2 ), new Point( 5.6, 15.4 ) ) ) );
		group.Freeze();

		DrawingImage image = new DrawingImage( group );
		image.Freeze();
		return image;
	}

	public static ImageSource? LoadImage( string? path )
	{
		if ( string.IsNullOrWhiteSpace( path ) || !File.Exists( path ) )
		{
			return null;
		}

		try
		{
			BitmapImage image = new BitmapImage();
			image.BeginInit();
			image.CacheOption = BitmapCacheOption.OnLoad;
			image.UriSource = new Uri( path );
			image.DecodePixelWidth = 64;
			image.EndInit();
			image.Freeze();
			return image;
		}
		catch ( Exception exception ) when ( exception is IOException or NotSupportedException or FileFormatException )
		{
			return null;
		}
	}

	[StructLayout( LayoutKind.Sequential, CharSet = CharSet.Unicode )]
	private struct ShellFileInfo
	{
		public IntPtr IconHandle;
		public int IconIndex;
		public uint Attributes;

		[MarshalAs( UnmanagedType.ByValTStr, SizeConst = 260 )]
		public string DisplayName;

		[MarshalAs( UnmanagedType.ByValTStr, SizeConst = 80 )]
		public string TypeName;
	}

	[DllImport( "shell32.dll", CharSet = CharSet.Unicode )]
	private static extern IntPtr SHGetFileInfo( string path, uint fileAttributes, ref ShellFileInfo info, uint size, uint flags );

	[DllImport( "user32.dll" )]
	private static extern bool DestroyIcon( IntPtr handle );

	private static ImageSource? ExtractIcon( string executable )
	{
		const uint IconFlag = 0x100;
		const uint LargeIconFlag = 0x0;
		ShellFileInfo info = new ShellFileInfo();

		SHGetFileInfo( executable, 0, ref info, ( uint ) Marshal.SizeOf( info ), IconFlag | LargeIconFlag );

		if ( info.IconHandle == IntPtr.Zero )
		{
			return null;
		}

		try
		{
			BitmapSource source = Imaging.CreateBitmapSourceFromHIcon( info.IconHandle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions() );
			source.Freeze();
			return source;
		}
		finally
		{
			DestroyIcon( info.IconHandle );
		}
	}
	private static string? FindClientExecutable( GamePlatform platform )
	{
		string programFiles = Environment.GetFolderPath( Environment.SpecialFolder.ProgramFiles );
		string programFilesX86 = Environment.GetFolderPath( Environment.SpecialFolder.ProgramFilesX86 );

		IEnumerable<string?> candidates = platform switch
		{
			GamePlatform.Steam => new[] { ReadRegistryString( Registry.CurrentUser, @"Software\Valve\Steam", "SteamExe" ), Path.Combine( programFilesX86, @"Steam\steam.exe" ) },
			GamePlatform.Epic => new[]
			{
				Path.Combine( programFilesX86, @"Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe" ),
				Path.Combine( programFiles, @"Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe" ),
				Path.Combine( programFiles, @"Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe" ),
				Path.Combine( programFilesX86, @"Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe" )
			},
			GamePlatform.Gog => new[] { Path.Combine( programFilesX86, @"GOG Galaxy\GalaxyClient.exe" ) },
			GamePlatform.EaApp => new[]
			{
				Path.Combine( programFiles, @"Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe" ),
				Path.Combine( programFilesX86, @"Origin\Origin.exe" )
			},
			GamePlatform.UbisoftConnect => new[]
			{
				Path.Combine( ReadRegistryString( Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Ubisoft\Launcher", "InstallDir" ) ?? string.Empty, "UbisoftConnect.exe" ),
				Path.Combine( programFilesX86, @"Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe" )
			},
			GamePlatform.BattleNet => new[] { Path.Combine( programFilesX86, @"Battle.net\Battle.net Launcher.exe" ), Path.Combine( programFilesX86, @"Battle.net\Battle.net.exe" ) },
			GamePlatform.Riot => new[] { FindRiotClient(), @"C:\Riot Games\Riot Client\RiotClientServices.exe" },
			_ => Array.Empty<string?>()
		};

		return candidates
			.Where( candidate => !string.IsNullOrWhiteSpace( candidate ) )
			.Select( candidate => Path.GetFullPath( candidate! ) )
			.FirstOrDefault( File.Exists );
	}

	private static string? ReadRegistryString( RegistryKey root, string subKey, string valueName )
	{
		using RegistryKey? key = root.OpenSubKey( subKey );
		return key?.GetValue( valueName ) as string;
	}

	private static string? FindRiotClient()
	{
		string installsFile = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.CommonApplicationData ), "Riot Games", "RiotClientInstalls.json" );

		try
		{
			using JsonDocument document = JsonDocument.Parse( File.ReadAllText( installsFile ) );
			return document.RootElement.TryGetProperty( "rc_default", out JsonElement element ) ? element.GetString() : null;
		}
		catch ( Exception exception ) when ( exception is IOException or JsonException )
		{
			return null;
		}
	}
}
