using System.Windows.Data;
using System.Windows.Markup;

namespace MateLauncher.App.Localization;

[MarkupExtensionReturnType( typeof( string ) )]
public class TrExtension : MarkupExtension
{
	public string Key { get; set; }

	public TrExtension( string key )
	{
		Key = key;
	}

	public override object ProvideValue( IServiceProvider serviceProvider )
	{
		Binding binding = new Binding( $"[{Key}]" )
		{
			Source = Localizer.Instance,
			Mode = BindingMode.OneWay
		};

		return binding.ProvideValue( serviceProvider );
	}
}
