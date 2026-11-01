using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MateLauncher.App.Localization;
using MateLauncher.App.Themes;

namespace MateLauncher.App;

public partial class ThemeColorField : ObservableObject
{
	public string Key { get; }
	public string Label { get; }

	[ObservableProperty]
	[NotifyPropertyChangedFor( nameof( Brush ) )]
	private string _Hex;

	public Brush Brush => AppTheme.ToBrush( Hex );

	public ThemeColorField( string key, string label, string hex )
	{
		Key = key;
		Label = label;
		_Hex = AppTheme.NormalizeColor( hex );
	}
}

public partial class ThemeEditorWindow : Window
{
	private readonly ObservableCollection<ThemeColorField> _Fields = new ObservableCollection<ThemeColorField>();
	private readonly string _ThemeId;
	private bool _IsLoadingField;

	public AppTheme? Result { get; private set; }
	public bool IsDeleted { get; private set; }

	public ThemeEditorWindow( AppTheme source, bool isNew )
	{
		InitializeComponent();
		_ThemeId = isNew ? "custom-" + Guid.NewGuid().ToString( "N" )[ ..8 ] : source.Id;
		NameBox.Text = isNew ? Localizer.Instance.Get( "Theme_DefaultName" ) : source.Name;
		DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;

		_Fields.Add( new ThemeColorField( "Accent", Localizer.Instance.Get( "Theme_Accent" ), source.Accent ) );
		_Fields.Add( new ThemeColorField( "Background", Localizer.Instance.Get( "Theme_Background" ), source.Background ) );
		_Fields.Add( new ThemeColorField( "Sidebar", Localizer.Instance.Get( "Theme_Sidebar" ), source.Sidebar ) );
		_Fields.Add( new ThemeColorField( "Card", Localizer.Instance.Get( "Theme_Card" ), source.Card ) );
		_Fields.Add( new ThemeColorField( "CardHover", Localizer.Instance.Get( "Theme_CardHover" ), source.CardHover ) );
		_Fields.Add( new ThemeColorField( "Border", Localizer.Instance.Get( "Theme_Border" ), source.Border ) );
		_Fields.Add( new ThemeColorField( "Text", Localizer.Instance.Get( "Theme_Text" ), source.Text ) );
		_Fields.Add( new ThemeColorField( "Muted", Localizer.Instance.Get( "Theme_Muted" ), source.Muted ) );

		foreach ( ThemeColorField field in _Fields )
		{
			field.PropertyChanged += ( _, _ ) => UpdatePreview();
		}

		FieldList.ItemsSource = _Fields;
		FieldList.SelectedIndex = 0;
		UpdatePreview();
	}

	private string ValueOf( string key )
	{
		return _Fields.First( field => field.Key == key ).Hex;
	}

	private void UpdatePreview()
	{
		PreviewFrame.Background = AppTheme.ToBrush( ValueOf( "Background" ) );
		PreviewFrame.BorderBrush = AppTheme.ToBrush( ValueOf( "Border" ) );
		PreviewSidebar.Background = AppTheme.ToBrush( ValueOf( "Sidebar" ) );
		PreviewDot.Fill = AppTheme.ToBrush( ValueOf( "Accent" ) );
		PreviewBar1.Background = AppTheme.ToBrush( ValueOf( "Muted" ) );
		PreviewBar2.Background = AppTheme.ToBrush( ValueOf( "Muted" ) );
		PreviewTitle.Foreground = AppTheme.ToBrush( ValueOf( "Text" ) );
		PreviewMuted.Foreground = AppTheme.ToBrush( ValueOf( "Muted" ) );
		PreviewCard.Background = AppTheme.ToBrush( ValueOf( "Card" ) );
		PreviewCardHover.Background = AppTheme.ToBrush( ValueOf( "CardHover" ) );
		PreviewButton.Background = AppTheme.ToBrush( ValueOf( "Accent" ) );
	}

	private void OnFieldSelected( object sender, SelectionChangedEventArgs e )
	{
		if ( FieldList.SelectedItem is not ThemeColorField field )
		{
			return;
		}

		_IsLoadingField = true;
		SelectedLabel.Text = field.Label.ToUpperInvariant();
		Picker.Hex = field.Hex;
		_IsLoadingField = false;
	}

	private void OnPickerChanged( object sender, EventArgs e )
	{
		if ( !_IsLoadingField && FieldList.SelectedItem is ThemeColorField field )
		{
			field.Hex = Picker.Hex;
		}
	}

	private AppTheme? BuildTheme()
	{
		string name = NameBox.Text.Trim();

		if ( name.Length == 0 )
		{
			NameBox.Focus();
			return null;
		}

		return new AppTheme( _ThemeId, name, ValueOf( "Background" ), ValueOf( "Sidebar" ), ValueOf( "Card" ), ValueOf( "CardHover" ), ValueOf( "Border" ), ValueOf( "Accent" ), ValueOf( "Text" ), ValueOf( "Muted" ), true );
	}

	private void OnOkClicked( object sender, RoutedEventArgs e )
	{
		Result = BuildTheme();

		if ( Result is not null )
		{
			DialogResult = true;
		}
	}

	private void OnDeleteClicked( object sender, RoutedEventArgs e )
	{
		IsDeleted = true;
		DialogResult = true;
	}

	private void OnExportClicked( object sender, RoutedEventArgs e )
	{
		AppTheme? theme = BuildTheme();

		if ( theme is null )
		{
			return;
		}

		Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
		{
			Title = Localizer.Instance.Get( "Theme_Export" ),
			Filter = Localizer.Instance.Get( "Theme_FileFilter" ),
			FileName = theme.Name + ".json"
		};

		if ( dialog.ShowDialog( this ) == true )
		{
			File.WriteAllText( dialog.FileName, JsonSerializer.Serialize( theme.ToCustom(), new JsonSerializerOptions { WriteIndented = true } ) );
		}
	}
}
