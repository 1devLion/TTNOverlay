# Contributing to TTNOverlay

Thanks for your interest in contributing! All changes go through pull requests and are reviewed and merged by the maintainer.

## Before you start

- **Bugs and small fixes**: feel free to open a pull request directly.
- **New features or big changes**: please open an issue first so we can discuss it. This avoids wasted work on something that might not fit the project.

## How to contribute

1. Fork the repository and create a branch from `main`.
2. Make your changes, keeping them focused on a single purpose.
3. Test the app locally.
4. Open a pull request against `main` describing what you changed and why.

## Building locally

You need the .NET 10 SDK. See the [Build section of the README](README.md#build) for the publish command.

The release workflow generates `Source/Generated/AppKeyProvider.cs` with the project's Twitch Client ID, which is stored as a secret. To build locally, create that file yourself with your own Twitch Client ID:

```csharp
namespace TTNOverlay.Generated
{
    internal static class AppKeyProvider
    {
        public const string Key = "YOUR_TWITCH_CLIENT_ID";
    }
}
```

Never commit your Client ID or any other secret.

## Commit messages

This project uses [Conventional Commits](https://www.conventionalcommits.org/), written in English:

- `feat:` a new feature
- `fix:` a bug fix
- `docs:` documentation only
- `refactor:` code change that neither fixes a bug nor adds a feature
- `perf:` a performance improvement
- `chore:` maintenance tasks

Example: `fix: reconnect chat after a dropped connection`

## Translations

UI text is looked up by key through `Strings.Get(key, language)`. The text itself lives in `Services/Localization/<Lang>/`, with one folder per language (`En`, `Es`, `De`, `Fr`, `Ja`, `Pt`, `Ru`, `Zh`). Each folder is split into files by area (`Common.cs`, `SettingsGeneral.cs`, `EventMessages.cs`, ...), and `<Lang>Strings.cs` gathers all of them into a single map.

**English (`En`) is the reference language.** If a key is missing in another language, the app falls back to the English text and logs a warning listing the missing keys, so a partial translation never breaks the UI.

### Adding or changing a string

1. Add the key to the matching area file in `En`. This one is required.
2. Add the translation to the same file in every other language folder. If you can't translate a language, leave it out and mention it in the PR so it can be completed later.
3. Keep the `{0}`, `{1}`, ... placeholders and any `\n` exactly as in the English text.
4. For text that depends on a count, add two entries, `<key>_One` and `<key>_Other`, and look it up with `Strings.GetPlural`.

Example (`Services/Localization/Es/ChatConnection.cs`):

```csharp
["MainWindow_Connecting"] = "Conectando a #{0}...",
```

### Adding a new area file

1. Create the file in every language folder, as a `partial` class with a `<Name>Entries` dictionary (copy an existing area file as a template).
2. Register `<Name>Entries` in the `Sections` list of each `<Lang>Strings.cs`.

### Adding a new language

1. Add a member to the `AppLanguage` enum in `Services/AppLanguage.cs`. The member name is what gets saved in the settings (`Settings.Language`), so it must match exactly the string used in the language dropdown (step 5). Renaming it later resets that setting for existing users.
2. Create `Services/Localization/<Lang>/` by copying an existing language folder, including `<Lang>Strings.cs`, and rename the class to `<Lang>Strings`.
3. Translate the values, keeping the keys unchanged.
4. Register the language in the `Tables` dictionary in `Services/Strings.cs`.
5. Add an entry to the list in `OpenLanguageDropdown()` in `Overlay/Settings/SettingsRenderWindow.General.cs`. Use the same pattern as the existing ones, with `Label` (the text shown in the dropdown), the same string assigned to `Settings.Language`, and the matching `AppLanguage` member in `SetLanguage`.
6. If the language has different plural rules than "one when count is 1" (as French, Japanese and Chinese do), update `GetPluralCategory` in `Services/Strings.cs`.
7. Add its flag to the "Supported Languages" section of the README and update the languages badge.

## Pull request checklist

- The change is focused and does not include unrelated edits
- The app builds and runs
- New user-facing text is added to `En` and, where possible, to the other language folders in `Services/Localization/` (see [Translations](#translations))

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).