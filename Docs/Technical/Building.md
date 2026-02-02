# Building HyPrism

This guide provides detailed instructions on compiling HyPrism from source.

## Prerequisites

Ensure your development environment has the following installed:

1.  **Framework:** .NET 10.0 SDK (Preview) or .NET 8.0/9.0 (if targeting LTS, check `global.json` if available).
2.  **OS:**
    *   **Windows:** 10 (1809+) or 11.
    *   **Linux:** Modern distros (Ubuntu 22.04+, Fedora 38+, Arch).
    *   **macOS:** macOS 12 Monterey or newer.
3.  **Tools:**
    *   Git.
    *   IDE: Visual Studio 2022, JetBrains Rider, or VS Code (with C# Dev Kit).

## Build Instructions

### Standard Debug Build

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/your-repo/HyPrism.git
    cd HyPrism
    ```

2.  **Restore NuGet packages:**
    ```bash
    dotnet restore
    ```

3.  **Build:**
    ```bash
    dotnet build
    ```

4.  **Run:**
    ```bash
    dotnet run
    ```
    *Note: On Linux, if you encounter rendering issues, run with `DOTNET_ROLL_FORWARD=Major` if using a preview runtime.*

### Release / Publish

To create a standalone executable for distribution:

**Windows (x64):**
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

**Linux (x64):**
```bash
dotnet publish -c Release -r linux-x64 --self-contained
```

**macOS (ARM64/Apple Silicon):**
```bash
dotnet publish -c Release -r osx-arm64 --self-contained
```

## Linux Specifics

### Dependencies
Avalonia relies on SkiaSharp, which requires native libraries on Linux. Ensure these are installed:

```bash
# Ubuntu/Debian
sudo apt install libfontconfig1 libice6 libsm6 libx11-6 libxext6 libxrender1
```

### Flatpak
We provide Flatpak manifests in `packaging/flatpak/`.
To build the Flatpak locally (requires `flatpak-builder`):

```bash
flatpak-builder --user --install build-dir packaging/flatpak/dev.hyprism.HyPrism.json
```

## Troubleshooting

*   **SkiaSharp Errors (Linux):** Ensure `libSkiaSharp.so` can be found. You may need to install `SkiaSharp.NativeAssets.Linux` NuGet package or install `libskia` system-wide.
*   **"Preview" Runtime needed:** If the `.csproj` specifies `net10.0` and you only have `net8.0`, you must install the .NET 10 Preview SDK from Microsoft.
