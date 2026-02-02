# Project Structure

The codebase is organized to separate concerns, keeping UI logic distinct from backend operations and assets.

```
/
├── Backend/                # Core Business Logic and Services
│   ├── Services/           # Helper services and utilities
│   ├── AppService.cs       # Application lifecycle manager
│   ├── AuthService.cs      # Authentication logic
│   ├── ClientPatcher.cs    # Binary patching subsystem
│   ├── DiscordService.cs   # Discord RPC integration
│   └── Logger.cs           # Logging infrastructure
│
├── UI/                     # Presentation Layer (Avalonia)
│   ├── Components/         # Reusable UI widgets
│   │   ├── Buttons/        # Custom button controls (IconButton, etc.)
│   │   ├── Inputs/         # Text fields, checkboxes
│   │   ├── Layouts/        # Modal wrappers, grids
│   │   └── Navigation/     # Sidebar items, tabs
│   ├── Converters/         # IValueConverter implementations
│   ├── ViewModels/         # MVVM ViewModels
│   └── Views/              # .axaml Views and Windows used by the app
│
├── Assets/                 # [NEW] Resources & Localization
│   ├── Icons/              # App icons and graphics
│   ├── Locales/            # JSON translation files (Modern format)
│   ├── bg_1.jpg            # Default backgrounds
│   └── logo.png            # Branding assets
│
├── assets/                 # [LEGACY] Old Resources
│   └── game-lang/          # Legacy .lang files (Deprecated)
│
├── Docs/                   # Project Documentation
│   ├── Development/        # Coding standards & guides
│   ├── General/            # High-level overview
│   ├── Technical/          # Architecture & Deep dives
│   └── User/               # User manuals
│
├── packaging/              # OS-Specific packaging
│   ├── flatpak/            # Flatpak manifests & metadata
│   ├── macos/              # Info.plist and bundling scripts
│   └── windows/            # Setup scripts/resources
│
├── scripts/                # CI/CD and utility shell scripts
├── HyPrism.csproj          # Main .NET Project file
├── Program.cs              # Entry point (Main)
└── App.axaml               # Application root (Resources, Styles)
```

## Key Directories Explained

### `Backend/`
Contains C# classes that do not depend on Avalonia *Views*. Ideally, code here should be portable enough to run in a console application (mostly).

### `UI/`
Contains all code related to the visual presentation.
*   **Components:** Small, atomic UI parts. Example: `IconButton` is a custom control wrapper around `Button` that renders an SVG.
*   **Views:** Full screens or complex overlays. Example: `SettingsView`, `ProfileEditorView`.

### `Assets/` vs `assets/`
*   **`Assets/` (Capitalized):** This is the **current** standard for resources. It contains the updated `Locales/` folder with JSON files.
*   **`assets/` (Lowercase):** This folder contains legacy data structures, specifically `game-lang`, which were used in previous iterations of the engine/launcher. We are in the process of migrating or deprecating this folder.
