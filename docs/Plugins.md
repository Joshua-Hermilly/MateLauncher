# Plugins MateLauncher

Un plugin ajoute une source de jeux (une plateforme de plus) sans toucher au code de MateLauncher.

> **Sécurité** : un plugin est une DLL exécutée avec vos droits Windows. N'installez que des plugins dont vous faites confiance. Ils ne sont jamais chargés en mode sans échec.

## Installer un plugin

Copiez-le dans `%AppData%\MateLauncher\plugins`, puis relancez MateLauncher et cliquez sur **Synchroniser**. Deux dispositions sont acceptées :

```
plugins/
  MonPlugin.dll              plugin sans dépendance
  AutrePlugin/
    AutrePlugin.dll          plugin avec ses DLL de dépendances à côté
    Dependance.dll
```

Les jeux d'un plugin apparaissent sous la plateforme **Plugin**. En cas de problème, lancez MateLauncher avec `--debug` : les erreurs de chargement sont écrites dans `debug.log`.

## Écrire un plugin

1. Créez une bibliothèque de classes `net9.0`.
2. Référencez `MateLauncher.Core` (le projet ou la DLL fournie avec l'application).
3. Implémentez `IGameProvider` dans une classe **publique** avec un **constructeur sans paramètre**.

```csharp
using MateLauncher.Core.Abstractions;
using MateLauncher.Core.Models;

namespace MonPlugin;

public class MonProvider : IGameProvider
{
	public GamePlatform Platform => GamePlatform.Plugin;

	public IReadOnlyList<Game> Detect()
	{
		return new List<Game>
		{
			new Game
			{
				Name = "Mon jeu",
				PlatformGameId = "mon-jeu",
				InstallPath = @"D:\Jeux\MonJeu",
				LaunchCommand = @"D:\Jeux\MonJeu\jeu.exe",
				CoverImagePath = "https://exemple.com/pochette.jpg"
			}
		};
	}
}
```

## Règles

- `Detect()` est appelée à chaque synchronisation : elle doit être rapide et ne jamais bloquer l'interface. Une exception est capturée et journalisée, elle n'empêche pas les autres plugins.
- MateLauncher force la plateforme à `Plugin` et préfixe `PlatformGameId` par le nom de votre assembly (`MonPlugin:mon-jeu`), ce qui évite les collisions entre plugins. Gardez donc un identifiant stable d'une exécution à l'autre : il sert à retrouver le temps de jeu et les favoris.
- Un jeu sans nom est ignoré.
- `CoverImagePath` accepte une URL `https` ou un chemin de fichier. Sans pochette, MateLauncher en cherche une avec ses sources habituelles.
- `MateLauncher.Core` est partagé avec l'application : ne l'embarquez pas dans le dossier du plugin.
