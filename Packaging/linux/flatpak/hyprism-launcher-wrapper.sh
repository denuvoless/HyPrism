#!/bin/sh
# Bundled copy of hyprism-launcher-wrapper.sh for flatpak bundle packaging
#!/bin/sh
# HyPrism Flatpak launcher wrapper —
# - if a user‑installed copy exists in $XDG_DATA_HOME/HyPrism, run it
# - otherwise download the Linux release (latest → prerelease), extract to app data dir and run
# - fall back to bundled /app/lib/hyprism/HyPrism if anything fails

set -eu

DATA_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/HyPrism"
LOG="$DATA_DIR/wrapper.log"
mkdir -p "$DATA_DIR"

log() { printf "%s %s\n" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >> "$LOG"; }

# If a user-installed launcher exists, run it
if [ -x "$DATA_DIR/HyPrism" ]; then
  log "Found user release at $DATA_DIR/HyPrism — exec"
  exec "$DATA_DIR/HyPrism" "$@"
fi

# Determine asset name by architecture
case "$(uname -m)" in
  x86_64|amd64) ASSET_RE='HyPrism-linux-x86_64.*\\.tar\\.xz' ;;
  aarch64|arm64) ASSET_RE='HyPrism-linux-arm64.*\\.tar\\.xz' ;;
  *) ASSET_RE='HyPrism-linux-x86_64.*\\.tar\\.xz' ;;
esac

# Helper: get browser_download_url for matching asset from GitHub API JSON
get_asset_url() {
  local json="$1" asset
  asset=$(printf "%s" "$json" | grep -E '"browser_download_url"' | sed -E 's/.*"browser_download_url" *: *"([^"]+)".*/\1/' | grep -E "$ASSET_RE" | head -n1 || true)
  printf "%s" "$asset"
}

# Downloader (curl/wget)
download_file() {
  local url="$1" out="$2"
  if command -v curl >/dev/null 2>&1; then
    curl -L --fail --silent --show-error -o "$out" "$url"
    return $?
  elif command -v wget >/dev/null 2>&1; then
    wget -qO "$out" "$url"
    return $?
  else
    return 2
  fi
}

# Try GitHub API: latest → prereleases
REPO="yyyumeniku/HyPrism"
log "Looking for release asset matching: $ASSET_RE"
asset_url=""

# try latest
if command -v curl >/dev/null 2>&1; then
  json=$(curl -sSf "https://api.github.com/repos/$REPO/releases/latest" 2>/dev/null || true)
else
  json=""
fi
if [ -n "$json" ]; then
  asset_url=$(get_asset_url "$json")
fi

# fallback: search releases for first prerelease with matching asset
if [ -z "$asset_url" ]; then
  if command -v curl >/dev/null 2>&1; then
    json_all=$(curl -sSf "https://api.github.com/repos/$REPO/releases" 2>/dev/null || true)
    if [ -n "$json_all" ]; then
      # prefer non-draft releases; pick first release with matching asset
      asset_url=$(printf "%s" "$json_all" | get_asset_url -)
    fi
  fi
fi

if [ -z "$asset_url" ]; then
  log "No suitable GitHub release asset found; falling back to bundled launcher"
  if [ -x "/app/lib/hyprism/HyPrism" ]; then
    exec /app/lib/hyprism/HyPrism "$@"
  fi
  log "Bundled launcher missing — exiting"
  echo "No launcher available" >&2
  exit 1
fi

log "Downloading asset: $asset_url"
TMP_TAR="$DATA_DIR/hyprism-release.tar.xz"
rm -f "$TMP_TAR"
if ! download_file "$asset_url" "$TMP_TAR"; then
  log "Download failed: $asset_url — falling back to bundled launcher"
  if [ -x "/app/lib/hyprism/HyPrism" ]; then
    exec /app/lib/hyprism/HyPrism "$@"
  fi
  exit 1
fi

log "Extracting release to $DATA_DIR"
# Extract into a temporary dir then move files
TMP_DIR="$DATA_DIR/.extract.$$"
rm -rf "$TMP_DIR" && mkdir -p "$TMP_DIR"
if tar -xJf "$TMP_TAR" -C "$TMP_DIR" 2>>"$LOG"; then
  # find top-level dir containing HyPrism binary
  found_bin=$(find "$TMP_DIR" -type f -name HyPrism -perm /111 | head -n1 || true)
  if [ -n "$found_bin" ]; then
    rm -rf "$DATA_DIR"/* || true
    mkdir -p "$DATA_DIR"
    # copy extracted tree into DATA_DIR preserving structure
    cp -a "$TMP_DIR"/* "$DATA_DIR/" 2>>"$LOG" || true
    chmod +x "$DATA_DIR/HyPrism" 2>>"$LOG" || true
    rm -rf "$TMP_DIR" "$TMP_TAR"
    log "Extraction complete — exec $DATA_DIR/HyPrism"
    exec "$DATA_DIR/HyPrism" "$@"
  else
    log "No HyPrism binary found inside archive — falling back"
    rm -rf "$TMP_DIR" "$TMP_TAR"
    if [ -x "/app/lib/hyprism/HyPrism" ]; then
      exec /app/lib/hyprism/HyPrism "$@"
    fi
    exit 1
  fi
else
  log "Extraction failed" 
  rm -rf "$TMP_DIR" "$TMP_TAR"
  if [ -x "/app/lib/hyprism/HyPrism" ]; then
    exec /app/lib/hyprism/HyPrism "$@"
  fi
  exit 1
fi

# Minimal shim that execs the wrapper shipped in /app/lib/hyprism if present,
# otherwise execs the system wrapper (this file is kept to make bundle builds
# include the wrapper script). The real logic is in Packaging/flatpak/hyprism-launcher-wrapper.sh

if [ -x "/app/lib/hyprism/hyprism-launcher-wrapper.sh" ]; then
  exec /app/lib/hyprism/hyprism-launcher-wrapper.sh "$@"
fi

# Fallback to bundled binary
if [ -x "/app/lib/hyprism/HyPrism" ]; then
  exec /app/lib/hyprism/HyPrism "$@"
fi

# Last-resort: fail with message
echo "HyPrism launcher not available inside bundle" >&2
exit 1
