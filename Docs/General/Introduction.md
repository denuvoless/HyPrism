# Introduction to HyPrism

**HyPrism** is a high-performance, cross-platform game launcher designed to provide a seamless bridge between the user and the game client. Built upon the bleeding edge of the .NET ecosystem, it leverages .NET 10 and Avalonia UI to deliver a native, responsive, and aesthetically pleasing experience across Windows, Linux, and macOS.

Unlike traditional Electron-based launchers that consume significant RAM and CPU resources, HyPrism is compiled to native code, ensuring minimal footprint and maximum performance.

## Core Philosophy

*   **Performance First:** Every subsystem, from the UI rendering to the file patching algorithm, is optimized for speed and low latency.
*   **True Cross-Platform:** A single codebase that compiles and runs natively on all major desktop operating systems without emulation or containers.
*   **Security & Integrity:** Advanced hashing algorithms (`SHA-256`, `MD5`) ensure that game files are authentic and unmodified before launch.
*   **Extennsibility:** A modular architecture allows for easy addition of new services, authentication providers, and UI components.

## Technology Stack

The project utilizes a modern, robust technology stack:

| Component | Technology | Version | Description |
|-----------|------------|---------|-------------|
| **Runtime** | .NET | 10.0 (Preview) | The latest available runtime for maximum performance features. |
| **Language** | C# | 13.0 | Utilization of the latest language features for cleaner code. |
| **UI Framework** | Avalonia UI | 11.3 | A declarative, XAML-based UI framework similar to WPF but cross-platform. |
| **Architecture** | MVVM | - | Model-View-ViewModel pattern for separation of concerns. |
| **Reactivity** | ReactiveUI | 11.x | Functional reactive programming for UI state management. |
| **Graphics** | SkiaSharp | - | Hardware-accelerated 2D graphics library used by Avalonia. |
| **SVG** | Avalonia.Svg.Skia | - | High-fidelity SVG rendering support. |

## Key Capabilities

*   **Binary Patching:** Capable of patching game binaries in memory or on disk to inject custom logic or fixes.
*   **Discord Integration:** Rich Presence support to display game status to friends.
*   **Asset Management:** Intelligent caching and downloading of game assets.
