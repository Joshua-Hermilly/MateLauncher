using System.Windows.Media;
using MateLauncher.App.Icons;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MateLauncher.App.Localization;
using MateLauncher.App.Themes;
using MateLauncher.Core.Models;

namespace MateLauncher.App.ViewModels;

public partial class ThemeOptionViewModel : ObservableObject
{
	public AppTheme Theme { get; }
	public bool IsCustom => Theme.IsCustom;

	[ObservableProperty]
	private bool _IsSelected;

	public ThemeOptionViewModel( AppTheme theme )
	{
		Theme = theme;
	}
}

public partial class AccentOptionViewModel : ObservableObject
{
	public string? Hex { get; }
	public Brush? Brush { get; }

	[ObservableProperty]
	private bool _IsSelected;

	public AccentOptionViewModel( string? hex )
	{
		Hex = hex;
		Brush = hex is null ? null : AppTheme.ToBrush( hex );
	}
}

public partial class FolderEntryViewModel : ObservableObject
{
	public string Path { get; }
	public ImageSource? Icon => PlatformIcons.LoadImage( IconPath );
	public bool HasIcon => Icon is not null;

	[ObservableProperty]
	private string _TabName;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( Icon ) )]
	[NotifyPropertyChangedFor( nameof( HasIcon ) )]
	private string? _IconPath;

	[ObservableProperty]
	private bool _IsEnabled;

	[ObservableProperty]
	private FolderScanMode _ScanMode;

	public FolderEntryViewModel( string path, string tabName, string? iconPath = null, bool isEnabled = true, FolderScanMode scanMode = FolderScanMode.Subfolders )
	{
		Path = path;
		_TabName = tabName;
		_IconPath = iconPath;
		_IsEnabled = isEnabled;
		_ScanMode = scanMode;
	}
}

public partial class FolderDraftViewModel : ObservableObject
{
	public FolderEntryViewModel Source { get; }
	public string Path => Source.Path;
	public bool Exists => System.IO.Directory.Exists( Source.Path );

	[ObservableProperty]
	private bool _IsEnabled;

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( ScanModeText ) )]
	private int _ScanModeIndex;

	public string ScanModeText => Localizer.Instance.Get( ScanModeIndex switch { 1 => "Scan_Nested", 2 => "Scan_Single", 3 => "Scan_Recursive", _ => "Scan_Subfolders" } );

	[RelayCommand]
	private void CycleScanMode()
	{
		ScanModeIndex = ( ScanModeIndex + 1 ) % 4;
	}

	[ObservableProperty]
	private bool _IsRemoved;

	public FolderDraftViewModel( FolderEntryViewModel source )
	{
		Source = source;
		_IsEnabled = source.IsEnabled;
		_ScanModeIndex = ( int ) source.ScanMode;
	}
}
