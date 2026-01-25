#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="${VERSION:-2.0.0}"
ARTIFACTS="$ROOT/artifacts"
RIDS=("win-x64" "linux-x64" "osx-arm64")
AUTO_INSTALL="${AUTO_INSTALL:-0}"
REMOTE_LINUX_HOST="${REMOTE_LINUX_HOST:-${REMOTE_HOST:-}}"
REMOTE_LINUX_PATH="${REMOTE_LINUX_PATH:-${REMOTE_PATH:-~/HyPrism}}"
SKIP_REMOTE="${SKIP_REMOTE:-0}"

if [[ "$(uname -s)" == "Linux" ]]; then
  have_apt=false
  if command -v apt-get >/dev/null 2>&1; then
    have_apt=true
  fi

  ensure_tool() {
    local cmd="$1" pkg_hint="$2" post_install="$3"
    if command -v "$cmd" >/dev/null 2>&1; then
      return 0
    fi

    if [[ "$AUTO_INSTALL" == "1" && "$have_apt" == "true" ]]; then
      echo "==> Installing $pkg_hint for $cmd"
      sudo apt-get update -y && sudo apt-get install -y $pkg_hint || true
      if [[ -n "$post_install" ]]; then
        eval "$post_install" || true
      fi
    else
      echo "!! Missing $cmd. Install: $pkg_hint (set AUTO_INSTALL=1 to auto-install with apt)."
    fi
  }

  # Packaging helpers often missing on fresh VMs
  ensure_tool fpm "ruby ruby-dev rubygems build-essential rpm" "command -v fpm >/dev/null 2>&1 || sudo gem install --no-document fpm"
  ensure_tool appimagetool "appimagetool" ""
  ensure_tool flatpak-builder "flatpak flatpak-builder flatpak-builder-libs" ""
fi

mkdir -p "$ARTIFACTS"

echo "==> Building frontend"
if [[ -f "$ROOT/frontend/package-lock.json" ]]; then
  (cd "$ROOT/frontend" && npm ci)
else
  (cd "$ROOT/frontend" && npm install)
fi
(cd "$ROOT/frontend" && npm run build)

echo "==> Restoring and publishing backend"
dotnet restore "$ROOT/HyPrism.csproj"

for rid in "${RIDS[@]}"; do
  out="$ARTIFACTS/$rid/portable"
  echo "--> Publishing $rid to $out"
  dotnet publish "$ROOT/HyPrism.csproj" -c Release -r "$rid" --self-contained true \
    /p:PublishSingleFile=true /p:PublishReadyToRun=true /p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$out"

  case "$rid" in
    win-*)
      (cd "$out" && zip -9 -r "$ARTIFACTS/HyPrism-${rid}-portable.zip" .)
      ;;
    osx-*)
      (cd "$out" && zip -9 -r "$ARTIFACTS/HyPrism-${rid}-portable.zip" .)
      ;;
    linux-*)
      tar -czf "$ARTIFACTS/HyPrism-${rid}-portable.tar.gz" -C "$out" .
      ;;
  esac
done

# Linux-only packaging helpers
if [[ "$(uname -s)" == "Linux" ]]; then
  LINUX_OUT="$ARTIFACTS/linux-x64/portable"
  PKGROOT="$ARTIFACTS/linux-x64/pkgroot"
  ICON_SRC="$ROOT/packaging/flatpak/dev.hyprism.HyPrism.png"
  DESKTOP_SRC="$ROOT/packaging/flatpak/dev.hyprism.HyPrism.desktop"

  echo "==> Staging Linux package tree"
  rm -rf "$PKGROOT"
  mkdir -p "$PKGROOT/opt/hyprism" "$PKGROOT/usr/bin" "$PKGROOT/usr/share/applications" "$PKGROOT/usr/share/icons/hicolor/256x256/apps"
  cp -R "$LINUX_OUT"/* "$PKGROOT/opt/hyprism/"
  ln -sf /opt/hyprism/HyPrism "$PKGROOT/usr/bin/hyprism"
  [[ -f "$DESKTOP_SRC" ]] && cp "$DESKTOP_SRC" "$PKGROOT/usr/share/applications/dev.hyprism.HyPrism.desktop"
  [[ -f "$ICON_SRC" ]] && cp "$ICON_SRC" "$PKGROOT/usr/share/icons/hicolor/256x256/apps/dev.hyprism.HyPrism.png"

  if command -v fpm >/dev/null 2>&1; then
    echo "==> Building .deb and .rpm via fpm"
    fpm -s dir -t deb -n hyprism -v "$VERSION" -C "$PKGROOT" \
      --description "HyPrism launcher" --url "https://github.com/yyyumeniku/HyPrism" \
      -p "$ARTIFACTS/HyPrism-linux-x64.deb" .
    fpm -s dir -t rpm -n hyprism -v "$VERSION" -C "$PKGROOT" \
      --description "HyPrism launcher" --url "https://github.com/yyyumeniku/HyPrism" \
      -p "$ARTIFACTS/HyPrism-linux-x64.rpm" .
  else
    echo "!! fpm not found; skipping deb/rpm generation"
  fi

  if command -v appimagetool >/dev/null 2>&1; then
    echo "==> Building AppImage"
    APPDIR="$ARTIFACTS/linux-x64/AppDir"
    rm -rf "$APPDIR"
    mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/256x256/apps"
    cp "$LINUX_OUT/HyPrism" "$APPDIR/usr/bin/HyPrism"
    chmod +x "$APPDIR/usr/bin/HyPrism"
    [[ -f "$DESKTOP_SRC" ]] && cp "$DESKTOP_SRC" "$APPDIR/usr/share/applications/dev.hyprism.HyPrism.desktop"
    [[ -f "$ICON_SRC" ]] && cp "$ICON_SRC" "$APPDIR/usr/share/icons/hicolor/256x256/apps/dev.hyprism.HyPrism.png"
    cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
exec "$(dirname "$0")/usr/bin/HyPrism" "$@"
EOF
    chmod +x "$APPDIR/AppRun"
    (cd "$APPDIR" && ln -sf usr/share/applications/dev.hyprism.HyPrism.desktop HyPrism.desktop)
    appimagetool "$APPDIR" "$ARTIFACTS/HyPrism-linux-x64.AppImage"
  else
    echo "!! appimagetool not found; skipping AppImage"
  fi

  if command -v flatpak-builder >/dev/null 2>&1; then
    echo "==> Building Flatpak"
    FLATPAK_STAGE="$ARTIFACTS/linux-x64/flatpak-build"
    FLATPAK_REPO="$ARTIFACTS/flatpak-repo"
    cp "$LINUX_OUT/HyPrism" "$ROOT/packaging/flatpak/HyPrism"
    flatpak-builder --force-clean "$FLATPAK_STAGE" "$ROOT/packaging/flatpak/dev.hyprism.HyPrism.json" --repo="$FLATPAK_REPO"
  else
    echo "!! flatpak-builder not found; skipping Flatpak"
  fi
fi

# Optional: trigger remote Linux build (e.g., Parallels VM) after local steps
if [[ -n "$REMOTE_LINUX_HOST" && "$SKIP_REMOTE" != "1" ]]; then
  echo "==> Triggering remote build on $REMOTE_LINUX_HOST ($REMOTE_LINUX_PATH)"
  ssh "$REMOTE_LINUX_HOST" "cd '$REMOTE_LINUX_PATH' && AUTO_INSTALL=1 SKIP_REMOTE=1 ./scripts/build-all.sh" || {
    echo "!! Remote build failed on $REMOTE_LINUX_HOST" >&2
    exit 1
  }
fi

echo "Done. Artifacts in $ARTIFACTS"
