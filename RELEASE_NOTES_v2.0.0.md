# HyPrism v2.0.0 Release Notes

## 🎉 Major Rewrite: v1.0 → v2.0

HyPrism v2.0.0 represents a **complete rewrite** of the launcher from Go/Wails to C#/Photino.NET. This brings improved performance, better maintainability, and new features.

## ⚠️ **IMPORTANT: Manual Installation Required**

**Auto-update from v1.0.x to v2.0.0 is NOT supported.**

If you're upgrading from v1.0.28 or earlier:

1. **Download** the new v2.0.0 installer for your platform from [GitHub Releases](https://github.com/yyyumeniku/HyPrism/releases)
2. **Close** the old v1.0.x launcher completely
3. **Delete** or uninstall the old v1.0.x version
4. **Install** the new v2.0.0 version
5. Your game files and mods will be preserved in the instance directory

### Why Manual Installation?

v2.0.0 uses a completely different technology stack and application structure. The auto-update mechanism from v1.0.x cannot handle this major change. **Attempting to use the in-app "Update" button from v1.0.x will fail and may damage your installation.**

## 🆕 New Features in v2.0.0

### UUID Editor
- Added "Our secret" button to edit your player UUID
- Random UUID generator with one click
- Persistent UUID storage across sessions

### Technical Improvements
- **Complete C# rewrite** using .NET 8.0 and Photino.NET
- **Better performance** with native .NET libraries
- **Improved stability** on all platforms
- **Single-file executables** with embedded dependencies
- **Modern React frontend** with TypeScript and Vite

## 📦 Platform Support

### macOS
- **ARM64 only** (Apple Silicon M1/M2/M3)
- Includes helper script to clear quarantine attributes
- Distributed as `.dmg` file

### Linux
- Multiple package formats:
  - `.deb` (Debian/Ubuntu)
  - `.rpm` (Fedora/RHEL)
  - `.AppImage` (universal)
  - `.flatpak` (Flatpak)
- x86_64 architecture

### Windows
- x64 architecture
- Distributed as `.zip` file

## 🔧 Known Issues

### Auto-Update Not Implemented
The auto-update feature shown in the UI is **not yet functional** in v2.0.0. Clicking "Update Available" will show a message directing you to manually download updates from GitHub releases.

This will be implemented in a future v2.1.x release.

### Migration from v1.0.x
Some users report needing to re-login or reconfigure settings after upgrading. Your game files and mods should be preserved, but launcher settings may need to be re-entered.

## 🐛 Bug Fixes

- Fixed macOS "damaged or incomplete" errors from GitHub Actions builds
- Fixed DMG volume name conflicts
- Removed problematic code signing that caused false positives
- Fixed asset extraction on macOS app bundles

## 📝 Breaking Changes

- **No auto-update from v1.x**: Manual installation required
- **ARM64 only on macOS**: Intel Macs are not supported in v2.0.0 (use v1.0.28)
- **Different config location**: Settings may need to be re-entered

## 🙏 Acknowledgments

Thank you to all users who tested the beta releases and reported issues!

---

**Download:** [GitHub Releases](https://github.com/yyyumeniku/HyPrism/releases)
**Report Issues:** [GitHub Issues](https://github.com/yyyumeniku/HyPrism/issues)
