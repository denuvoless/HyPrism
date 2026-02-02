# System Architecture

HyPrism follows the **Model-View-ViewModel (MVVM)** architectural pattern, strictly separating the User Interface (View) from the Business Logic (Model/Service) via an intermediary State Layer (ViewModel).

## High-Level Overview

```mermaid
graph TD
    User[User Interaction] --> View[Avalonia View (.axaml)]
    View -->|Data Binding| ViewModel[ReactiveViewModel]
    ViewModel -->|Commands| Service[Backend Service]
    Service -->|Status Updates| ViewModel
    Service -->|IO Operations| Disk/Network
```

## Layers Breakdown

### 1. Presentation Layer (UI)
Located in `UI/`.
*   **Views:** Pure XAML definitions of the interface. Code-behind files (`.axaml.cs`) are minimal and only handle view-specific concerns (like window dragging).
*   **ViewModels:** Classes inheriting from `ViewModelBase` (using `CommunityToolkit.Mvvm`). They expose `ObservableProperty` fields that the View binds to.
*   **Converters:** Value converters (e.g., `BooleanToVisibility`, `StringEquality`) to transform data for display.

### 2. Application Logic (ViewModels)
This layer acts as the glue.
*   **Reactive Commands:** Actions are represented as `ICommand` (often `RelayCommand` or `ReactiveCommand`).
*   **State Management:** Holds the runtime state of the application (e.g., `IsDownloading`, `CurrentProgress`, `UserProfile`).

### 3. Service Layer (Backend)
Located in `Backend/`. This is where the heavy lifting happens.
*   **`AppService`:** The central coordinator. Manages the initialization sequence, game launch, and inter-service communication.
*   **`AuthService`:** Handles JWT tokens, login requests, and refresh cycles.
*   **`ClientPatcher`:** A specialized service for binary manipulation. It reads the game executable, searches for byte patterns, and applies patches.
*   **`DiscordService`:** Wraps the Discord RPC C++ library to update presence.
*   **`Logger`:** A centralized logging singleton that writes to `run.log`.

## Core Libraries & Dependencies

*   **Avalonia UI:** The rendering engine.
*   **ReactiveUI:** We rely heavily on ReactiveUI's `WhenAnyValue` and `ObservableAsPropertyHelper` for complex state dependencies.
*   **CommunityToolkit.Mvvm:** specificallly the Source Generators (`[ObservableProperty]`) to reduce boilerplate code in ViewModels.
*   **Newtonsoft.Json:** Used for parsing API responses and local config files.

## Data Flow Example: Launching the Game

1.  **View:** User clicks "Play". `Button` invokes `LaunchCommand` in `MainViewModel`.
2.  **ViewModel:** `LaunchCommand` sets `IsBusy = true` and calls `AppService.LaunchGameAsync()`.
3.  **AppService:** 
    *   Validates Game Files (`ClientPatcher.Verify()`).
    *   Downloads updates if necessary (`UpdateService`).
    *   Constructs the process start info.
    *   Starts the game process.
4.  **ViewModel:** Subscribes to `AppService` events to update the progress bar.
5.  **View:** Progress bar updates automatically via binding.
