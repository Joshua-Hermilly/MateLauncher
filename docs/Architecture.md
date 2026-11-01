# MateLauncher — Architecture

## Solution

```
MateLauncher.sln
src/
  MateLauncher.Core             Models, interfaces, services métier (net9.0)
  MateLauncher.Providers        Détection par plateforme (Steam, Epic, Manual, ...)
  MateLauncher.Infrastructure   EF Core + SQLite, cache d'images, suivi du temps de jeu
  MateLauncher.App              WPF, Views, ViewModels (net9.0-windows)
tests/
  MateLauncher.Tests            Tests unitaires
```

Dépendances : `App` → `Core`, `Providers`, `Infrastructure` ; `Providers` → `Core` ; `Infrastructure` → `Core`. `Core` ne dépend de rien.

## Concepts principaux (Core)

- `Game` : identifiant, nom, plateforme, identifiant plateforme, chemin d'installation, commande de lancement, pochette, temps de jeu, dernier lancement, favori, masqué.
- `GamePlatform` : enum des plateformes.
- `IGameProvider` : `Detect()` retourne les jeux installés pour une plateforme.
- `IGameLauncher` : lance un jeu via le protocole de la plateforme ou l'exécutable.
- `ILibraryRepository` : accès à la bibliothèque locale.
- `LibrarySyncService` : appelle chaque `IGameProvider` et fusionne le résultat dans la base.
- `PlaytimeTracker` : surveille le processus lancé et cumule le temps de jeu.

## Détection (Providers)

- Steam : chemin d'installation dans le registre, lecture de `libraryfolders.vdf` et des `appmanifest_*.acf`.
- Epic : fichiers `.item` du dossier `ProgramData\Epic\EpicGamesLauncher\Data\Manifests`.
- Manual : jeux ajoutés par l'utilisateur.
- GOG, EA, Ubisoft, Battle.net, Xbox : même schéma, ajoutés ensuite.

## Interface (App)

MVVM avec CommunityToolkit.Mvvm et injection de dépendances (`Microsoft.Extensions.DependencyInjection`). Thème sombre, grille de pochettes, panneau de détail, barre latérale de collections.

## Conventions

Celles de `~/.claude/CLAUDE.md` : Allman, tabulations, espaces dans les parenthèses, aucun commentaire, C# avec attributs privés `_PascalCase`.
