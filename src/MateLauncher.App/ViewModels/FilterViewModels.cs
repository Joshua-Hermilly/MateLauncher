using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MateLauncher.App.ViewModels;

public partial class FilterOptionViewModel : ObservableObject
{
	public string Key { get; }
	public string Label { get; }

	public Action<FilterOptionViewModel>? Changed { get; set; }

	[ObservableProperty]
	private int _Count;

	[ObservableProperty]
	private bool _IsSelected;

	public FilterOptionViewModel( string key, string label )
	{
		Key = key;
		Label = label;
	}

	partial void OnIsSelectedChanged( bool value )
	{
		Changed?.Invoke( this );
	}
}

public partial class FilterGroupViewModel : ObservableObject
{
	public string Id { get; }
	public string Title { get; }
	public ObservableCollection<FilterOptionViewModel> Options { get; } = new ObservableCollection<FilterOptionViewModel>();

	[ObservableProperty]
	private bool _IsExpanded = true;

	public FilterGroupViewModel( string id, string title )
	{
		Id = id;
		Title = title;
	}
}
