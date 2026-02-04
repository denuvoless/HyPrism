#!/bin/bash
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  HyPrism macOS Build Script (Local)${NC}"
echo -e "${GREEN}========================================${NC}"

# Get the script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_ROOT"

# Detect architecture
ARCH=$(uname -m)
if [ "$ARCH" = "arm64" ]; then
    RID="osx-arm64"
    ARCH_NAME="arm64"
else
    RID="osx-x64"
    ARCH_NAME="x64"
fi

echo -e "${YELLOW}Building for macOS ${ARCH_NAME}...${NC}"

# Build frontend
echo -e "${YELLOW}Building frontend...${NC}"
cd frontend
npm ci
npm run build
cd ..

# Build .NET app
echo -e "${YELLOW}Building .NET application...${NC}"
dotnet publish ./HyPrism.csproj -c Release -r "$RID" --self-contained true -o "artifacts/$RID"

# Create app bundle
echo -e "${YELLOW}Creating macOS app bundle...${NC}"
APP_NAME="HyPrism.app"
APP_PATH="artifacts/$APP_NAME"

rm -rf "$APP_PATH"
mkdir -p "$APP_PATH/Contents/MacOS"
mkdir -p "$APP_PATH/Contents/Resources"

# Copy all files from publish output
cp -R "artifacts/$RID/"* "$APP_PATH/Contents/MacOS/"

# Copy resources
if [ -d "$APP_PATH/Contents/MacOS/wwwroot" ]; then
    cp -R "$APP_PATH/Contents/MacOS/wwwroot" "$APP_PATH/Contents/Resources/"
fi
if [ -f "$APP_PATH/Contents/MacOS/jre.json" ]; then
    cp "$APP_PATH/Contents/MacOS/jre.json" "$APP_PATH/Contents/Resources/"
fi
if [ -d "$APP_PATH/Contents/MacOS/assets" ]; then
    cp -R "$APP_PATH/Contents/MacOS/assets" "$APP_PATH/Contents/Resources/"
fi

# Copy Info.plist and icon
cp packaging/macos/Info.plist "$APP_PATH/Contents/"
cp packaging/macos/hyprism-macos.icns "$APP_PATH/Contents/Resources/"

# Set executable permission
chmod +x "$APP_PATH/Contents/MacOS/HyPrism"

# Create PkgInfo
echo "APPL????" > "$APP_PATH/Contents/PkgInfo"

# Sign the app locally (ad-hoc signing)
echo -e "${YELLOW}Signing app bundle (ad-hoc)...${NC}"
codesign --force --deep --sign - "$APP_PATH" || echo -e "${YELLOW}Warning: Codesign failed, continuing anyway${NC}"

# Create DMG
echo -e "${YELLOW}Creating DMG...${NC}"
DMG_NAME="HyPrism-macos-${ARCH_NAME}.dmg"
DMG_PATH="artifacts/$DMG_NAME"
DMG_STAGING="artifacts/dmg-staging"

rm -rf "$DMG_STAGING"
rm -f "$DMG_PATH"
rm -f "artifacts/HyPrism-rw.dmg"
mkdir -p "$DMG_STAGING"

# Copy app to staging
cp -R "$APP_PATH" "$DMG_STAGING/"

# Create Applications symlink
ln -s /Applications "$DMG_STAGING/Applications"

# Create the "Fix Permissions" command file
cat > "$DMG_STAGING/Fix Permissions.command" << 'EOF'
#!/bin/bash
# This script removes quarantine attributes from HyPrism
# Run this if macOS says the app is damaged or can't be opened

APP_PATH="$(dirname "$0")/HyPrism.app"

echo "================================"
echo "  HyPrism - Fix Permissions"
echo "================================"
echo ""
echo "Removing quarantine attributes..."
xattr -cr "$APP_PATH"

echo ""
echo "✓ Done! You can now open HyPrism.app"
echo ""
echo "Press any key to close..."
read -n 1 -s
EOF
chmod +x "$DMG_STAGING/Fix Permissions.command"

# Use specific background image
echo -e "${YELLOW}Setting up DMG background...${NC}"
SELECTED_BG="$PROJECT_ROOT/packaging/macos/dmg-background.jpg"
BG_EXT="jpg"

if [ -f "$SELECTED_BG" ]; then
    BG_FILENAME=$(basename "$SELECTED_BG")
    echo -e "${GREEN}Using background: $BG_FILENAME${NC}"
    
    # Create .background directory and copy the background
    mkdir -p "$DMG_STAGING/.background"
    cp "$SELECTED_BG" "$DMG_STAGING/.background/background.$BG_EXT"
else
    echo -e "${RED}Background image not found: $SELECTED_BG${NC}"
    SELECTED_BG=""
    BG_EXT=""
fi

# Create a temporary read-write DMG
echo -e "${YELLOW}Creating temporary DMG...${NC}"
hdiutil create -volname "HyPrism" -srcfolder "$DMG_STAGING" -ov -format UDRW -fs HFS+ "artifacts/HyPrism-rw.dmg"

# Mount the DMG
echo -e "${YELLOW}Mounting DMG for customization...${NC}"
MOUNT_POINT=$(hdiutil attach -readwrite -noverify -noautoopen "artifacts/HyPrism-rw.dmg" | grep "/Volumes/HyPrism" | sed 's/.*\(\/Volumes\/.*\)/\1/')

if [ -z "$MOUNT_POINT" ]; then
    echo -e "${RED}Failed to mount DMG${NC}"
    exit 1
fi

echo -e "${GREEN}Mounted at: $MOUNT_POINT${NC}"

# Give the system time to settle
sleep 2

# Set custom icon positions and window settings
echo -e "${YELLOW}Configuring DMG appearance...${NC}"

# Create AppleScript to set up the DMG
cat > /tmp/dmg_setup.applescript << APPLESCRIPT
tell application "Finder"
    tell disk "HyPrism"
        open
        set current view of container window to icon view
        set toolbar visible of container window to false
        set statusbar visible of container window to false
        set the bounds of container window to {400, 200, 1040, 680}
        set viewOptions to the icon view options of container window
        set arrangement of viewOptions to not arranged
        set icon size of viewOptions to 128
        set text size of viewOptions to 13
        set label position of viewOptions to bottom
        set shows icon preview of viewOptions to true
APPLESCRIPT

# Add background picture if available
if [ -n "$SELECTED_BG" ] && [ -f "$MOUNT_POINT/.background/background.$BG_EXT" ]; then
    cat >> /tmp/dmg_setup.applescript << APPLESCRIPT
        set background picture of viewOptions to file ".background:background.$BG_EXT"
        delay 1
APPLESCRIPT
fi

# Position items
cat >> /tmp/dmg_setup.applescript << APPLESCRIPT
        set position of item "HyPrism.app" of container window to {120, 220}
        set position of item "Applications" of container window to {500, 220}
        set position of item "Fix Permissions.command" of container window to {310, 400}
        close
        open
        update without registering applications
        delay 5
    end tell
end tell
APPLESCRIPT

# Run the AppleScript
osascript /tmp/dmg_setup.applescript

# Wait for changes to take effect
sleep 3

# Make sure .DS_Store is written
sync

# Unmount
echo -e "${YELLOW}Unmounting DMG...${NC}"
hdiutil detach "$MOUNT_POINT" -force || true
sleep 2

# Convert to compressed read-only DMG
echo -e "${YELLOW}Compressing DMG...${NC}"
hdiutil convert "artifacts/HyPrism-rw.dmg" -format UDZO -imagekey zlib-level=9 -o "$DMG_PATH"

# Clean up
rm -f "artifacts/HyPrism-rw.dmg"
rm -f /tmp/dmg_setup.applescript
rm -rf "$DMG_STAGING"

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  Build Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "App Bundle: ${YELLOW}$APP_PATH${NC}"
echo -e "DMG: ${YELLOW}$DMG_PATH${NC}"
echo ""
echo -e "${GREEN}To install:${NC}"
echo "1. Open the DMG file"
echo "2. Run 'Fix Permissions (Run First).command'"
echo "3. Drag HyPrism.app to Applications"
