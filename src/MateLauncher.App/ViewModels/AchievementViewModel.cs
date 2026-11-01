using MateLauncher.App.Localization;
using MateLauncher.Infrastructure.Achievements;

namespace MateLauncher.App.ViewModels;

public class AchievementViewModel
{
	public string Name { get; }
	public string Description { get; }
	public bool IsUnlocked { get; }
	public string? IconUrl { get; }
	public string DateText { get; }
	public string PercentText { get; }

	public AchievementViewModel( AchievementInfo info )
	{
		Name = info.Name;
		Description = info.Description;
		IsUnlocked = info.IsUnlocked;
		IconUrl = info.IconUrl;
		DateText = info.UnlockedAt is null ? string.Empty : string.Format( Localizer.Instance.Get( "Ach_Unlocked" ), info.UnlockedAt.Value.ToString( "dd/MM/yyyy HH:mm" ) );
		PercentText = info.GlobalPercent is null ? string.Empty : string.Format( Localizer.Instance.Get( "Ach_Rarity" ), info.GlobalPercent.Value.ToString( "0.#" ) );
	}
}
