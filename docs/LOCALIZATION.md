# UI localization

## Non-negotiable rule

Localization applies to the StarSyncUniverse application interface only.

**Never translate Star Citizen/game content.** The following values always remain native exactly as delivered/resolved by their data source:

- object/location/body/system names;
- descriptions and lore text;
- factions and organizations;
- services, commodities and item names;
- mission/game terminology when it is source content rather than an application control;
- source paths, UUIDs, class names, entity types, status/provenance identifiers and raw technical records;
- bookmark names/notes entered by users.

The same rule applies to future languages.

## Current languages

- `en` — English, canonical UI fallback.
- `de` — German.

The selected language is stored as `UiLanguage` in `StarSyncUniverseSettings` and can be changed under Settings → Interface. Changing it saves settings and reloads the local renderer so the native WPF shell and the WebView UI change together.

## Translation source

All application translations live in:

`Localization/UiLocalizationCatalog.cs`

Do not scatter German/English conditionals throughout data/domain code. New UI wording should receive a stable semantic key, for example:

- `nav.settings`
- `ui.selectedObject`
- `detail.servicesCommerce`
- `settings.runIntegrity`

English is the source/fallback value. German is stored beside it.

## WPF usage

Use `UiLocalizationCatalog.T(language, key)` (or `MainWindow.Ui(key)`) for WPF application chrome. Controls that change after startup should be named in XAML and updated by `ApplySettingsUiState()`.

Do not pass game values through the localization catalog.

## WebView renderer usage

The renderer payload contains:

- `uiLanguage`
- `uiText` — key → translated UI string
- `uiExactText` — known UI source wording → translated UI string

Use `uiT('semantic.key', 'English fallback')` for newly written renderer code.

A conservative exact-text translator also handles legacy/dynamically-created UI controls. It only replaces strings that exactly match cataloged application UI text. Arbitrary values are left untouched; this is specifically intended to prevent accidental translation of game content.

## Adding a new UI string

1. Add a key and English/German values to `UiLocalizationCatalog.Entries`.
2. Use `Ui(key)` in WPF or `uiT(key, fallback)` in renderer JavaScript/templates.
3. If the old renderer already emits a static English string, adding the exact English source text to the catalog allows the exact-text compatibility layer to translate it.
4. Verify that the value is application UI and not Star Citizen content.
5. Test both `en` and `de`.

## Adding another language

The current catalog record stores English/German directly because only two languages are shipped. To add a third language cleanly:

1. change catalog entries to a language dictionary or typed translation record;
2. extend `NormalizeLanguage` with the new language code;
3. add the Settings option;
4. preserve English as fallback for missing entries;
5. leave all game-content exclusion rules unchanged.

No domain/database migration should be required because language selection is an application preference, not a property of game data.

## Review checklist

Before merging localization changes:

- UI control text switches language.
- About/help text switches language.
- Settings language selection persists.
- Search results keep native object names.
- map labels keep native object/location names.
- Selected Object values keep native names/descriptions/services/factions.
- database/workspace rows keep native source content.
- bookmarks keep user-entered text exactly as entered.
- source identifiers/technical provenance remain unchanged.
