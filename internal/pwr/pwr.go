package pwr

import (
	"HyPrism/internal/util"
	"HyPrism/internal/pwr/butler"
	"context"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"time"
)

// cleanStagingDirectory removes staging directory and any leftover temp files
// This fixes "Access Denied" errors on Windows where previous installations left locked files
func cleanStagingDirectory(gameDir string) error {
	stagingDir := filepath.Join(gameDir, "staging-temp")
	
	// Remove staging directory completely
	if err := os.RemoveAll(stagingDir); err != nil {
		// On Windows, try to remove files individually if directory removal fails
		if runtime.GOOS == "windows" {
			filepath.Walk(stagingDir, func(path string, info os.FileInfo, err error) error {
				if err == nil && !info.IsDir() {
					os.Remove(path)
				}
				return nil
			})
			// Try again after individual file removal
			os.RemoveAll(stagingDir)
		}
	}
	
	// Also clean any .tmp files in game directory that butler might have left
	entries, _ := os.ReadDir(gameDir)
	for _, entry := range entries {
		name := entry.Name()
		if strings.HasSuffix(name, ".tmp") || strings.HasPrefix(name, "sf-") {
			os.Remove(filepath.Join(gameDir, name))
		}
	}
	
	return nil
}

// ApplyPWR is deprecated - use ApplyPWRToDir with specific instance path
// This legacy function applied patches to release/package/game/latest which is no longer used
func ApplyPWR(ctx context.Context, pwrFile string, progressCallback func(stage string, progress float64, message string, currentFile string, speed string, downloaded, total int64)) error {
	return fmt.Errorf("ApplyPWR is deprecated - use ApplyPWRToDir with instance path")
}

// ApplyPWRToDir applies a PWR patch file to a specific directory
func ApplyPWRToDir(ctx context.Context, pwrFile string, targetDir string, progressCallback func(stage string, progress float64, message string, currentFile string, speed string, downloaded, total int64)) error {
	stagingDir := filepath.Join(targetDir, "staging-temp")
	
	// Get Butler path
	butlerPath, err := butler.GetButlerPath()
	if err != nil {
		return fmt.Errorf("butler not found: %w", err)
	}
	
	// Clean staging directory
	if progressCallback != nil {
		progressCallback("install", 0, "Preparing installation...", "", "", 0, 0)
	}
	cleanStagingDirectory(targetDir)
	
	// Create directories
	if err := os.MkdirAll(targetDir, 0755); err != nil {
		return fmt.Errorf("failed to create target directory: %w", err)
	}
	if err := os.MkdirAll(stagingDir, 0755); err != nil {
		return fmt.Errorf("failed to create staging directory: %w", err)
	}

	if progressCallback != nil {
		progressCallback("install", 5, "Installing game...", "", "", 0, 0)
	}

	fmt.Printf("Applying PWR patch with Butler: %s -> %s\n", pwrFile, targetDir)
	
	var cmd *exec.Cmd
	if runtime.GOOS == "windows" {
		cmd = exec.CommandContext(ctx, butlerPath, "apply", "--staging-dir", stagingDir, "--save-interval=60", pwrFile, targetDir)
	} else {
		cmd = exec.CommandContext(ctx, butlerPath, "apply", "--staging-dir", stagingDir, pwrFile, targetDir)
	}
	util.HideConsoleWindow(cmd)
	
	output, err := cmd.CombinedOutput()
	if err != nil {
		fmt.Printf("Butler error output: %s\n", string(output))
		cleanStagingDirectory(targetDir)
		return fmt.Errorf("butler apply failed: %w\nOutput: %s", err, string(output))
	}

	fmt.Printf("Butler output: %s\n", string(output))

	// Clean up staging directory
	cleanStagingDirectory(targetDir)

	// Clean up patch file
	go func() {
		time.Sleep(2 * time.Second)
		os.Remove(pwrFile)
	}()

	if progressCallback != nil {
		progressCallback("install", 100, "Installation complete", "", "", 0, 0)
	}

	// Set executable permissions on Unix
	if runtime.GOOS != "windows" {
		var clientPath string
		switch runtime.GOOS {
		case "darwin":
			clientPath = filepath.Join(targetDir, "Client", "Hytale.app", "Contents", "MacOS", "HytaleClient")
		default:
			clientPath = filepath.Join(targetDir, "Client", "HytaleClient")
		}
		os.Chmod(clientPath, 0755)
	}

	fmt.Println("Installation to directory complete")
	return nil
}
