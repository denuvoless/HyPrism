# Installation Guide

## System Requirements

| | Minimum | Recommended |
|---|---|---|
| **OS** | Windows 10, Linux (Modern), macOS 12 | Windows 11, Ubuntu 22.04 LTS, macOS 14 |
| **RAM** | 2 GB | 4 GB |
| **Disk** | 200 MB (Launcher only) | 10 GB+ (Launch + Game) |
| **Graphics** | DirectX 10 / OpenGL 3.3 | DirectX 11+ / Vulkan |

---

## Windows Installation

1.  **Download:** Navigate to the [Releases Page](#) and download `HyPrism-Setup.exe`.
2.  **Run Installer:** Double-click the file. Windows SmartScreen may warn you if the binary is unsigned; click "Run Anyway".
3.  **Finish:** HyPrism will launch automatically after installation. A shortcut will be placed on your Desktop.

---

## Linux Installation

### Option A: Flatpak (Recommended)
This ensures all dependencies are bundled and sandbox isolation.

```bash
# Add Flathub remote if you haven't (optional, depends on distribution)
flatpak remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo

# Install HyPrism (Assuming it's published or you have the local ref)
flatpak install dev.hyprism.HyPrism
```

### Option B: Portable Binary
1.  Download `HyPrism-linux-x64.tar.gz`.
2.  Extract the archive:
    ```bash
    tar -xzf HyPrism-linux-x64.tar.gz
    cd HyPrism
    ```
3.  Make executable and run:
    ```bash
    chmod +x HyPrism
    ./HyPrism
    ```
    *Note: You may need to install `libICU` or `libssl` depending on your distro.*

---

## macOS Installation

1.  **Download:** Get the `HyPrism.dmg` file.
2.  **Mount:** Double-click the `.dmg`.
3.  **Install:** Drag the **HyPrism** icon into the **Applications** folder shortcut.
4.  **Security Notice:** On first launch, macOS might block the app because it's from an "Unidentified Developer".
    *   Go to **System Settings** > **Privacy & Security**.
    *   Scroll down and click **"Open Anyway"** for HyPrism.
