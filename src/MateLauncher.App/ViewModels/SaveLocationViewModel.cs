using MateLauncher.App.Localization;
using MateLauncher.Providers.Saves;

namespace MateLauncher.App.ViewModels;

public class SaveLocationViewModel
{
	public string Path { get; }
	public string Source { get; }
	public string SizeText { get; }
	public string ModifiedText { get; }
	public string Details => $"{Source}  ·  {SizeText}  ·  {ModifiedText}";

	public SaveLocationViewModel( SaveLocation location )
	{
		Path = location.Path;
		Source = location.Source;
		SizeText = location.SizeBytes <= 0 ? Localizer.Instance.Get( "Text_Unknown" ) : GameCardViewModel.FormatSize( location.SizeBytes );
		ModifiedText = location.LastWriteTime is null ? Localizer.Instance.Get( "Text_Never" ) : location.LastWriteTime.Value.ToString( "dd/MM/yyyy HH:mm" );
	}
}
