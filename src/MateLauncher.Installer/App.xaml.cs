using System.Windows;

namespace MateLauncher.Installer;

public partial class App : Application
{
	protected override void OnStartup( StartupEventArgs e )
	{
		base.OnStartup( e );
		bool isUninstall = e.Args.Any( argument => string.Equals( argument, "/uninstall", StringComparison.OrdinalIgnoreCase ) );
		bool isUpdate = e.Args.Any( argument => string.Equals( argument, "/update", StringComparison.OrdinalIgnoreCase ) );
		new MainWindow( isUninstall, isUpdate ).Show();
	}
}
