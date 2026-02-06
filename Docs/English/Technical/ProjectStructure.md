# Project Structure

The HyPrism codebase is organized for clear separation of concerns.

> **Note:** After migrating to Avalonia UI, the structure has changed significantly. See [MigrationGuide.md](MigrationGuide.md).

---

## Table of Contents

- [Root Structure](#-root-structure)
- [UI — Presentation Layer](#-ui--presentation-layer)
- [Services — Service Layer](#-services--service-layer)
- [Models — Data Models](#-models--data-models)
- [Assets — Resources](#-assets--resources)
- [Packaging](#-packaging)
- [Scripts](#-scripts)
- [Key Files](#-key-files)
- [Naming Conventions](#-naming-conventions)

---

## 📁 Root Structure

```
HyPrism/
├── Assets/                 # Resources (icons, locales, images)
├── Docs/                   # Project documentation
├── Models/                 # Data models
├── Packaging/              # OS packaging
├── Scripts/                # CI/CD and utility scripts
├── Services/               # Service layer (Core, Game, User)
├── UI/                     # Presentation layer (Avalonia)
├── Bootstrapper.cs         # DI container initialization
├── Program.cs              # Entry point
├── HyPrism.csproj          # .NET project file
└── HyPrism.sln             # Solution file
```

---

## 🎨 UI/ — Presentation Layer

```
UI/
├── App.axaml               # Root Application (resources, styles)
├── App.axaml.cs            # Application initialization
├── MainWindow/             # Main window
│   ├── MainWindow.axaml
│   ├── MainWindow.axaml.cs
│   └── MainViewModel.cs
├── Components/             # Reusable UI components
│   ├── Buttons/
│   │   ├── CloseButton/
│   │   ├── IconButton/
│   │   └── PrimaryButton/
│   ├── Cards/
│   │   ├── NewsCard/
│   │   └── NoticeCard/
│   ├── Common/             # Common components
│   ├── Dashboard/          # Dashboard components
│   ├── Inputs/             # Input fields
│   ├── Layouts/            # Layout components
│   └── Navigation/         # Navigation elements
├── Views/                  # Full-screen views
│   ├── DashboardView/
│   │   ├── DashboardView.axaml
│   │   └── DashboardViewModel.cs
│   ├── SettingsView/
│   ├── ProfileEditorView/
│   ├── ModManagerView/
│   ├── NewsView/
│   └── LoadingView/
├── Styles/                 # Global styles
│   ├── BaseControlStyles.axaml
│   ├── CommonAnimations.axaml
│   ├── DropdownInputStyles.axaml
│   └── SharedColors.axaml
├── Converters/             # Value Converters
├── Behaviors/              # Avalonia Behaviors
├── Helpers/                # UI helper classes
└── Transitions/            # Transition animations
```

### UI Organization Principles

1. **Components** (`Components/`) — atomic, reusable elements
2. **Views** (`Views/`) — full-screen views
3. **Styles** (`Styles/`) — global styles, not component-specific
4. **Each component in its own folder** with `.axaml` and `.axaml.cs` together

---

## ⚙️ Services/ — Service Layer

```
Services/
├── Core/                   # Infrastructure services
│   ├── BrowserService.cs
│   ├── ConfigService.cs
│   ├── DiscordService.cs
│   ├── FileDialogService.cs
│   ├── FileService.cs
│   ├── GitHubService.cs
│   ├── LocalizationService.cs
│   ├── Logger.cs
│   ├── NewsService.cs
│   ├── ProgressNotificationService.cs
│   ├── RosettaService.cs
│   ├── SettingsService.cs
│   ├── ThemeService.cs
│   ├── UpdateService.cs
│   └── UtilityService.cs
├── Game/                   # Game services
│   ├── AssetService.cs
│   ├── AuthService.cs
│   ├── AvatarService.cs
│   ├── ButlerService.cs
│   ├── ClientPatcher.cs
│   ├── DownloadService.cs
│   ├── GameProcessService.cs
│   ├── GameSessionService.cs
│   ├── InstanceService.cs
│   ├── LanguageService.cs
│   ├── LaunchService.cs
│   ├── ModService.cs
│   └── VersionService.cs
└── User/                   # User services
    ├── ProfileManagementService.cs
    ├── ProfileService.cs
    ├── SkinService.cs
    └── UserIdentityService.cs
```

### Service Categories

| Category | Purpose | Examples |
|----------|---------|----------|
| **Core** | Base infrastructure | Logger, ConfigService, LocalizationService |
| **Game** | Game operations | LaunchService, VersionService, ModService |
| **User** | User data | ProfileService, SkinService |

---

## 📦 Models/ — Data Models

```
Models/
├── CommonModels.cs         # Common models
├── Config.cs               # Application configuration
├── CurseForgeModels.cs     # CurseForge API models
├── DiscordModels.cs        # Discord models
├── HytaleNewsModels.cs     # Hytale news models
├── InstalledInstance.cs    # Installed instance
├── LatestInstanceInfo.cs   # Latest instance info
├── LatestVersionInfo.cs    # Latest version info
├── ModModels.cs            # Mod models
├── NewsItemResponse.cs     # News API response
├── Profile.cs              # User profile
├── UpdateInfo.cs           # Update information
└── VersionCache.cs         # Version cache
```

### Principle

Models are **POCO classes** (Plain Old CLR Objects):
- No business logic
- Only data and properties
- JSON serializable

---

## 🖼️ Assets/ — Resources

```
Assets/
├── Icons/                  # SVG icons
│   └── Flags/              # Country flags
├── Images/                 # Images
│   └── Backgrounds/        # Background images
└── Locales/                # Localization files (JSON)
    ├── en-US.json
    ├── ru-RU.json
    ├── de-DE.json
    └── ...
```

### ⚠️ Legacy: assets/

The `assets/` folder (lowercase) contains deprecated resources:
- Old `.lang` localization files

**Do not use** this folder for new resources!

---

## 📦 Packaging/

```
Packaging/
├── flatpak/                # Linux Flatpak
│   ├── dev.hyprism.HyPrism.desktop
│   ├── dev.hyprism.HyPrism.json
│   └── dev.hyprism.HyPrism.metainfo.xml
├── macos/                  # macOS
│   └── Info.plist
└── windows/                # Windows
    └── (setup scripts)
```

---

## 🔧 Scripts/

```
Scripts/
├── build-linux.sh          # Linux build
├── Dockerfile.build        # Docker for building
├── run-discord-bot.sh      # Discord bot launch
├── run.sh                  # Application launch
└── update_locales.py       # Localization update
```

---

## 📄 Key Files

### Program.cs

Application entry point:
```csharp
public static void Main(string[] args)
{
    BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
}
```

### Bootstrapper.cs

DI container initialization:
```csharp
public static IServiceProvider Initialize()
{
    var services = new ServiceCollection();
    // Register all services and ViewModels
    return services.BuildServiceProvider();
}
```

### App.axaml

Root application resource:
- Global styles
- Color palette
- Merged Resource Dictionaries

### HyPrism.csproj

Project configuration:
- Target Framework: `net10.0`
- Avalonia and dependencies
- Build properties

---

## 📁 Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Resource folders | PascalCase | `Assets/`, `Icons/` |
| Scripts | lowercase-with-dashes | `build-linux.sh` |
| Components | PascalCase in own folder | `Components/Buttons/PrimaryButton/` |
| Views | PascalCase + "View" | `DashboardView/` |
| Services | PascalCase + "Service" | `GameSessionService.cs` |

---

## 📚 Additional Resources

- [Architecture.md](Architecture.md) — System architecture
- [CodingStandards.md](../Development/CodingStandards.md) — Coding standards
- [UIComponentGuide.md](../Development/UIComponentGuide.md) — Creating components
