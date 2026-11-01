using CommunityToolkit.Mvvm.ComponentModel;
using MateLauncher.Infrastructure.Launching;

namespace MateLauncher.App.ViewModels;

public partial class MainViewModel
{
	[ObservableProperty]
	private bool _PerformanceModeEnabled;

	[ObservableProperty]
	private string _PerformancePrograms = string.Empty;

	partial void OnPerformanceModeEnabledChanged( bool value )
	{
		SaveSettings();
	}

	partial void OnPerformanceProgramsChanged( string value )
	{
		SaveSettings();
	}

	private List<string> GetPerformanceProgramList()
	{
		return PerformanceMode.ParseProgramNames( PerformancePrograms );
	}
}
