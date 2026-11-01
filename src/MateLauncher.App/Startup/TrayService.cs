using System.Windows;
using MateLauncher.App.Localization;

namespace MateLauncher.App.Startup;

public class TrayService : IDisposable
{
	private readonly Action _ShowWindow;
	private readonly Action _ToggleOverlay;
	private readonly Action _Quit;
	private System.Windows.Forms.NotifyIcon? _Icon;

	public bool IsVisible => _Icon?.Visible == true;

	public TrayService( Action showWindow, Action toggleOverlay, Action quit )
	{
		_ShowWindow = showWindow;
		_ToggleOverlay = toggleOverlay;
		_Quit = quit;
	}

	public void SetVisible( bool visible )
	{
		if ( !visible )
		{
			if ( _Icon is not null )
			{
				_Icon.Visible = false;
			}

			return;
		}

		if ( _Icon is null )
		{
			_Icon = new System.Windows.Forms.NotifyIcon
			{
				Icon = System.Drawing.Icon.ExtractAssociatedIcon( Environment.ProcessPath! ),
				Text = "MateLauncher",
				ContextMenuStrip = BuildMenu()
			};
			_Icon.MouseClick += ( _, args ) =>
			{
				if ( args.Button == System.Windows.Forms.MouseButtons.Left )
				{
					_ShowWindow();
				}
			};
		}

		_Icon.Visible = true;
	}

	public void RefreshTexts()
	{
		if ( _Icon is not null )
		{
			_Icon.ContextMenuStrip = BuildMenu();
		}
	}

	private System.Windows.Forms.ContextMenuStrip BuildMenu()
	{
		System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
		menu.Items.Add( Localizer.Instance.Get( "Tray_Open" ), null, ( _, _ ) => _ShowWindow() );
		menu.Items.Add( Localizer.Instance.Get( "Tray_Overlay" ), null, ( _, _ ) => _ToggleOverlay() );
		menu.Items.Add( new System.Windows.Forms.ToolStripSeparator() );
		menu.Items.Add( Localizer.Instance.Get( "Tray_Quit" ), null, ( _, _ ) => _Quit() );
		return menu;
	}

	public void Dispose()
	{
		if ( _Icon is not null )
		{
			_Icon.Visible = false;
			_Icon.Dispose();
			_Icon = null;
		}
	}
}
