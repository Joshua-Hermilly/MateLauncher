using System.Collections.ObjectModel;
using System.Windows;
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
	[NotifyPropertyChangedFor( nameof( IsValid ) )]
	private string _Hex;

	public bool IsValid => AppTheme.IsValidColor( Hex );
	public Brush Brush => IsValid ? AppTheme.ToBrush( AppTheme.NormalizeColor( Hex ) ) : Brushes.Transparent;

	public ThemeColorField( string key, string label, string hex )
	{
		Key = key;
		Label = label;
		_Hex = hex;
	}
}

public partial class ThemeEditorWindow : Window
{
	private readonly ObservableCollection<ThemeColorField> _Fields = new ObservableCollection<ThemeColorField>();
	private readonly string _ThemeId;

	public AppTheme? Result { get; private set; }
	public bool IsDeleted { get; private set; }

	public ThemeEditorWindow( AppTheme source, bool isNew )
	{
		InitializeComponent();
		_ThemeId = isNew ? "custom-" + Guid.NewGuid().ToString( "N" )[ ..8 ] : source.Id;
		NameBox.Text = isNew ? Localizer.Instance.Get( "Theme_DefaultName" ) : source.Name;
		DeleteButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;

		_Fields.Add( new ThemeColorField( "Background", Localizer.Instance.Get( "Theme_Background" ), source.Background ) );
		_Fields.Add( new ThemeColorField( "Sidebar", Localizer.Instance.Get( "Theme_Sidebar" ), source.Sidebar ) );
		_Fields.Add( new ThemeColorField( "Card", Localizer.Instance.Get( "Theme_Card" ), source.Card ) );
		_Fields.Add( new ThemeColorField( "CardHover", Localizer.Instance.Get( "Theme_CardHover" ), source.CardHover ) );
		_Fields.Add( new ThemeColorField( "Border", Localizer.Instance.Get( "Theme_Border" ), source.Border ) );
		_Fields.Add( new ThemeColorField( "Accent", Localizer.Instance.Get( "Theme_Accent" ), source.Accent ) );
		_Fields.Add( new ThemeColorField( "Text", Localizer.Instance.Get( "Theme_Text" ), source.Text ) );
		_Fields.Add( new ThemeColorField( "Muted", Localizer.Instance.Get( "Theme_Muted" ), source.Muted ) );

		foreach ( ThemeColorField field in _Fields )
		{
			field.PropertyChanged += ( _, _ ) => UpdatePreview();
		}

		FieldList.ItemsSource = _Fields;
		UpdatePreview();
	}

	private Brush PreviewBrush( string key, Brush fallback )
	{
		ThemeColorField field = _Fields.First( candidate => candidate.Key == key );
		return field.IsValid ? field.Brush : fallback;
	}

	private void UpdatePreview()
	{
		PreviewFrame.Background = PreviewBrush( "Background", Brushes.Black );
		PreviewFrame.BorderBrush = PreviewBrush( "Border", Brushes.Gray );
		PreviewSidebar.Background = PreviewBrush( "Sidebar", Brushes.Black );
		PreviewCard.Background = PreviewBrush( "Card", Brushes.DimGray );
		PreviewCardHover.Background = PreviewBrush( "CardHover", Brushes.Gray );
		PreviewAccent.Background = PreviewBrush( "Accent", Brushes.Red );
		PreviewTitle.Foreground = PreviewBrush( "Text", Brushes.White );
		PreviewMuted.Foreground = PreviewBrush( "Muted", Brushes.Gray );
	}

	private void OnSwatchClicked( object sender, RoutedEventArgs e )
	{
		if ( ( sender as FrameworkElement )?.DataContext is not ThemeColorField field )
		{
			return;
		}

		ColorPickerWindow picker = new ColorPickerWindow( field.Label, field.IsValid ? AppTheme.NormalizeColor( field.Hex ) : null ) { Owner = this };

		if ( picker.ShowDialog() == true && picker.SelectedColor is not null && AppTheme.IsValidColor( picker.SelectedColor ) )
		{
			field.Hex = AppTheme.NormalizeColor( picker.SelectedColor );
		}
	}

	private void OnOkClicked( object sender, RoutedEventArgs e )
	{
		string name = NameBox.Text.Trim();

		if ( name.Length == 0 )
		{
			NameBox.Focus();
			return;
		}

		if ( _Fields.Any( field => !field.IsValid ) )
		{
			return;
		}

		string Value( string key )
		{
			return AppTheme.NormalizeColor( _Fields.First( field => field.Key == key ).Hex );
		}

		Result = new AppTheme( _ThemeId, name, Value( "Background" ), Value( "Sidebar" ), Value( "Card" ), Value( "CardHover" ), Value( "Border" ), Value( "Accent" ), Value( "Text" ), Value( "Muted" ), true );
		DialogResult = true;
	}

	private void OnDeleteClicked( object sender, RoutedEventArgs e )
	{
		IsDeleted = true;
		DialogResult = true;
	}
}
