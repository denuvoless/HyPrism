# Configuration

HyPrism stores settings locally in JSON format. Most settings can be changed through the GUI, but power users can edit files directly.

---

## 📁 Configuration Location

| Platform | Path |
|----------|------|
| **Windows** | `%APPDATA%\HyPrism\` |
| **Linux** | `~/.config/HyPrism/` |
| **macOS** | `~/Library/Application Support/HyPrism/` |

---

## 📄 Key Files

### config.json

Main user configuration file.

```json
{
  "UUID": "550e8400-e29b-41d4-a716-446655440000",
  "Nick": "Player",
  "VersionType": "release",
  "AccentColor": "#FFA845",
  "Language": "en-US",
  "DiscordRPC": true,
  "Background": null,
  "LaunchAfterDownload": true,
  "CloseLauncherOnPlay": false
}
```

| Key | Type | Description |
|-----|------|-------------|
| `UUID` | string | Unique user identifier |
| `Nick` | string | Display player name |
| `VersionType` | string | `"release"` or `"pre-release"` |
| `AccentColor` | string | HEX accent color code |
| `Language` | string | Language code (must match file in `Assets/Locales/`) |
| `DiscordRPC` | bool | Enable Discord Rich Presence |
| `Background` | string? | Path to custom background (null = default) |
| `LaunchAfterDownload` | bool | Auto-launch game after download |
| `CloseLauncherOnPlay` | bool | Close launcher when game starts |

---

### profiles/

User profiles folder.

```
profiles/
├── default.json
├── profile_2.json
└── profile_3.json
```

**Profile structure:**
```json
{
  "UUID": "550e8400-e29b-41d4-a716-446655440000",
  "Nickname": "MyProfile",
  "SkinPath": "/path/to/skin.png",
  "CreatedAt": "2024-01-15T12:00:00Z",
  "LastUsed": "2024-06-20T18:30:00Z"
}
```

---

### Instances/

Installed game versions folder.

```
Instances/
├── release/
│   ├── latest/              # Auto-updated version
│   │   ├── game.exe
│   │   └── ...
│   └── v123/                # Pinned version
└── pre-release/
    └── latest/
```

---

### Logs/

Application logs.

```
Logs/
├── 2024-06-20_12-30-45.log  # Current session
└── 2024-06-19_10-15-00.log  # Previous sessions
```

**Check logs when:**
- Launcher crashes
- Patching errors
- Download issues

---

## ⚙️ Settings via GUI

### Accessing Settings

1. Click the ⚙️ icon in the main window
2. Or use a hotkey (if configured)

### Available Settings

| Section | Settings |
|---------|----------|
| **General** | Language, Discord RPC, auto-launch |
| **Appearance** | Accent color, background |
| **Game** | Version type, launch parameters |
| **Profile** | Nickname, skin |

---

## 🎨 Accent Color

HyPrism supports custom accent color.

### Via GUI

1. Open settings
2. Select color from palette or enter HEX code

### Via config.json

```json
{
  "AccentColor": "#FF5500"
}
```

### Via ThemeService (programmatically)

```csharp
ThemeService.Instance.ApplyAccentColor("#FF5500");
```

---

## 🌍 Interface Language

### Supported Languages

| Code | Language |
|------|----------|
| `en-US` | English |
| `ru-RU` | Русский |
| `de-DE` | Deutsch |
| `es-ES` | Español |
| `fr-FR` | Français |
| `ja-JP` | 日本語 |
| `ko-KR` | 한국어 |
| `pt-BR` | Português (Brasil) |
| `tr-TR` | Türkçe |
| `uk-UA` | Українська |
| `zh-CN` | 简体中文 |
| `be-BY` | Беларуская |

### Changing Language

1. **Via GUI:** Settings → Language → Select
2. **Via config.json:**
   ```json
   {
     "Language": "en-US"
   }
   ```

No restart required — language applies instantly.

---

## 🔒 Security

### Sensitive Data

⚠️ **Do not share these files:**

- `config.json` — contains UUID
- `profiles/*.json` — contains profile data
- `auth.json` (if present) — authorization tokens

### Reset Configuration

For complete reset, delete the configuration folder:

```bash
# Windows
rmdir /s %APPDATA%\HyPrism

# Linux/macOS
rm -rf ~/.config/HyPrism
```

---

## 📚 Additional Resources

- [Installation.md](Installation.md) — Installation
- [Features.md](../General/Features.md) — Features
