using System.Windows;
using System.Windows.Controls;
using MateLauncher.App.Localization;

namespace MateLauncher.App.Controls;

public partial class SecretBox : UserControl
{
	private const string ShowGlyph = "";
	private const string HideGlyph = "";

	public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
		nameof( Text ),
		typeof( string ),
		typeof( SecretBox ),
		new FrameworkPropertyMetadata( string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged ) );

	public string Text
	{
		get => ( string ) GetValue( TextProperty );
		set => SetValue( TextProperty, value );
	}

	public SecretBox()
	{
		InitializeComponent();
	}

	private bool IsRevealed => VisibleBox.Visibility == Visibility.Visible;

	private static void OnTextChanged( DependencyObject sender, DependencyPropertyChangedEventArgs args )
	{
		( ( SecretBox ) sender ).Refresh();
	}

	private void Refresh()
	{
		string value = Text ?? string.Empty;

		if ( HiddenBox.Password != value )
		{
			HiddenBox.Password = value;
		}

		if ( VisibleBox.Text != value )
		{
			VisibleBox.Text = value;
		}
	}

	private void Commit()
	{
		string value = IsRevealed ? VisibleBox.Text : HiddenBox.Password;

		if ( value != Text )
		{
			Text = value;
		}
	}

	private void OnBoxLostFocus( object sender, RoutedEventArgs args )
	{
		Commit();
	}

	private void OnRevealClick( object sender, RoutedEventArgs args )
	{
		Commit();
		bool reveal = !IsRevealed;
		Refresh();
		HiddenBox.Visibility = reveal ? Visibility.Collapsed : Visibility.Visible;
		VisibleBox.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;
		RevealGlyph.Text = reveal ? HideGlyph : ShowGlyph;
		RevealButton.ToolTip = Localizer.Instance.Get( reveal ? "Secret_Hide" : "Secret_Show" );
	}
}
