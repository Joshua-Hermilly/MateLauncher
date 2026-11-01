# MateLauncher — Cahier des charges

## 1. Vision

Un launcher unique qui regroupe les jeux et applications de toutes les plateformes (Steam, Epic, GOG, EA, Ubisoft, Battle.net, Xbox) et les ajouts manuels. Interface de type « vrai launcher », inspirée de GOG Galaxy. Projet open source (licence MIT), publié sur GitHub.

## 2. Décisions actées

| Sujet | Décision |
|---|---|
| Stack | .NET 9, WPF, MVVM (CommunityToolkit.Mvvm) |
| Base locale | SQLite via Entity Framework Core |
| Nommage du code | Anglais |
| Plateforme cible | Windows uniquement |
| Licence | MIT |
| Dépôt | GitHub : Joshua-Hermilly/MateLauncher |
| Serveur en ligne | Reporté (pas dans la première version) |
| Lancement d'un jeu | Via le protocole de la plateforme (ex. `steam://run/<id>`), sinon exécutable direct |
| Synchronisation | Au démarrage, à la fermeture, et via un bouton « Synchroniser » |
| Barre système / démarrage Windows | Configurable |
| Téléchargements et mises à jour | Plus tard, en déclenchant le client officiel |

## 3. Plateformes

Ordre de priorité : Steam, Epic, jeux manuels, puis GOG, EA App, Ubisoft Connect, Battle.net, Xbox. Les applications qui ne sont pas des jeux sont aussi gérées (arguments, exécution en administrateur, dossier de travail, variables d'environnement).

## 4. Fonctionnalités de la première version (MVP)

1. Détecter les jeux Steam et Epic installés.
2. Afficher la bibliothèque en grille de pochettes (thème sombre).
3. Lancer un jeu.
4. Ajouter un jeu ou une application manuellement.
5. Enregistrer le temps de jeu et la date de dernier lancement.
6. Catégories, tags, collections, favoris, tri et filtres.
7. Masquer ou renommer un jeu.
8. Paramètres : barre système, démarrage avec Windows, comportement de la synchronisation.

## 5. Pochettes et métadonnées

Sources : fichiers locaux et magasin Steam, puis SteamGridDB, IGDB, Wikipédia et Google (clés facultatives).

- Les images de Steam sont déjà sur le disque (cache de la bibliothèque), donc aucune connexion n'est nécessaire.
- Epic fournit peu d'images en local, d'où l'intérêt de SteamGridDB (gratuit, clé d'API personnelle).
- IGDB fournit descriptions, genres, dates de sortie et notes (compte Twitch requis) : intégré, avec cache local.
- Les clés d'API ne sont jamais commitées : saisies dans les paramètres, stockées chiffrées (DPAPI) dans `settings.json`.

## 6. Hors périmètre pour l'instant

Serveur en ligne et comptes, fil d'actualité, social, téléchargement propre au launcher, Linux et macOS.

## 7. Hypothèses (points laissés ouverts)

- « Profils : tous » : je considère qu'il y a un seul utilisateur local, mais que plusieurs comptes d'une même plateforme peuvent être détectés. À confirmer.
- Le nom du dossier est corrigé en `MateLauncher`.
- Mises à jour du launcher : releases GitHub, avec vérification SHA-256.
