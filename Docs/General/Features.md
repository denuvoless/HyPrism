# Features

HyPrism provides a comprehensive suite of features designed for both casual players and power users.

## 👤 Identity & Profile Management
*   **Unified Authentication:** Secure login system supporting multiple backend providers.
*   **Profile Customization:** Users can update their display name (Nick) and avatar directly within the launcher.
*   **Session Persistence:** Secure token storage allows users to stay logged in across restarts without compromising security.

## 🎮 Game Client Management
*   **Smart Patching (`ClientPatcher`):**
    *   **Binary Modifications:** Capable of applying binary patches to the game executable to fix bugs or enable features.
    *   **Integrity Verification:** Scans game files using cryptographic hashes to ensure they verify against the server manifest.
    *   **Differential Updates:** Downloads only the changed parts of files to save bandwidth (planned/supported).
*   **Launch Options:** configurable JVM arguments (if applicable) or launch flags.

## 🖥️ User Interface & UX
*   **Modern Design:** A "Glassy" and dark-themed UI that fits modern OS aesthetics.
*   **Responsive Layout:** The interface adapts gracefully to different window sizes.
*   **Modal System:** Error reporting, confirmations, and settings are handled via a unified overlay system.
*   **Theming:** Support for dynamic accent colors (loaded from `App.axaml`).

## 🌐 Integrations
*   **Discord RPC:** 
    *   Real-time status updates (e.g., "In Launcher", "Playing Game").
    *   Displays current game mode or map (extensible).
*   **News Feed:** Fetches and renders project news directly in the launcher using Markdown or HTML.

## 🌍 Localization (I18n)
*   **Dynamic Language Switching:** Change the interface language instantly without restarting the application.
*   **Community Translations:** JSON-based localization files allow the community to easily contribute translations.
*   **Fallbacks:** Automatic fallback to English strings if a translation key is missing.
