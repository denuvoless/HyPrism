package app

import (
	"HyPrism/internal/env"
	"HyPrism/internal/java"
	"HyPrism/internal/pwr/butler"
	"fmt"
	"net"
	"net/http"
	"os"
	"path/filepath"
	"runtime"
	"time"
)

// DiagnosticReport contains system diagnostic information
type DiagnosticReport struct {
	Platform      PlatformInfo      `json:"platform"`
	Connectivity  ConnectivityInfo  `json:"connectivity"`
	GameStatus    GameStatusInfo    `json:"gameStatus"`
	Dependencies  DependenciesInfo  `json:"dependencies"`
	Timestamp     string            `json:"timestamp"`
}

type PlatformInfo struct {
	OS      string `json:"os"`
	Arch    string `json:"arch"`
	Version string `json:"version"`
}

type ConnectivityInfo struct {
	HytalePatches bool   `json:"hytalePatches"`
	GitHub        bool   `json:"github"`
	ItchIO        bool   `json:"itchIO"`
	Error         string `json:"error,omitempty"`
}

type GameStatusInfo struct {
	Installed       bool   `json:"installed"`
	Version         string `json:"version"`
	ClientExists    bool   `json:"clientExists"`
	OnlineFixApplied bool   `json:"onlineFixApplied"`
}

type DependenciesInfo struct {
	JavaInstalled   bool   `json:"javaInstalled"`
	JavaPath        string `json:"javaPath"`
	ButlerInstalled bool   `json:"butlerInstalled"`
	ButlerPath      string `json:"butlerPath"`
}

// RunDiagnostics runs system diagnostics
func (a *App) RunDiagnostics() DiagnosticReport {
	report := DiagnosticReport{
		Timestamp: time.Now().Format(time.RFC3339),
	}

	// Platform info
	report.Platform = PlatformInfo{
		OS:      runtime.GOOS,
		Arch:    runtime.GOARCH,
		Version: AppVersion,
	}

	// Connectivity checks
	report.Connectivity = checkConnectivity()

	// Game status
	report.GameStatus = checkGameStatus()

	// Dependencies
	report.Dependencies = checkDependencies()

	return report
}

func checkConnectivity() ConnectivityInfo {
	info := ConnectivityInfo{}
	client := &http.Client{
		Timeout: 10 * time.Second,
	}

	// Check Hytale patches
	resp, err := client.Head("https://game-patches.hytale.com")
	if err == nil && resp.StatusCode < 500 {
		info.HytalePatches = true
	}

	// Check GitHub
	resp, err = client.Head("https://api.github.com")
	if err == nil && resp.StatusCode < 500 {
		info.GitHub = true
	}

	// Check itch.io (Butler)
	resp, err = client.Head("https://broth.itch.zone")
	if err == nil && resp.StatusCode < 500 {
		info.ItchIO = true
	}

	// DNS check
	_, err = net.LookupHost("game-patches.hytale.com")
	if err != nil {
		info.Error = "DNS resolution failed: " + err.Error()
	}

	return info
}

func checkGameStatus() GameStatusInfo {
	info := GameStatusInfo{}

	// Check release-latest instance (version 0)
	gameDir := env.GetInstanceGameDir("release", 0)

	// Check if game is installed
	clientName := "HytaleClient"
	if runtime.GOOS == "windows" {
		clientName += ".exe"
	}

	clientPath := filepath.Join(gameDir, "Client", clientName)
	if _, err := os.Stat(clientPath); err == nil {
		info.Installed = true
		info.ClientExists = true
	}

	// Check if any release instance is installed
	if env.IsVersionInstalled("release", 0) {
		info.Version = "release-latest"
	}

	// Check if online fix is applied (Windows only)
	if runtime.GOOS == "windows" {
		serverBat := filepath.Join(gameDir, "Server", "start-server.bat")
		if _, err := os.Stat(serverBat); err == nil {
			info.OnlineFixApplied = true
		}
	} else {
		info.OnlineFixApplied = true // Not needed on other platforms
	}

	return info
}

func checkDependencies() DependenciesInfo {
	info := DependenciesInfo{}

	// Check Java
	javaPath, err := java.GetJavaExec()
	if err == nil {
		info.JavaInstalled = true
		info.JavaPath = javaPath
	}

	// Check Butler
	butlerPath, err := butler.GetButlerPath()
	if err == nil {
		if _, err := os.Stat(butlerPath); err == nil {
			info.ButlerInstalled = true
			info.ButlerPath = butlerPath
		}
	}

	return info
}

// SaveDiagnosticReport saves diagnostics to a file
func (a *App) SaveDiagnosticReport() (string, error) {
	report := a.RunDiagnostics()
	
	logsDir := filepath.Join(env.GetDefaultAppDir(), "logs")
	if err := os.MkdirAll(logsDir, 0755); err != nil {
		return "", err
	}

	filename := fmt.Sprintf("diagnostic_%s.txt", time.Now().Format("2006-01-02_15-04-05"))
	filepath := filepath.Join(logsDir, filename)

	content := fmt.Sprintf(`HyPrism Diagnostic Report
Generated: %s

=== PLATFORM ===
OS: %s
Arch: %s
Launcher Version: %s

=== CONNECTIVITY ===
Hytale Patches Server: %v
GitHub API: %v
itch.io (Butler): %v
Error: %s

=== GAME STATUS ===
Installed: %v
Version: %s
Client Exists: %v
Online Fix Applied: %v

=== DEPENDENCIES ===
Java Installed: %v
Java Path: %s
Butler Installed: %v
Butler Path: %s
`,
		report.Timestamp,
		report.Platform.OS, report.Platform.Arch, report.Platform.Version,
		report.Connectivity.HytalePatches, report.Connectivity.GitHub, report.Connectivity.ItchIO, report.Connectivity.Error,
		report.GameStatus.Installed, report.GameStatus.Version, report.GameStatus.ClientExists, report.GameStatus.OnlineFixApplied,
		report.Dependencies.JavaInstalled, report.Dependencies.JavaPath, report.Dependencies.ButlerInstalled, report.Dependencies.ButlerPath,
	)

	if err := os.WriteFile(filepath, []byte(content), 0644); err != nil {
		return "", err
	}

	return filepath, nil
}

// CrashReport represents a crash report
type CrashReport struct {
	Filename  string `json:"filename"`
	Timestamp string `json:"timestamp"`
	Preview   string `json:"preview"`
}

// GetCrashReports returns available crash reports
func (a *App) GetCrashReports() ([]CrashReport, error) {
	crashDir := filepath.Join(env.GetDefaultAppDir(), "crashes")
	
	entries, err := os.ReadDir(crashDir)
	if err != nil {
		if os.IsNotExist(err) {
			return []CrashReport{}, nil
		}
		return nil, err
	}

	var reports []CrashReport
	for _, entry := range entries {
		if entry.IsDir() {
			continue
		}

		info, err := entry.Info()
		if err != nil {
			continue
		}

		report := CrashReport{
			Filename:  entry.Name(),
			Timestamp: info.ModTime().Format(time.RFC3339),
		}

		// Read first 500 bytes as preview
		content, err := os.ReadFile(filepath.Join(crashDir, entry.Name()))
		if err == nil {
			if len(content) > 500 {
				report.Preview = string(content[:500]) + "..."
			} else {
				report.Preview = string(content)
			}
		}

		reports = append(reports, report)
	}

	return reports, nil
}
