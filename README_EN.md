# CS2_Translate_Mod

[![Version](https://img.shields.io/badge/version-v1.1.3-blue)]()
[![.NET](https://img.shields.io/badge/.NET_Framework-4.7.2-purple)]()
[![License](https://img.shields.io/badge/license-see_LICENSE-green)](LICENSE)

> **[日本語版 README はこちら](README.md)**

A translation loader & extractor mod for Cities: Skylines 2.  
Simply place translation JSON files in the `Translations` folder to translate other mods' UI into any language.  
By default, translations are injected into the Japanese locale (`ja-JP`), but the system supports any locale.  
It can also **automatically extract localization keys per mod** using a multi-phase approach to generate translation templates.

---

## Features

### Translation Loading & Injection

- **Auto-loading of translation JSONs** — Automatically detects and loads `.json` files in the `Translations` folder (recursively searches subdirectories)
- **Locale injection** — Injects translations into the game's localization system (default: `ja-JP`; supports any locale ID)
- **Multi-mod support** — Loads multiple translation files simultaneously (up to 500 files, 50 MB each)
- **Auto re-injection on locale change** — Automatically reapplies translations with debounce (3 seconds) when the game language is switched
- **Manual reload** — Reload translations from the in-game settings screen

### Translation Key Extraction

- **Multi-phase per-mod key extraction** — Extracts localization keys from installed mods using a 3-phase approach
  - **Phase 1**: Extracts from `m_UserSources` grouped by assembly info per mod (recommended, highest accuracy)
  - **Phase 1.5**: Supplements missed mod keys from `activeDictionary` (rescues entries registered via MemorySource, etc.)
  - **Phase 2**: Fallback — groups all entries from `activeDictionary` by key pattern analysis (only runs if Phase 1 + 1.5 yields 0 results)
- **Automatic vanilla key exclusion** — Automatically identifies and excludes base game localization keys (~70 categories)
- **Self-injection source exclusion** — Automatically excludes translation sources injected by this mod itself
- **Existing translation merge** — Preserves existing translations when re-extracting; creates `.bak` backup on file corruption

### Optimization & Safety

- **Event generation filter** — Skips redundant processing of already-handled locale change events
- **Dictionary fingerprint** — Skips re-injection when translation content is identical to the previous run
- **Callback suppression** — Suppresses locale change callbacks during injection to prevent recursive loops
- **Debug logging** — Detailed diagnostics for translation loading, injection, and extraction

---

## Project Structure

```
CS2_Translate_Mod/
├── src/                                  # Source code
│   ├── Mod.cs                            # Entry point (IMod implementation)
│   ├── Setting.cs                        # In-game settings (Options UI)
│   ├── Models/
│   │   ├── TranslationData.cs            # Translation JSON data model
│   │   └── ExtractionResult.cs           # Extraction result summary model
│   ├── Utils/
│   │   └── TranslationLoader.cs          # JSON file loading, parsing & dictionary building
│   ├── Localization/
│   │   └── LocalizationInjector.cs       # Localization injection & source management
│   ├── Extraction/
│   │   └── TranslationExtractor.cs       # Multi-phase per-mod translation key extraction
│   └── Systems/
│       ├── TranslationLoaderSystem.cs     # ECS system (loading, injection control & optimization)
│       └── TranslationExtractorSystem.cs  # ECS system (extraction control)
├── Translations/                         # Translation JSON files directory
├── docs/
│   └── TROUBLESHOOTING.md                # Troubleshooting notes
├── CHANGELOG.md                          # Change log
├── CS2_Translate_Mod.csproj              # Project file
├── CS2_Translate_Mod.sln                 # Solution file
├── Directory.Build.props.template        # Game path configuration template
├── README.md                             # Japanese README
├── README_EN.md                          # English README (this file)
└── LICENSE
```

---

## Setup

### 1. Configure Game Path

Copy `Directory.Build.props.template` to `Directory.Build.props` and update the path to your Cities: Skylines 2 installation:

```xml
<Project>
  <PropertyGroup>
    <GamePath>C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II</GamePath>
  </PropertyGroup>
</Project>
```

### 2. Build

```bash
dotnet build
```

### 3. Install

Copy the build output to the CS2 mod directory.  
The mod references `Application.persistentDataPath`, so place files at the following path:

```
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CS2_Translate_Mod\
├── CS2_Translate_Mod.dll
└── Translations/
    └── (translation JSON files)
```

> **Note**: This is `AppData\LocalLow`, NOT `%LOCALAPPDATA%` (AppData\Local).

---

## Translation JSON Format

```json
{
  "modId": "_extracted_mod_Anarchy",
  "modName": "Extracted from mod_Anarchy",
  "version": "2026-02-20",
  "entries": {
    "Options.SECTION[Anarchy.Anarchy.AnarchyMod]": {
      "original": "Anarchy",
      "translation": "アナーキー"
    },
    "Options.OPTION_DESCRIPTION[Anarchy.Anarchy.AnarchyMod.AnarchyToggle]": {
      "original": "Enables Anarchy mode which allows placing buildings without restrictions.",
      "translation": "建物の配置制限を無視するアナーキーモードを有効にします。"
    }
  }
}
```

| Field | Description |
|---|---|
| `modId` | Mod identifier (metadata) |
| `modName` | Mod name (metadata) |
| `version` | Translation file version |
| `entries` | Dictionary of translation entries |
| `entries[key].original` | Original text (from the source locale; English by default) |
| `entries[key].translation` | Translated text (target language). Empty string `""` is treated as untranslated and skipped |

---

## Usage

1. Place translation JSON files for the target mod in the `Translations` folder
2. Launch the game (or press the "Reload Translations" button in settings)
3. Translations are automatically applied to the target locale (default: Japanese)
4. When switching game language, translations are automatically re-injected after a 3-second debounce

> **Note**: The default injection target is `ja-JP` (Japanese). To inject into other locales, you can specify any locale ID through `LocalizationInjector` in the source code.

### In-Game Settings

The following options are available under the "Mod Translation" section in the Options screen:

| Section | Setting | Description |
|---|---|---|
| Settings | Enable Translation | Enable/disable translation loading |
| Settings | Enable Debug Log | Detailed log output (diagnostics for loading, injection, extraction) |
| Settings | Reload Translations | Reload and apply translation files immediately |
| Extraction | **Extract Translation Keys** | **Extract localization keys from installed mods, grouped by mod** |

---

## Translation Key Extraction (Per-Mod)

This mod can automatically extract localization keys from installed mods, **grouped by mod**.

### Multi-Phase Extraction

Extraction runs in three phases to maximize mod identification accuracy:

| Phase | Method | Description |
|---|---|---|
| **Phase 1** | Source-based (recommended) | Extracts from `LocalizationManager`'s `m_UserSources` per source. Groups by assembly info per mod. Highest accuracy. |
| **Phase 1.5** | Supplementary | Supplements missed mod keys from `activeDictionary`. Rescues entries registered via MemorySource, etc. |
| **Phase 2** | Dictionary-based (fallback) | Only runs if Phase 1 + 1.5 yields 0 results. Groups all `activeDictionary` entries by key pattern analysis. |

- Base game (vanilla) keys are automatically excluded via **assembly info** and **key prefix patterns** (~70 categories)
- Translation sources injected by this mod (MemorySource) are identified via `IsOurSource()` and skipped

### How to Extract

1. Launch the game with the target mods enabled, then go to  
   Options → "Mod Translation" → "Extraction" section → "Extract Translation Keys" button
2. JSON files are generated per mod in the `Translations` folder

### Example Output

```
Translations/
├── _extracted_mod_Anarchy.json         # Anarchy mod translation template
├── _extracted_mod_FindIt.json          # Find It mod translation template
├── _extracted_mod_ExtraLib.json        # Extra Lib mod translation template
└── _extracted_mod__ungrouped.json      # Keys with unidentified mod origin
```

### Existing Translation Merge

If translation files already exist, extraction **will not overwrite existing translations**.  
New keys are added while existing translations are preserved.  
If an existing file fails to load, a `.bak` backup is automatically created.

---

## Creating Translation Files

To create a translation file for a new mod:

1. Press the "Extract Translation Keys" button in the in-game settings
2. JSON files are automatically generated per mod in the `Translations` folder
3. Fill in the `"translation"` fields in the generated JSON files with your translations
4. Press "Reload Translations" in the in-game settings (or restart the game)

When creating files manually, it is recommended to include the mod name in the filename, e.g., `_modname.json`.

---

## Troubleshooting

For detailed technical issues, see [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).  
The following issues are documented:

- Assembly path resolution issues (specific to CS2's asset system)
- Settings UI localization key mismatches
- `onActiveDictionaryChanged` callback spam
- Reflection-based access to `LocalizationManager` internals

---

## License

See [LICENSE](LICENSE).
