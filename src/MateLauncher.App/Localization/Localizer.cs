using System.ComponentModel;

namespace MateLauncher.App.Localization;

public class Localizer : INotifyPropertyChanged
{
	public static Localizer Instance { get; } = new Localizer();

	private Dictionary<string, string> _Current;

	public string Language { get; private set; } = "fr";

	public event PropertyChangedEventHandler? PropertyChanged;
	public event Action? LanguageChanged;

	private Localizer()
	{
		_Current = Strings.French;
	}

	public string this[ string key ] => Get( key );

	public string Get( string key )
	{
		if ( _Current.TryGetValue( key, out string? value ) )
		{
			return value;
		}

		return Strings.French.TryGetValue( key, out string? fallback ) ? fallback : key;
	}

	public string Format( string key, params object[] arguments )
	{
		return string.Format( Get( key ), arguments );
	}

	public void SetLanguage( string language )
	{
		string code = language == "en" ? "en" : "fr";

		if ( code == Language )
		{
			return;
		}

		Language = code;
		_Current = code == "en" ? Strings.English : Strings.French;
		PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( "Item[]" ) );
		LanguageChanged?.Invoke();
	}
}
