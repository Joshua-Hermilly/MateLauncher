# Contribuer à MateLauncher

Merci de votre intérêt ! Quelques règles simples pour que les contributions s'intègrent bien.

## Avant de commencer

- Ouvrez d'abord une issue pour une nouvelle fonctionnalité, afin d'en discuter.
- Les corrections de bugs et les petites améliorations peuvent aller directement en pull request.
- Pour une nouvelle plateforme de jeux, préférez un [plugin](docs/Plugins.md) tant que la plateforme est de niche.

## Environnement

SDK .NET 9 sous Windows.

```powershell
dotnet build MateLauncher.slnx
dotnet test tests/MateLauncher.Tests
```

La pull request doit compiler et passer les tests (vérifié par la CI).

## Style de code

- Accolades **Allman** (ouvrante sur sa propre ligne), indentation par **tabulations**.
- Espaces dans les parenthèses : `Methode( param )`, `if ( condition )`.
- Nommage en anglais. Attribut privé `_PascalCase`, membre public `PascalCase`, variable locale et paramètre `camelCase`.
- Pas de commentaires : le code doit se documenter par des noms explicites.
- Respectez l'architecture en place : `Core` ne dépend de rien, `Providers` et `Infrastructure` dépendent de `Core`, `App` dépend de tout.

## Tests

Ajoutez des tests xUnit pour toute logique de parsing, de fusion ou de sécurité (voir `tests/MateLauncher.Tests`). Évitez les tests qui touchent à `%AppData%` : utilisez un dossier temporaire.

## Interface

Les textes passent par `Localization/Strings.cs` (français et anglais). N'écrivez jamais de texte en dur dans le XAML.

## Sécurité

Ne commitez jamais de clé d'API. Pour signaler une faille, ouvrez une [security advisory](https://github.com/Joshua-Hermilly/MateLauncher/security/advisories/new) plutôt qu'une issue publique.
