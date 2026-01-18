package env

import (
	"fmt"
	"os"
	"path/filepath"
)

// CleanupIncompleteDownloads removes incomplete download files
func CleanupIncompleteDownloads() error {
	appDir := GetDefaultAppDir()
	
	// Extensions to clean
	extensions := []string{".tmp", ".partial", ".downloading"}

	// Clean cache directory
	if err := cleanDirectory(GetCacheDir(), extensions); err != nil {
		return err
	}

	// Clean JRE directory
	if err := cleanDirectory(GetJREDir(), extensions); err != nil {
		return err
	}

	// Clean Butler directory
	if err := cleanDirectory(GetButlerDir(), extensions); err != nil {
		return err
	}

	// Check for incomplete game in release-latest instance
	gameDir := GetInstanceGameDir("release", 0)
	if err := cleanIncompleteGame(gameDir); err != nil {
		return err
	}

	fmt.Printf("Cleanup completed in %s\n", appDir)
	return nil
}

func cleanDirectory(dir string, extensions []string) error {
	entries, err := os.ReadDir(dir)
	if err != nil {
		if os.IsNotExist(err) {
			return nil
		}
		return err
	}

	for _, entry := range entries {
		if entry.IsDir() {
			continue
		}

		for _, ext := range extensions {
			if filepath.Ext(entry.Name()) == ext {
				filePath := filepath.Join(dir, entry.Name())
				fmt.Printf("Removing incomplete file: %s\n", filePath)
				os.Remove(filePath)
				break
			}
		}
	}

	return nil
}

func cleanIncompleteGame(gameDir string) error {
	markerFile := filepath.Join(gameDir, ".installing")
	
	if _, err := os.Stat(markerFile); err == nil {
		// Installation was incomplete, clean up
		fmt.Println("Found incomplete game installation, cleaning up...")
		
		// Remove the Client folder if it exists
		clientDir := filepath.Join(gameDir, "Client")
		os.RemoveAll(clientDir)
		
		// Remove marker
		os.Remove(markerFile)
	}

	return nil
}
