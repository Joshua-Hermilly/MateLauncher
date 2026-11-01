using MateLauncher.Core.Models;

namespace MateLauncher.Core.Abstractions;

public interface ISettingsService
{
	AppSettings Load();

	void Save( AppSettings settings );
}
