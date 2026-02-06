# Структура проекта

Кодовая база HyPrism организована для чёткого разделения ответственности.

> **Примечание:** После миграции на Avalonia UI структура значительно изменилась. См. [MigrationGuide.md](MigrationGuide.md).

---

## Содержание

- [Корневая структура](#-корневая-структура)
- [UI/ — Презентационный слой](#-ui--презентационный-слой)
- [Services/ — Сервисный слой](#-services--сервисный-слой)
- [Models/ — Модели данных](#-models--модели-данных)
- [Assets/ — Ресурсы](#-assets--ресурсы)
- [Корневые файлы проекта](#-корневые-файлы-проекта)
- [Docs/ — Документация](#-docs--документация)
- [Packaging/ — Пакетирование](#-packaging--пакетирование)
- [Scripts/ — Скрипты автоматизации](#-scripts--скрипты-автоматизации)

---

## 📁 Корневая структура

```
HyPrism/
├── Assets/                 # Ресурсы (иконки, локализации, изображения)
├── Docs/                   # Документация проекта
├── Models/                 # Модели данных
├── Packaging/              # Пакетирование для ОС
├── Scripts/                # CI/CD и утилитарные скрипты
├── Services/               # Сервисный слой (Core, Game, User)
├── UI/                     # Презентационный слой (Avalonia)
├── Bootstrapper.cs         # Инициализация DI контейнера
├── Program.cs              # Точка входа
├── HyPrism.csproj          # Файл проекта .NET
└── HyPrism.sln             # Solution файл
```

---

## 🎨 UI/ — Презентационный слой

```
UI/
├── App.axaml               # Корневой Application (ресурсы, стили)
├── App.axaml.cs            # Инициализация приложения
├── MainWindow/             # Главное окно
│   ├── MainWindow.axaml
│   ├── MainWindow.axaml.cs
│   └── MainViewModel.cs
├── Components/             # Переиспользуемые UI компоненты
│   ├── Buttons/
│   │   ├── CloseButton/
│   │   ├── IconButton/
│   │   └── PrimaryButton/
│   ├── Cards/
│   │   ├── NewsCard/
│   │   └── NoticeCard/
│   ├── Common/             # Общие компоненты
│   ├── Dashboard/          # Компоненты дашборда
│   ├── Inputs/             # Поля ввода
│   ├── Layouts/            # Layout компоненты
│   └── Navigation/         # Навигационные элементы
├── Views/                  # Полноэкранные представления
│   ├── DashboardView/
│   │   ├── DashboardView.axaml
│   │   └── DashboardViewModel.cs
│   ├── SettingsView/
│   ├── ProfileEditorView/
│   ├── ModManagerView/
│   ├── NewsView/
│   └── LoadingView/
├── Styles/                 # Глобальные стили
│   ├── BaseControlStyles.axaml
│   ├── CommonAnimations.axaml
│   ├── DropdownInputStyles.axaml
│   └── SharedColors.axaml
├── Converters/             # Value Converters
├── Behaviors/              # Avalonia Behaviors
├── Helpers/                # UI вспомогательные классы
└── Transitions/            # Анимации переходов
```

### Принципы организации UI

1. **Компоненты** (`Components/`) — атомарные, переиспользуемые элементы
2. **Views** (`Views/`) — полноэкранные представления
3. **Стили** (`Styles/`) — глобальные стили, не специфичные для компонента
4. **Каждый компонент в своей папке** с `.axaml` и `.axaml.cs` вместе

---

## ⚙️ Services/ — Сервисный слой

```
Services/
├── Core/                   # Инфраструктурные сервисы
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
├── Game/                   # Игровые сервисы
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
└── User/                   # Пользовательские сервисы
    ├── ProfileManagementService.cs
    ├── ProfileService.cs
    ├── SkinService.cs
    └── UserIdentityService.cs
```

### Категории сервисов

| Категория | Назначение | Примеры |
|-----------|------------|---------|
| **Core** | Базовая инфраструктура | Logger, ConfigService, LocalizationService |
| **Game** | Работа с игрой | LaunchService, VersionService, ModService |
| **User** | Пользовательские данные | ProfileService, SkinService |

---

## 📦 Models/ — Модели данных

```
Models/
├── CommonModels.cs         # Общие модели
├── Config.cs               # Конфигурация приложения
├── CurseForgeModels.cs     # Модели CurseForge API
├── DiscordModels.cs        # Модели Discord
├── HytaleNewsModels.cs     # Модели новостей Hytale
├── InstalledInstance.cs    # Установленный инстанс
├── LatestInstanceInfo.cs   # Информация о последнем инстансе
├── LatestVersionInfo.cs    # Информация о последней версии
├── ModModels.cs            # Модели модов
├── NewsItemResponse.cs     # Ответ API новостей
├── Profile.cs              # Профиль пользователя
├── UpdateInfo.cs           # Информация об обновлении
└── VersionCache.cs         # Кэш версий
```

### Принцип

Модели — **POCO классы** (Plain Old CLR Objects):
- Без бизнес-логики
- Только данные и свойства
- Сериализуемые через JSON

---

## 🖼️ Assets/ — Ресурсы

```
Assets/
├── Icons/                  # SVG иконки
│   └── Flags/              # Флаги стран
├── Images/                 # Изображения
│   └── Backgrounds/        # Фоновые изображения
└── Locales/                # Файлы локализации (JSON)
    ├── en-US.json
    ├── ru-RU.json
    ├── de-DE.json
    └── ...
```

### ⚠️ Legacy: assets/

Папка `assets/` (lowercase) содержит устаревшие ресурсы:
- Старые `.lang` файлы локализации

**Не используйте** эту папку для новых ресурсов!

---

## 📦 Packaging/ — Пакетирование

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

## 🔧 Scripts/ — Скрипты

```
Scripts/
├── build-linux.sh          # Сборка для Linux
├── Dockerfile.build        # Docker для сборки
├── run-discord-bot.sh      # Запуск Discord бота
├── run.sh                  # Запуск приложения
└── update_locales.py       # Обновление локализаций
```

---

## 📄 Ключевые файлы

### Program.cs

Точка входа приложения:
```csharp
public static void Main(string[] args)
{
    BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
}
```

### Bootstrapper.cs

Инициализация DI контейнера:
```csharp
public static IServiceProvider Initialize()
{
    var services = new ServiceCollection();
    // Регистрация всех сервисов и ViewModels
    return services.BuildServiceProvider();
}
```

### App.axaml

Корневой ресурс приложения:
- Глобальные стили
- Цветовая палитра
- Merged Resource Dictionaries

### HyPrism.csproj

Конфигурация проекта:
- Target Framework: `net10.0`
- Avalonia и зависимости
- Build properties

---

## 📁 Конвенции именования

| Тип | Конвенция | Пример |
|-----|-----------|--------|
| Папки ресурсов | PascalCase | `Assets/`, `Icons/` |
| Скрипты | lowercase-with-dashes | `build-linux.sh` |
| Компоненты | PascalCase в своей папке | `Components/Buttons/PrimaryButton/` |
| Views | PascalCase + "View" | `DashboardView/` |
| Сервисы | PascalCase + "Service" | `GameSessionService.cs` |

---

## 📚 Дополнительные ресурсы

- [Architecture.md](Architecture.md) — Архитектура системы
- [CodingStandards.md](../Development/CodingStandards.md) — Стандарты кода
- [UIComponentGuide.md](../Development/UIComponentGuide.md) — Создание компонентов
