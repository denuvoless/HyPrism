# Coding Standards

To ensure code quality, maintainability, and consistency, all contributors must adhere to the following standards.

## General C# Guidelines

*   **Language Version:** Use the latest available C# features (currently matching .NET 10).
*   **Formatting:** Use the standard Visual Studio / .NET default formatting configuration (`.editorconfig` is preferred).
    *   Indent with 4 spaces.
    *   Braces on new lines (Allman style).
*   **Naming Conventions:**
    *   `PascalCase` for Classes, Methods, Properties, Structs.
    *   `camelCase` for local variables and parameters.
    *   `_camelCase` for private fields.
    *   `IPrefix` for Interfaces (e.g., `IService`).

## Architecture & Design

### MVVM (Model-View-ViewModel)
*   **Strict Separation:** ViewModels must NOT reference Avalonia Controls directly. Do not use `Control`, `Window`, or `Button` types in VM code.
*   **Properties:** Use `[ObservableProperty]` from `CommunityToolkit.Mvvm` instead of writing full boilerplate with `OnPropertyChanged`.
*   **Commands:** Use `[RelayCommand]` for actions.

### Asynchrony
*   Use `async`/`await` for all I/O bound operations.
*   Avoid `Task.Run` inside library methods unless calculating heavy CPU loads.
*   Suffix async methods with `Async` (e.g., `LoadProfileAsync`).
*   **Always** configure awaiters if strictly library code (though in UI apps, context capture is usually desired).

### Null Safety
*   **Nullable References:** Project-wide `<Nullable>enable</Nullable>` is active.
*   Avoid using `!` (null-forgiving operator) unless you are absolutely certain the value exists (e.g., verified by external logic).

## Application Layers

*   **UI Layer:** Should only contain display logic. Complex calculations go to Backend.
*   **Backend Services:** Should be stateless singleton services where possible. State should reside in Model objects or specific session managers.

## Comments & Documentation

*   Public APIs should have XML documentation (`/// <summary>`).
*   Comments should explain *Why*, not *What* (code explains what).
