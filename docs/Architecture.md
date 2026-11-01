# MateLauncher — Architecture

## Solution

```
MateLauncher.slnx
src/
  MateLauncher.Core             Modèles, interfaces, services métier (net9.0)
  MateLauncher.Providers        Détection par plateforme (Steam, Epic, GOG, EA, Ubisoft, Battle.net, Xbox, Riot, dossiers)
  MateLauncher.Infrastructure   SQLite (EF Core), jaquettes, métadonnées, sauvegardes, mises à jour, plugins, sécurité
  MateLauncher.App              WPF, vues, ViewModels (net9.0-windows)
  MateLauncher.Installer        Installateur autonome (WPF)
tests/
  MateLauncher.Tests            Tests unitaires xUnit
```

Dépendances : `App` → `Core`, `Providers`, `Infrastructure` ; `Providers` → `Core` ; `Infrastructure` → `Core`. `Core` ne dépend de rien. Les tests référencent `Core`, `Providers` et `Infrastructure` ; `Providers` expose ses parseurs aux tests via `InternalsVisibleTo`.

## Concepts principaux (Core)

- `Game` : identifiant, nom, plateforme, identifiant plateforme, chemin d'installation, commande de lancement, pochette, temps de jeu, dernier lancement, favori, masqué.
- `GamePlatform` : enum des plateformes. Ne jamais réordonner les valeurs : elles sont stockées en base.
- `IGameProvider` : `Detect()` retourne les jeux installés pour une plateforme. C'est aussi le contrat des plugins.
- `IGameLauncher` : lance un jeu via le protocole de la plateforme ou l'exécutable.
- `ILibraryRepository` : accès à la bibliothèque locale.
- `LibrarySyncService` : appelle chaque `IGameProvider` (une erreur n'arrête pas les autres) et fusionne le résultat dans la base. Aucune détection en mode sans échec.
- `ICoverFinder` / `IGameMetadataProvider` : sources de pochettes et de métadonnées.
- `IPlaytimeTracker` : surveille le processus lancé et cumule le temps de jeu.

## Détection (Providers)

- Steam : chemin d'installation dans le registre, lecture de `libraryfolders.vdf` et des `appmanifest_*.acf`.
- Epic : fichiers `.item` du dossier `ProgramData\Epic\EpicGamesLauncher\Data\Manifests`.
- GOG, EA, Ubisoft, Battle.net, Xbox, Riot : registre et fichiers locaux de chaque client.
- Dossiers : exécutables trouvés dans les dossiers choisis par l'utilisateur.
- Plugins : voir ci-dessous.

## Infrastructure

- `Persistence` : EF Core et SQLite (`library.db`).
- `Settings/JsonSettingsService` : `settings.json`. Les clés d'API sont chiffrées avec DPAPI (`Security/SecretProtector`, préfixe `dpapi:`) ; les anciennes valeurs en clair sont relues puis réécrites chiffrées.
- `Covers` : chaîne de `ICoverFinder` dans `CompositeCoverFinder` (magasin Steam, SteamGridDB, IGDB, Wikipédia, Google), puis `CoverCache`.
- `Metadata/IgdbClient` : OAuth Twitch, métadonnées (cache JSON local) et jaquettes.
- `Updates/AppUpdateService` : interroge les releases GitHub, exige le fichier `<installateur>.sha256` et vérifie l'empreinte SHA-256 avant d'exécuter l'installateur.
- `Backup` : sauvegarde, export et import de la base et des réglages.
- `Plugins` : `PluginLoader` charge les DLL de `%AppData%\MateLauncher\plugins` dans un `AssemblyLoadContext` dédié (`MateLauncher.Core` partagé) ; `PluginGameProvider` agrège les plugins sous la plateforme `Plugin` et ne charge rien tant que la première détection n'a pas lieu. Voir [Plugins.md](Plugins.md).

## Interface (App)

MVVM avec CommunityToolkit.Mvvm et injection de dépendances (`Microsoft.Extensions.DependencyInjection`).

`MainViewModel` est une classe partielle répartie par domaine : `Library`, `Navigation`, `Filters`, `Folders`, `Themes`, `Settings`, `Updates`, `GameUpdates`, `News`, `Backup`, `Integrations` (Steam, Google, IGDB), `Covers`. Les champs, propriétés observables et le constructeur restent dans `MainViewModel.cs`. Prochaine étape naturelle : extraire certains domaines en ViewModels dédiés, ce qui demande de reprendre les bindings du XAML.

Thèmes : `ThemeService` fournit les pinceaux ; avec l'effet translucide activé et Windows 11 (build 22621 ou plus), `TitleBarStyler` applique le fond acrylique DWM et `ThemeService` rend les surfaces semi-transparentes.

## Intégration continue

- `ci.yml` : compile la solution et lance les tests à chaque push sur `main` et chaque pull request.
- `release.yml` : sur un tag `v*`, construit l'installateur et l'archive portable, génère le `.sha256` de l'installateur et publie la release.

## Conventions

Voir [CONTRIBUTING.md](../CONTRIBUTING.md) : Allman, tabulations, espaces dans les parenthèses, aucun commentaire, nommage en anglais avec attributs privés `_PascalCase`.
