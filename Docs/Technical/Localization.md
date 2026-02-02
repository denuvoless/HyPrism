# Localization System

HyPrism features a flexible localization engine capable of switching languages at runtime. The system is transitioning from a legacy proprietary format to clear, standard JSON files.

## Directory Structure

### ✅ New System (`Assets/Locales/`)
The current active localization system uses standardized JSON files located in `Assets/Locales/`.
Each file matches an IETF language tag:
*   `en-US.json` (English - United States)
*   `ru-RU.json` (Russian)
*   `de-DE.json` (German)
*   `ja-JP.json` (Japanese)
*   ...and others.

**Format:**
```json
{
  "Launcher.Title": "HyPrism Launcher",
  "Buttons.Play": "Play",
  "Buttons.Settings": "Settings",
  "Errors.Network": "Connection failed"
}
```

### ⚠️ Legacy System (`assets/game-lang/`)
This is the deprecated format used by older versions of the engine.
Structure: `assets/game-lang/{CODE}/client.lang`
This folder is kept for backward compatibility with specific game client modules that might attempt to read legacy configs, but the Launcher itself primarily relies on the JSON files in `Assets/Locales`.

## Adding a New Language

1.  Navigate to `Assets/Locales/`.
2.  Duplicate `en-US.json` and rename it to your target locale (e.g., `pl-PL.json`).
3.  Translate the values inside the JSON object.
    *   **Guideline:** Keep string length relatively similar to English to avoid UI overflow.
4.  The application scans this directory on startup. If your file is valid JSON, the language will automatically appear in the Settings > Language dropdown.

## Implementation Details

The Localization Service (in `Backend`) loads these files into a `Dictionary<string, Dictionary<string, string>>`.
When a View requests a string (e.g., via `{Binding LocalizedResources[Buttons.Play]}`), the service looks up the key in the currently active dictionary. If the key is missing, it falls back to the `en-US` dictionary.
