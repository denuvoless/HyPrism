# Configuration

HyPrism stores its configuration locally in JSON format. While most settings can be changed in the GUI, power users may want to edit the files directly.

## Configuration Location

| Platform | Path |
|----------|------|
| **Windows** | `%APPDATA%\HyPrism\` (e.g., `C:\Users\Name\AppData\Roaming\HyPrism\`) |
| **Linux** | `~/.config/HyPrism/` |
| **macOS** | `~/Library/Application Support/HyPrism/` |

## Key Files

### `settings.json`
Stores the main user preferences.

```json
{
  "GamePath": "/home/user/Games/MyGame",
  "Language": "en-US",
  "Theme": "Dark",
  "JavaArgs": "-Xmx4G -Xms2G",
  "CloseLauncherOnPlay": true
}
```

| Key | Description |
|-----|-------------|
| `GamePath` | Absolute path to where the game client is installed. |
| `Language` | ISO code of the active language (must match a file in `Assets/Locales`). |
| `JavaArgs` | (If applicable) Custom arguments passed to the game process. |

### `auth.json` (or similar secure storage)
Contains encrypted or serialized session tokens. **Do not share this file.** It grants access to your account.

### `logs/`
This directory contains `run.log` and `launcher.log`.
*   **run.log:** Detailed debug information from the current session.
*   **last_run.log:** A backup of the previous session's log.

Check these files if the launcher crashes or fails to patch.
