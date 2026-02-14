#!/bin/sh
# HyPrism Flatpak launcher wrapper
# - If a runnable release is present in $XDG_DATA_HOME/HyPrism -> run it
# - Otherwise: download the Linux release asset (latest, fallback to prerelease),
#   extract it into $XDG_DATA_HOME/HyPrism, and exec the launcher
# Logging: appended to $XDG_DATA_HOME/HyPrism/wrapper.log

set -u

APP_REPO_OWNER="yyyumeniku"
APP_REPO_NAME="HyPrism"
ASSET_BASENAME_PATTERN="HyPrism-linux-x64-.*\\.tar\\.xz"

log() {
  ts=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
  printf "%s %s\n" "$ts" "$*" | tee -a "$LOGFILE" 1>&2
}

# Determine data dir
if [ -n "${XDG_DATA_HOME:-}" ]; then
  DATA_ROOT="$XDG_DATA_HOME/HyPrism"
else
  DATA_ROOT="$HOME/.local/share/HyPrism"
fi

mkdir -p "$DATA_ROOT"
LOGFILE="$DATA_ROOT/wrapper.log"

# initialize logfile and emit environment diagnostics
: > "$LOGFILE" 2>/dev/null || true
exists="no"; [ -d "$DATA_ROOT" ] && exists="yes"
log "Wrapper PID $$ starting"
log "ENV: XDG_DATA_HOME='${XDG_DATA_HOME:-}' HOME='${HOME:-}' USER='${USER:-}'"
log "Using DATA_ROOT='$DATA_ROOT' (exists=$exists)"
log "Logging to $LOGFILE"

# Find an existing HyPrism executable under the data dir (best-effort)
find_local_binary() {
  log "Searching for local HyPrism binary under $DATA_ROOT"
  # check some common locations first
  if [ -x "$DATA_ROOT/HyPrism" ]; then
    log "Found local executable: $DATA_ROOT/HyPrism"
    printf '%s' "$DATA_ROOT/HyPrism"
    return 0
  fi
  if [ -x "$DATA_ROOT/hyprism/HyPrism" ]; then
    log "Found local executable: $DATA_ROOT/hyprism/HyPrism"
    printf '%s' "$DATA_ROOT/hyprism/HyPrism"
    return 0
  fi

  log "Searching deeper in $DATA_ROOT (maxdepth=3)"
  result=$(find "$DATA_ROOT" -maxdepth 3 -type f -name HyPrism -executable -print -quit 2>/dev/null || true)
  if [ -n "$result" ]; then
    log "Found local executable via find: $result"
    printf '%s' "$result"
    return 0
  fi

  log "No local HyPrism executable found"
  return 1
} 

# Download helper: prefer curl, then wget, then python
download_file() {
  url="$1"; out="$2"
  log "Downloading to $out (URL: $url)"
  if command -v curl >/dev/null 2>&1; then
    log "Using curl to download"
    if curl -fsL "$url" -o "$out"; then
      size=$(du -h "$out" | cut -f1 || true)
      log "Download succeeded (size: $size)"
      return 0
    else
      log "curl download failed"
      return 1
    fi
  fi
  if command -v wget >/dev/null 2>&1; then
    log "Using wget to download"
    if wget -qO "$out" "$url"; then
      size=$(du -h "$out" | cut -f1 || true)
      log "Download succeeded (size: $size)"
      return 0
    else
      log "wget download failed"
      return 1
    fi
  fi
  if command -v python3 >/dev/null 2>&1; then
    log "Using python3 urllib to download"
    if python3 -c "import sys,urllib.request; urllib.request.urlretrieve(sys.argv[1], sys.argv[2])" "$url" "$out"; then
      size=$(du -h "$out" | cut -f1 || true)
      log "Download succeeded (size: $size)"
      return 0
    else
      log "python3 download failed"
      return 1
    fi
  fi
  log "No downloader available (curl/wget/python3)"
  return 2
} 

# Ask GitHub API for the 'latest' release asset URL (tar.xz). Return empty on failure.
get_latest_asset_url() {
  url=""
  api_latest="https://api.github.com/repos/$APP_REPO_OWNER/$APP_REPO_NAME/releases/latest"
  log "Querying GitHub latest release API: $api_latest"
  json=$(curl -fsL "$api_latest" 2>/dev/null || printf '')
  len=$(printf '%s' "$json" | wc -c)
  log "GitHub latest response length: ${len} bytes"
  if [ -n "$json" ]; then
    url=$(printf '%s' "$json" | grep -Eo '"browser_download_url":\s*"[^\"]*'$ASSET_BASENAME_PATTERN'"' | sed -E 's/.*"([^\"]+)".*/\1/' | head -n1 || true)
    if [ -n "$url" ]; then
      log "Extracted latest asset URL: $url"
    else
      log "No matching asset URL found in latest release JSON"
    fi
  else
    log "No JSON returned for latest release"
  fi
  printf '%s' "$url"
} 

# Find the most recent prerelease that contains the desired asset
get_prerelease_asset_url() {
  api_list="https://api.github.com/repos/$APP_REPO_OWNER/$APP_REPO_NAME/releases?per_page=50"
  log "Querying GitHub releases list for prereleases: $api_list"
  list_json=$(curl -fsS "$api_list" 2>/dev/null || printf '')
  len=$(printf '%s' "$list_json" | wc -c)
  log "Releases list response length: ${len} bytes"
  if [ -z "$list_json" ]; then
    return 1
  fi

  # extract the first tag_name which has "prerelease": true
  tag=$(printf '%s' "$list_json" | awk -F '"' '
    /"tag_name"/ { tag=$4 }
    /"prerelease"/ { if ($0 ~ /true/ && tag != "") { print tag; exit } }
  ')

  if [ -z "$tag" ]; then
    return 1
  fi

  api_tag="https://api.github.com/repos/$APP_REPO_OWNER/$APP_REPO_NAME/releases/tags/$tag"
  json=$(curl -fsL "$api_tag" 2>/dev/null || printf '')
  if [ -n "$json" ]; then
    url=$(printf '%s' "$json" | grep -Eo '"browser_download_url":\s*"[^"]*'$ASSET_BASENAME_PATTERN'"' | sed -E 's/.*"([^"]+)".*/\1/' | head -n1 || true)
    if [ -n "$url" ]; then
      printf '%s' "$url"
      return 0
    fi
  fi

  return 1
}

# Determine if the filesystem containing DATA_ROOT is mounted with noexec
is_mount_noexec() {
  target="$DATA_ROOT"
  best=""
  best_len=0
  while IFS= read -r dev mnt rest; do
    case "$target" in
      "$mnt"*|"$mnt")
        l=$(printf '%s' "$mnt" | wc -c)
        if [ "$l" -gt "$best_len" ]; then best_len=$l; best="$mnt"; fi
        ;;
    esac
  done < /proc/self/mounts
  if [ -z "$best" ]; then
    log "Could not determine mount point for $DATA_ROOT"
    return 1
  fi
  opts=$(awk -v m="$best" '$2==m {print $4; exit}' /proc/self/mounts 2>/dev/null || true)
  log "Mount point for $DATA_ROOT is '$best' (opts: $opts)"
  case ",$opts," in
    *,noexec,*)
      log "Mount point has noexec set"
      return 0 ;;
    *)
      log "Mount point is executable"
      return 1 ;;
  esac
}

# Main flow
log "Wrapper start. Data dir: $DATA_ROOT"

# 1) If we already have a runnable binary, run it
existing=$(find_local_binary)
if [ -n "$existing" ]; then
  log "Found existing launcher: $existing — executing"
  exec "$existing" "$@"
fi

# 2) Try to fetch 'latest' release asset
log "No local launcher found — querying GitHub releases (latest -> prerelease)"
asset_url=$(get_latest_asset_url || true)
if [ -n "$asset_url" ]; then
  log "Found 'latest' asset: $asset_url"
else
  log "No suitable asset in 'latest' — searching prereleases"
  asset_url=$(get_prerelease_asset_url || true)
  if [ -n "$asset_url" ]; then
    log "Found prerelease asset: $asset_url"
  fi
fi

if [ -z "$asset_url" ]; then
  log "WARN: no suitable HyPrism linux asset found on GitHub (latest or prerelease)"
  # Fallback: if this Flatpak bundles a launcher, run it instead of failing outright.
  if [ -x "/app/lib/hyprism/HyPrism" ]; then
    log "FALLBACK: executing bundled launcher at /app/lib/hyprism/HyPrism"
    exec /app/lib/hyprism/HyPrism "$@"
  fi
  log "ERROR: no suitable HyPrism linux asset found and no bundled launcher available"
  exit 2
fi

# 3) Download asset
tmpfile=$(mktemp -t hyprism-asset-XXXXXX) || tmpfile="/tmp/hyprism-asset-$$"
trap 'rm -f "$tmpfile"' EXIT INT TERM
log "Downloading $asset_url"
if ! download_file "$asset_url" "$tmpfile"; then
  log "ERROR: download failed: $asset_url"
  exit 2
fi
log "Download complete: $tmpfile"

# 4) Extract into DATA_ROOT
case "$asset_url" in
  *.tar.xz) tar -xJf "$tmpfile" -C "$DATA_ROOT" || { log "ERROR: extract failed"; exit 3; } ;;
  *.tar.gz|*.tgz) tar -xzf "$tmpfile" -C "$DATA_ROOT" || { log "ERROR: extract failed"; exit 3; } ;;
  *.zip) unzip -q "$tmpfile" -d "$DATA_ROOT" || { log "ERROR: extract failed"; exit 3; } ;;
  *) log "ERROR: unknown archive format: $asset_url"; exit 3 ;;
esac
log "Extracted asset into $DATA_ROOT"
log "Listing top-level content of $DATA_ROOT (logged to $LOGFILE)"
ls -la "$DATA_ROOT" | head -n 50 >> "$LOGFILE" 2>&1 || true

# 5) Find the extracted launcher
installed=$(find_local_binary)
if [ -z "$installed" ]; then
  # try deeper search
  installed=$(find "$DATA_ROOT" -type f -name HyPrism -executable -print -quit 2>/dev/null || true)
fi
if [ -z "$installed" ]; then
  log "ERROR: could not locate extracted HyPrism binary under $DATA_ROOT"
  ls -la "$DATA_ROOT" >> "$LOGFILE" 2>&1 || true
  exit 4
fi

chmod a+x "$installed" 2>/dev/null || true
log "Launcher installed at $installed"

# 6) Check possible mount 'noexec' that would prevent execution
if is_mount_noexec; then
  log "ERROR: filesystem containing $DATA_ROOT is mounted with 'noexec' — cannot execute downloaded binary"
  log "Please install HyPrism via your distribution or use the native Flatpak update mechanism."
  exit 5
fi

# 7) Exec the binary
log "Executing $installed -- passing through arguments"
exec "$installed" "$@"
