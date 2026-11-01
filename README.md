# MateLauncher

Un launcher unique pour toute votre bibliothèque de jeux Windows : Steam, Epic Games, GOG, EA App, Ubisoft Connect, Battle.net, Xbox, Riot, vos dossiers de jeux et vos ajouts manuels. Open source, local, sans compte et sans télémétrie.

> Interface en français et en anglais · Windows 10/11 · .NET 9 · licence MIT

<!-- Ajoutez ici une capture d'écran : docs/images/library.png -->

## Fonctionnalités

- **Détection automatique** des jeux installés sur chaque plateforme (fichiers locaux et registre, aucune connexion requise).
- **Bibliothèque** en grille ou en liste, recherche, filtres, tri, favoris, catégories colorées, dossiers de jeux.
- **Lancement** via le client officiel (`steam://run/…`) ou l'exécutable, avec arguments et exécution en administrateur.
- **Temps de jeu** par jeu et par jour, statistiques, overlay en jeu avec raccourci clavier.
- **Jaquettes** : magasin Steam, [SteamGridDB](https://www.steamgriddb.com), IGDB, Wikipédia, Google (optionnel).
- **Fiche jeu enrichie** (description, genres, date de sortie, note) grâce à [IGDB](https://www.igdb.com).
- **Succès Steam**, actualités des jeux, mises à jour Steam en attente.
- **Thèmes** intégrés et personnalisés, couleur d'accent libre, **effet translucide** (acrylique, Windows 11).
- **Manette** (XInput) pour naviguer sans clavier.
- **Sauvegarde** automatique, export et import de la bibliothèque.
- **Mode sans échec** si le lancement précédent a planté.
- **Plugins** pour ajouter d'autres plateformes : voir [docs/Plugins.md](docs/Plugins.md).

## Installation

Téléchargez `MateLauncher-Setup-<version>-win-x64.exe` depuis la page [Releases](https://github.com/Joshua-Hermilly/MateLauncher/releases), ou la version portable `.zip`.

Chaque installateur est publié avec son empreinte `.sha256`. MateLauncher la vérifie avant d'installer une mise à jour et refuse tout fichier dont l'empreinte ne correspond pas. Pour vérifier à la main :

```powershell
Get-FileHash .\MateLauncher-Setup-<version>-win-x64.exe -Algorithm SHA256
```

## Vie privée et sécurité

- Aucune télémétrie. Les données restent dans `%AppData%\MateLauncher`.
- Les clés d'API que vous saisissez (Steam, Google, SteamGridDB, IGDB) sont chiffrées avec DPAPI : elles ne sont lisibles que par votre compte Windows, sur cet ordinateur.
- Les plugins sont du code exécuté avec vos droits : n'installez que ceux dont vous faites confiance. Ils ne sont jamais chargés en mode sans échec.

## Clés d'API (facultatives)

| Service | Sert à | Où l'obtenir |
|---|---|---|
| Steam Web API | succès et profil | <https://steamcommunity.com/dev/apikey> |
| SteamGridDB | jaquettes de qualité | <https://www.steamgriddb.com/profile/preferences/api> |
| IGDB (Twitch) | métadonnées et jaquettes | <https://api-docs.igdb.com/#account-creation> |
| Google Custom Search | jaquettes de secours | <https://programmablesearchengine.google.com> |

Saisissez-les dans **Paramètres**. Sans clé, MateLauncher fonctionne avec les sources publiques.

## Compiler

Prérequis : SDK .NET 9 sous Windows.

```powershell
dotnet build MateLauncher.slnx
dotnet test tests/MateLauncher.Tests
dotnet run --project src/MateLauncher.App
```

Pour produire l'installateur et l'archive portable avec leur empreinte :

```powershell
./build-release.ps1 -Version 1.0.0
```

Une release GitHub est créée automatiquement en poussant un tag `v*` (voir `.github/workflows/release.yml`). L'intégration continue compile et teste chaque push et chaque pull request.

## Architecture

```
src/
  MateLauncher.Core            modèles, interfaces, synchronisation (aucune dépendance)
  MateLauncher.Providers       détection par plateforme
  MateLauncher.Infrastructure  SQLite, jaquettes, métadonnées, mises à jour, plugins
  MateLauncher.App             WPF, MVVM (CommunityToolkit.Mvvm)
  MateLauncher.Installer       installateur
tests/MateLauncher.Tests       xUnit
```

Détails dans [docs/Architecture.md](docs/Architecture.md).

## Contribuer

Les issues et pull requests sont les bienvenues. Lisez [CONTRIBUTING.md](CONTRIBUTING.md) avant de commencer.

## Licence

[MIT](LICENSE) © 2026 Joshua Hermilly. Les noms et logos des plateformes appartiennent à leurs propriétaires respectifs ; MateLauncher n'est affilié à aucun d'eux.
