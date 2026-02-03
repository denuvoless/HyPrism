using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using HyPrism.Services.Core;
using HyPrism.Services.Game;
using HyPrism.Services.User;
using HyPrism.Models;

namespace HyPrism.Services;

public class AppService : IDisposable
{
    private readonly string _configPath;
    private readonly string _appDir;
    private Config _config;
    private readonly ButlerService _butlerService;
    private readonly DiscordService _discordService;
    private bool _disposed;
    
    // Wrapper for GameProcess state via GameProcessService
    private Process? _gameProcess 
    {
        get => _gameProcessService?.GetGameProcess();
        set => _gameProcessService?.SetGameProcess(value);
    }
    
    // New services
    private readonly ConfigService _configService;
    private readonly ProfileService _profileService;
    private readonly NewsService _newsService;
    private readonly VersionService _versionService;
    private readonly DownloadService _downloadService;
    private readonly ModService _modService;
    private readonly LaunchService _launchService;
    private readonly GameProcessService _gameProcessService;
    private readonly FileService _fileService;
    private readonly UpdateService _updateService;
    private readonly SkinService _skinService;
    private readonly InstanceService _instanceService;
    private readonly UserIdentityService _userIdentityService;
    private readonly ProfileManagementService _profileManagementService;
    private readonly SettingsService _settingsService;
    private readonly FileDialogService _fileDialogService;
    private readonly LanguageService _languageService;
    private readonly AssetService _assetService;
    private readonly AvatarService _avatarService;
    private readonly RosettaService _rosettaService;
    private readonly BrowserService _browserService;
    private readonly ProgressNotificationService _progressNotificationService;
    private readonly GameSessionService _gameSessionService;
    
    // Exposed for ViewModel access
    public Config Configuration => _config;
    public ProfileService ProfileService => _profileService;
    public NewsService NewsService => _newsService;
    public VersionService VersionService => _versionService;
    public ModService ModService => _modService;
    public LocalizationService Localization => LocalizationService.Instance;

    // UI Events (forwarded from ProgressNotificationService)
    public event Action<string, double, string, long, long>? DownloadProgressChanged
    {
        add => _progressNotificationService.DownloadProgressChanged += value;
        remove => _progressNotificationService.DownloadProgressChanged -= value;
    }
    public event Action<string, int>? GameStateChanged
    {
        add => _progressNotificationService.GameStateChanged += value;
        remove => _progressNotificationService.GameStateChanged -= value;
    }
    public event Action<string, string, string?>? ErrorOccurred
    {
        add => _progressNotificationService.ErrorOccurred += value;
        remove => _progressNotificationService.ErrorOccurred -= value;
    }
    public event Action<object>? LauncherUpdateAvailable;
    
    // Lock for mod manifest operations to prevent concurrent writes
    private static readonly SemaphoreSlim _modManifestLock = new(1, 1);
    
    // Replaced by instance field _httpClient in constructor
    // private static readonly HttpClient HttpClient...
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Private field replaced by injected one, logic moved to constructor
    private readonly HttpClient _httpClient; 

    static AppService()
    {
        UtilityService.LoadEnvFile();
    }

    // --- Legacy Construction Support ---
    private readonly struct LegacyDeps
    {
        public readonly ConfigService Config;
        public readonly HttpClient Client;
        public readonly ProfileService Profile;
        public readonly DownloadService Download;
        public readonly VersionService Version;
        public readonly NewsService News;
        
        // New Services
        public readonly ModService Mod;
        public readonly LaunchService Launch;
        public readonly InstanceService Instance;
        public readonly BrowserService Browser;
        public readonly DiscordService Discord;
        public readonly RosettaService Rosetta;
        public readonly FileDialogService FileDialog;
        public readonly ButlerService Butler;
        public readonly SettingsService Settings;
        public readonly AssetService Asset;
        public readonly AvatarService Avatar;
        public readonly SkinService Skin;
        public readonly UserIdentityService UserIdentity;
        public readonly ProfileManagementService ProfileMan;
        public readonly LanguageService Language;
        public readonly GameProcessService GameProcess;
        public readonly FileService File;
        public readonly UpdateService Update;
        public readonly ProgressNotificationService Progress;
        public readonly GameSessionService GameSession;

        public LegacyDeps(
            ConfigService cfg, HttpClient cli, ProfileService prof, 
            DownloadService dl, VersionService ver, NewsService news,
            ModService mod, LaunchService launch, InstanceService inst,
            BrowserService browser, DiscordService discord, RosettaService rosetta,
            FileDialogService fileDialog, ButlerService butler, SettingsService settings,
            AssetService asset, AvatarService avatar, SkinService skin, UserIdentityService uid,
            ProfileManagementService pm, LanguageService lang, GameProcessService gameProc, FileService file,
            UpdateService update, ProgressNotificationService progress, GameSessionService gameSession)
        {
            Config = cfg; Client = cli; Profile = prof; Download = dl; Version = ver; News = news;
            Mod = mod; Launch = launch; Instance = inst;
            Browser = browser; Discord = discord; Rosetta = rosetta; FileDialog = fileDialog; Butler = butler;
            Settings = settings;
            Asset = asset;
            Avatar = avatar;
            Skin = skin;
            UserIdentity = uid;
            ProfileMan = pm;
            Language = lang;
            GameProcess = gameProc;
            File = file;
            Update = update;
            Progress = progress;
            GameSession = gameSession;
        }
    }

    private static LegacyDeps BuildLegacy()
    {
        var appDir = UtilityService.GetEffectiveAppDir();
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.Add("User-Agent", "HyPrism/1.0");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var config = new ConfigService(appDir);
        var instance = new InstanceService(appDir, config);
        var appPath = new AppPathConfiguration(appDir);
        var skin = new SkinService(appPath, config, instance);
        var uid = new UserIdentityService(config, skin, instance);
        var pm = new ProfileManagementService(appPath, config, skin, instance, uid);
        var ver = new VersionService(appDir, client, config);
        var lang = new LanguageService(ver, instance);
        var file = new FileService(appPath);
        var gameProc = new GameProcessService();
        var browser = new BrowserService();
        var update = new UpdateService(client, config, ver, instance, browser);
        var discord = new DiscordService();
        var progress = new ProgressNotificationService(discord);
        var launch = new LaunchService(appDir, client);
        var mod = new ModService(client, appDir);
        // ... (truncated for brevity in thought process)
        var download = new DownloadService(client);
        var butler = new ButlerService(appDir);
        var settings = new SettingsService(config);
        
        var gameSession = new GameSessionService(
            config, instance, ver, update, launch, butler, download, mod, skin, uid, gameProc, progress, discord, client, appPath
        );
        
        // Manual wiring for legacy
        return new LegacyDeps(
            config,
            client,
            new ProfileService(appDir, config),
            download,
            ver,
            new NewsService(client),
            mod,
            launch,
            instance,
            browser,
            discord,
            new RosettaService(),
            new FileDialogService(),
            butler,
            settings,
            new AssetService(instance, appDir),
            new AvatarService(instance, appDir),
            skin,
            uid,
            pm,
            lang,
            gameProc,
            file,
            update,
            progress,
            gameSession
        );
    }

    // Legacy Constructor
    public AppService() : this(BuildLegacy()) { }

    // Private helper constructor to unpack struct
    private AppService(LegacyDeps d) : this(
        d.Config, d.Client, d.Profile, d.Download, d.Version, d.News,
        d.Mod, d.Launch, d.Instance, d.Browser, d.Discord, d.Rosetta, d.FileDialog, d.Butler, d.Settings,
        d.Asset, d.Avatar, d.Skin, d.UserIdentity, d.ProfileMan, d.Language, d.GameProcess, d.File, d.Update, d.Progress, d.GameSession) { }

    // --- Main DI Constructor ---
    public AppService(
        ConfigService configService, 
        HttpClient httpClient,
        ProfileService profileService,
        DownloadService downloadService,
        VersionService versionService,
        NewsService newsService,
        // New Injections
        ModService modService,
        LaunchService launchService,
        InstanceService instanceService,
        BrowserService browserService,
        DiscordService discordService,
        RosettaService rosettaService,
        FileDialogService fileDialogService,
        ButlerService butlerService,
        SettingsService settingsService,
        AssetService assetService,
        AvatarService avatarService,
        SkinService skinService,
        UserIdentityService userIdentityService,
        ProfileManagementService profileManagementService,
        LanguageService languageService,
        GameProcessService gameProcessService,
        FileService fileService,
        UpdateService updateService,
        ProgressNotificationService progressNotificationService,
        GameSessionService gameSessionService)
    {
        _httpClient = httpClient;
        _appDir = UtilityService.GetEffectiveAppDir();
        Directory.CreateDirectory(_appDir);
        _configPath = Path.Combine(_appDir, "config.json");
        
        // --- Core Services (Injected) ---
        _configService = configService;
        _config = _configService.Configuration;
        
        // --- Phase 2 Injected Services ---
        _profileService = profileService;
        _downloadService = downloadService;
        _versionService = versionService;
        _newsService = newsService;
        
        // --- Phase 3 Injected Services ---
        _modService = modService;
        _launchService = launchService;
        _instanceService = instanceService;
        _browserService = browserService;
        _discordService = discordService;
        _rosettaService = rosettaService;
        _fileDialogService = fileDialogService;
        _butlerService = butlerService;
        _settingsService = settingsService;
        _assetService = assetService;
        _avatarService = avatarService;
        _skinService = skinService;
        _userIdentityService = userIdentityService;
        _profileManagementService = profileManagementService;
        _languageService = languageService;
        _gameProcessService = gameProcessService;
        _fileService = fileService;
        _updateService = updateService;
        _progressNotificationService = progressNotificationService;
        _gameSessionService = gameSessionService;
        
        // Subscribe to events
        _updateService.LauncherUpdateAvailable += (obj) => LauncherUpdateAvailable?.Invoke(obj);
        
        // Update placeholder names to random ones immediately
        if (_config.Nick == "Hyprism" || _config.Nick == "HyPrism" || _config.Nick == "Player")
        {
            _config.Nick = UtilityService.GenerateRandomUsername();
            SaveConfig();
            Logger.Info("Config", $"Updated placeholder username to: {_config.Nick}");
        }
        
        // IMPORTANT: Attempt to recover orphaned skin data after config is loaded.
        // This handles the case where config was reset but old skin files still exist.
        _skinService.TryRecoverOrphanedSkinOnStartup();
        
        _instanceService.MigrateLegacyData();
        _discordService.Initialize();
        
        // Initialize profile mods symlink if an active profile exists
        _profileManagementService.InitializeProfileModsSymlink();
    }

    /// <summary>
    /// Gets the effective app directory, checking for environment variable override first.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _skinService?.StopSkinProtection();
        _discordService?.Dispose();
    }
    
    /// <summary>
    /// Gets the UserData path for an instance. The game stores skins, settings, etc. here.
    /// </summary>
    private int ResolveVersionOrLatest(string branch, int version)
    {
        if (version > 0) return version;
        if (_config.SelectedVersion > 0) return _config.SelectedVersion;

        var info = _instanceService.LoadLatestInfo(branch);
        if (info?.Version > 0) return info.Version;

        string resolvedBranch = string.IsNullOrWhiteSpace(branch) ? _config.VersionType : branch;
        string branchDir = _instanceService.GetBranchPath(resolvedBranch);
        if (Directory.Exists(branchDir))
        {
            var latest = Directory.GetDirectories(branchDir)
                .Select(Path.GetFileName)
                .Select(name => int.TryParse(name, out var v) ? v : -1)
                .Where(v => v > 0)
                .OrderByDescending(v => v)
                .FirstOrDefault();
            return latest;
        }

        return 0;
    }

    private string GetLatestInstancePath(string branch) => _instanceService.GetLatestInstancePath(branch);

    private string GetInstancePath(string branch, int version) => _instanceService.GetInstancePath(branch, version);

    private string ResolveInstancePath(string branch, int version, bool preferExisting) => _instanceService.ResolveInstancePath(branch, version, preferExisting);

    private async Task<(string branch, int version)> ResolveLatestCompositeAsync()
    {
        var releaseVersions = await GetVersionListAsync("release");
        var preVersions = await GetVersionListAsync("pre-release");
        int releaseLatest = releaseVersions.FirstOrDefault();
        int preLatest = preVersions.FirstOrDefault();

        // If both missing, default to release 0
        if (releaseLatest == 0 && preLatest == 0)
        {
            return ("release", 0);
        }

        // Prefer whichever has the higher version number; tie goes to pre-release
        if (preLatest >= releaseLatest)
        {
            return ("pre-release", preLatest);
        }

        return ("release", releaseLatest);
    }

    private void SaveConfigInternal(Config config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }

    public void SaveConfig()
    {
        SaveConfigInternal(_config);
    }
    
    public void SetLanguage(string languageCode) => _settingsService.SetLanguage(languageCode);
    
    /// <summary>
    /// Attempts to recover orphaned skin data on startup.
    public Config QueryConfig() => _config;

    public string GetNick() => _profileService.GetNick();
    
    public string GetUUID() => _profileService.GetUUID();
    
    /// <summary>
    /// Gets the avatar preview image as base64 data URL for displaying in the launcher.
    /// Returns null if no avatar preview exists.
    /// </summary>
    public string? GetAvatarPreview() => _profileService.GetAvatarPreview();
    
    /// <summary>
    /// Gets the avatar preview for a specific UUID.
    /// Checks profile folder first, then game cache, then persistent backup.
    /// </summary>
    public string? GetAvatarPreviewForUUID(string uuid) => _profileService.GetAvatarPreviewForUUID(uuid);

    public string GetCustomInstanceDir() => _config.InstanceDirectory ?? "";

    public bool SetUUID(string uuid) => _profileService.SetUUID(uuid);
    
    /// <summary>
    /// Clears the avatar cache for the current UUID.
    /// Call this when the user wants to reset their avatar.
    /// </summary>
    public bool ClearAvatarCache()
    {
        var uuid = GetCurrentUuid();
        return _avatarService.ClearAvatarCache(uuid);
    }
    
    public bool SetNick(string nick) => _profileService.SetNick(nick);
    
    // ========== UUID Management (Username->UUID Mapping) ==========
    
    /// <summary>
    /// Gets or creates a UUID for a specific username.
    /// Uses case-insensitive lookup but preserves original username casing.
    /// This ensures each username consistently gets the same UUID across sessions.
    /// </summary>
    public string GetUuidForUser(string username) => _userIdentityService.GetUuidForUser(username);
    
    /// <summary>
    /// Gets the UUID for the current user (based on Nick).
    /// </summary>
    public string GetCurrentUuid() => _userIdentityService.GetCurrentUuid();
    
    /// <summary>
    /// Gets all username->UUID mappings.
    /// Returns a list of objects with username, uuid, and isCurrent properties.
    /// </summary>
    public List<UuidMapping> GetAllUuidMappings() => _userIdentityService.GetAllUuidMappings();
    
    /// <summary>
    /// Sets a custom UUID for a specific username.
    /// </summary>
    public bool SetUuidForUser(string username, string uuid) => _userIdentityService.SetUuidForUser(username, uuid);
    
    /// <summary>
    /// Deletes the UUID mapping for a specific username.
    /// Cannot delete the UUID for the current user.
    /// </summary>
    public bool DeleteUuidForUser(string username) => _userIdentityService.DeleteUuidForUser(username);
    
    /// <summary>
    /// Generates a new random UUID for the current user.
    /// Warning: This will change the player's identity and they will lose their skin!
    /// </summary>
    public string ResetCurrentUserUuid() => _userIdentityService.ResetCurrentUserUuid();
    
    /// <summary>
    /// Switches to an existing username (and its UUID).
    /// Returns the UUID for the username.
    /// </summary>
    public string? SwitchToUsername(string username) => _userIdentityService.SwitchToUsername(username);
    
    /// <summary>
    /// Attempts to recover orphaned skin data and associate it with the current user.
    /// This is useful when a user's config was reset but their skin data still exists.
    /// Returns true if skin data was recovered, false otherwise.
    /// </summary>
    public bool RecoverOrphanedSkinData() => _userIdentityService.RecoverOrphanedSkinData();
    
    /// <summary>
    /// Gets the UUID of any orphaned skin found in the game cache.
    /// Returns null if no orphaned skins are found.
    /// </summary>
    public string? GetOrphanedSkinUuid() => _userIdentityService.GetOrphanedSkinUuid();
    
    // ========== Profile Management ==========
    
    /// <summary>
    /// Gets all saved profiles, filtering out any with null/empty names.
    /// </summary>
    public List<Profile> GetProfiles() => _profileManagementService.GetProfiles();
    
    /// <summary>
    /// Gets the currently active profile index. -1 means no profile selected.
    /// </summary>
    public int GetActiveProfileIndex() => _profileManagementService.GetActiveProfileIndex();
    
    /// <summary>
    /// Creates a new profile with the given name and UUID.
    /// Returns the created profile.
    /// </summary>
    public Profile? CreateProfile(string name, string uuid) => _profileManagementService.CreateProfile(name, uuid);
    
    /// <summary>
    /// Deletes a profile by its ID.
    /// Returns true if successful.
    /// </summary>
    public bool DeleteProfile(string profileId) => _profileManagementService.DeleteProfile(profileId);
    
    /// <summary>
    /// Switches to a profile by its index.
    /// Returns true if successful.
    /// </summary>
    public bool SwitchProfile(int index) => _profileManagementService.SwitchProfile(index);
    
    /// <summary>
    /// Updates an existing profile.
    /// </summary>
    public bool UpdateProfile(string profileId, string? newName, string? newUuid) => 
        _profileManagementService.UpdateProfile(profileId, newName, newUuid);
    
    /// <summary>
    /// Gets the path to the Profiles folder.
    /// </summary>
    private string GetProfilesFolder() => _profileManagementService.GetProfilesFolder();
    
    /// <summary>
    /// Saves the current UUID/Nick as a new profile.
    /// Returns the created profile.
    /// </summary>
    public Profile? SaveCurrentAsProfile() => _profileManagementService.SaveCurrentAsProfile();
    
    /// <summary>
    /// Duplicates an existing profile (copies UserData folder too).
    /// Returns the newly created profile.
    /// </summary>
    public Profile? DuplicateProfile(string profileId) => _profileManagementService.DuplicateProfile(profileId);
    
    /// <summary>
    /// Duplicates an existing profile WITHOUT copying UserData folder.
    /// Only copies settings and configuration.
    /// Returns the newly created profile.
    /// </summary>
    public Profile? DuplicateProfileWithoutData(string profileId) => _profileManagementService.DuplicateProfileWithoutData(profileId);
    
    /// <summary>
    /// Opens the current active profile's folder in the file manager.
    /// </summary>
    public bool OpenCurrentProfileFolder() => _profileManagementService.OpenCurrentProfileFolder();

    public Task<string?> SetInstanceDirectoryAsync(string path)
    {
        try
        {
            // If path is empty or whitespace, clear the custom instance directory
            if (string.IsNullOrWhiteSpace(path))
            {
                _config.InstanceDirectory = null!;
                SaveConfig();
                Logger.Success("Config", "Instance directory cleared, using default");
                return Task.FromResult<string?>(null);
            }

            var expanded = Environment.ExpandEnvironmentVariables(path.Trim());

            if (!Path.IsPathRooted(expanded))
            {
                expanded = Path.GetFullPath(Path.Combine(_appDir, expanded));
            }

            Directory.CreateDirectory(expanded);

            _config.InstanceDirectory = expanded;
            SaveConfig();

            Logger.Success("Config", $"Instance directory set to {expanded}");
            return Task.FromResult<string?>(expanded);
        }
        catch (Exception ex)
        {
            Logger.Error("Config", $"Failed to set instance directory: {ex.Message}");
            return Task.FromResult<string?>(null);
        }
    }

    public string GetLauncherVersion() => _updateService.GetLauncherVersion();

    /// <summary>
    /// Check if Rosetta 2 is installed on macOS Apple Silicon.
    /// Returns null if not on macOS or if Rosetta is installed.
    /// Returns a warning object if Rosetta is needed but not installed.
    /// </summary>
    public RosettaStatus? CheckRosettaStatus() => _rosettaService.CheckRosettaStatus();

    // Version Management
    public string GetVersionType() => _config.VersionType;
    
    public bool SetVersionType(string versionType)
    {
        _config.VersionType = UtilityService.NormalizeVersionType(versionType);
        SaveConfig();
        return true;
    }

    // Returns list of available version numbers by checking Hytale's patch server
    // Uses caching to start from the last known version instead of version 1
    public async Task<List<int>> GetVersionListAsync(string branch) => await _versionService.GetVersionListAsync(branch);

    public bool SetSelectedVersion(int versionNumber)
    {
        _config.SelectedVersion = versionNumber;
        SaveConfig();
        return true;
    }

    public bool IsVersionInstalled(string branch, int versionNumber)
    {
        var normalizedBranch = UtilityService.NormalizeVersionType(branch);

        // Version 0 means "latest" - check if any version is installed
        if (versionNumber == 0)
        {
            var resolvedLatest = ResolveInstancePath(normalizedBranch, 0, preferExisting: true);
            bool hasClient = _instanceService.IsClientPresent(resolvedLatest);
            Logger.Info("Version", $"IsVersionInstalled check for version 0 (latest): path={resolvedLatest}, hasClient={hasClient}");
            return hasClient;
        }
        
        string versionPath = ResolveInstancePath(normalizedBranch, versionNumber, preferExisting: true);

        if (!_instanceService.IsClientPresent(versionPath))
        {
            // Last chance: try legacy dash naming in legacy roots
            var legacy = _instanceService.FindExistingInstancePath(normalizedBranch, versionNumber);
            if (!string.IsNullOrWhiteSpace(legacy))
            {
                Logger.Info("Version", $"IsVersionInstalled: found legacy layout at {legacy}");
                return _instanceService.IsClientPresent(legacy);
            }
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if Assets.zip exists for the specified branch and version.
    /// Assets.zip is required for the skin customizer to work.
    /// </summary>
    public bool HasAssetsZip(string branch, int version)
    {
        var normalizedBranch = UtilityService.NormalizeVersionType(branch);
        var versionPath = ResolveInstancePath(normalizedBranch, version, preferExisting: true);
        return _assetService.HasAssetsZip(versionPath);
    }
    
    /// <summary>
    /// Gets the path to Assets.zip if it exists, or null if not found.
    /// </summary>
    public string? GetAssetsZipPath(string branch, int version)
    {
        var normalizedBranch = UtilityService.NormalizeVersionType(branch);
        var versionPath = ResolveInstancePath(normalizedBranch, version, preferExisting: true);
        return _assetService.GetAssetsZipPathIfExists(versionPath);
    }
    
    /// <summary>
    /// Gets the available cosmetics from the Assets.zip file for the specified instance.
    /// Returns a dictionary where keys are category names and values are lists of cosmetic IDs.
    /// </summary>
    public Dictionary<string, List<string>>? GetCosmeticsList(string branch, int version)
    {
        var normalizedBranch = UtilityService.NormalizeVersionType(branch);
        var versionPath = ResolveInstancePath(normalizedBranch, version, preferExisting: true);
        return _assetService.GetCosmeticsList(versionPath);
    }

    public List<int> GetInstalledVersionsForBranch(string branch)
    {
        var normalizedBranch = UtilityService.NormalizeVersionType(branch);
        var result = new HashSet<int>();

        foreach (var root in _instanceService.GetInstanceRootsIncludingLegacy())
        {
            // New layout: branch/version
            string branchPath = Path.Combine(root, normalizedBranch);
            if (Directory.Exists(branchPath))
            {
                foreach (var dir in Directory.GetDirectories(branchPath))
                {
                    var name = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (string.Equals(name, "latest", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_instanceService.IsClientPresent(dir))
                        {
                            result.Add(0);
                            Logger.Info("Version", $"Installed versions include latest for {normalizedBranch} at {dir}");
                        }
                        continue;
                    }

                    if (int.TryParse(name, out int version))
                    {
                        if (_instanceService.IsClientPresent(dir))
                        {
                            result.Add(version);
                            Logger.Info("Version", $"Installed version detected: {normalizedBranch}/{version} at {dir}");
                        }
                    }
                }
            }

            // Legacy dash layout: release-29 or release-v29
            foreach (var dir in Directory.GetDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name)) continue;
                if (!name.StartsWith(normalizedBranch + "-", StringComparison.OrdinalIgnoreCase)) continue;

                var suffix = name.Substring(normalizedBranch.Length + 1);
                
                // Remove 'v' prefix if present (e.g., "v5" -> "5")
                if (suffix.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                {
                    suffix = suffix.Substring(1);
                }

                if (string.Equals(suffix, "latest", StringComparison.OrdinalIgnoreCase))
                {
                    if (_instanceService.IsClientPresent(dir))
                    {
                        result.Add(0);
                        Logger.Info("Version", $"Installed legacy latest detected: {name} at {dir}");
                    }
                    continue;
                }

                if (int.TryParse(suffix, out int version))
                {
                    if (_instanceService.IsClientPresent(dir))
                    {
                        result.Add(version);
                        Logger.Info("Version", $"Installed legacy version detected: {name} at {dir}");
                    }
                }
            }
        }
        
        return result.ToList();
    }

    public async Task<bool> CheckLatestNeedsUpdateAsync(string branch)
    {
        var normalizedBranch = UtilityService.NormalizeVersionType(branch);
        var versions = await GetVersionListAsync(normalizedBranch);
        if (versions.Count == 0) return false;

        var latest = versions[0];
        var latestPath = GetLatestInstancePath(normalizedBranch);
        var info = _instanceService.LoadLatestInfo(normalizedBranch);
        var baseOk = _instanceService.IsClientPresent(latestPath);
        if (!baseOk) return true;
        if (info == null)
        {
            // Game is installed but no version tracking - assume needs update to be safe
            // Don't write anything here - let the user decide via UPDATE button
            Logger.Info("Update", $"No latest.json found for {normalizedBranch}, assuming update may be needed");
            return true;
        }
        return info.Version != latest;
    }
    
    /// <summary>
    /// Gets the version status for the latest instance.
    /// </summary>
    public async Task<VersionStatus> GetLatestVersionStatusAsync(string branch)
    {
        return await _versionService.GetLatestVersionStatusAsync(
            branch,
            _instanceService.IsClientPresent,
            () => GetLatestInstancePath(UtilityService.NormalizeVersionType(branch)),
            (b) => {
                var info = _instanceService.LoadLatestInfo(b);
                return info != null ? new LatestVersionInfo { Version = info.Version } : null;
            }
        );
    }
    
    /// <summary>
    /// Forces the latest instance to update by resetting its version info.
    /// This will trigger a differential update on next launch.
    /// </summary>
    public async Task<bool> ForceUpdateLatestAsync(string branch) => 
        await _updateService.ForceUpdateLatestAsync(branch);
    
    /// <summary>
    /// Duplicates the current latest instance as a versioned instance.
    /// </summary>
    public async Task<bool> DuplicateLatestAsync(string branch) => 
        await _updateService.DuplicateLatestAsync(branch);

    /// <summary>
    /// Get information about the pending update, including old version details.
    /// Returns null if no update is pending.
    /// </summary>
    public async Task<UpdateInfo?> GetPendingUpdateInfoAsync(string branch)
    {
        try
        {
            var normalizedBranch = UtilityService.NormalizeVersionType(branch);
            var versions = await GetVersionListAsync(normalizedBranch);
            if (versions.Count == 0) return null;

            var latestVersion = versions[0];
            var latestPath = GetLatestInstancePath(normalizedBranch);
            var info = _instanceService.LoadLatestInfo(normalizedBranch);
            
            // Check if update is needed
            if (info == null || info.Version == latestVersion) return null;
            
            // Check if old version has userdata
            var oldUserDataPath = Path.Combine(latestPath, "UserData");
            var hasOldUserData = Directory.Exists(oldUserDataPath) && 
                                 Directory.GetFileSystemEntries(oldUserDataPath).Length > 0;
            
            return new UpdateInfo
            {
                OldVersion = info.Version,
                NewVersion = latestVersion,
                HasOldUserData = hasOldUserData,
                Branch = normalizedBranch
            };
        }
        catch (Exception ex)
        {
            Logger.Warning("Update", $"Failed to get pending update info: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Copy userdata from one version to another.
    /// </summary>
    public async Task<bool> CopyUserDataAsync(string branch, int fromVersion, int toVersion)
    {
        try
        {
            var normalizedBranch = UtilityService.NormalizeVersionType(branch);
            
            // Get source path (if fromVersion is 0, use latest)
            string fromPath;
            if (fromVersion == 0)
            {
                fromPath = GetLatestInstancePath(normalizedBranch);
            }
            else
            {
                fromPath = ResolveInstancePath(normalizedBranch, fromVersion, preferExisting: true);
            }
            
            // Get destination path (if toVersion is 0, use latest)
            string toPath;
            if (toVersion == 0)
            {
                toPath = GetLatestInstancePath(normalizedBranch);
            }
            else
            {
                toPath = ResolveInstancePath(normalizedBranch, toVersion, preferExisting: true);
            }
            
            var fromUserData = Path.Combine(fromPath, "UserData");
            var toUserData = Path.Combine(toPath, "UserData");
            
            if (!Directory.Exists(fromUserData))
            {
                Logger.Warning("UserData", $"Source UserData does not exist: {fromUserData}");
                return false;
            }
            
            // Create destination if needed
            Directory.CreateDirectory(toUserData);
            
            // Copy all contents
            await Task.Run(() => UtilityService.CopyDirectory(fromUserData, toUserData, true));
            
            Logger.Success("UserData", $"Copied UserData from v{fromVersion} to v{toVersion}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("UserData", $"Failed to copy userdata: {ex.Message}");
            return false;
        }
    }

    // Game
    public async Task<DownloadProgress> DownloadAndLaunchAsync() => await _gameSessionService.DownloadAndLaunchAsync();

    private void SendErrorEvent(string type, string message, string? technical = null)
    {
        _progressNotificationService.SendErrorEvent(type, message, technical);
    }

    // Check for launcher updates and emit event if available
    public async Task CheckForLauncherUpdatesAsync() => 
        await _updateService.CheckForLauncherUpdatesAsync();

    public bool IsGameRunning() => _gameProcessService.IsGameRunning();

    public List<string> GetRecentLogs(int count = 10)
    {
        return Logger.GetRecentLogs(count);
    }

    public bool ExitGame() => _gameProcessService.ExitGame();

    public bool DeleteGame(string branch, int versionNumber) => _instanceService.DeleteGame(branch, versionNumber);

    // Folder
    public bool OpenFolder() => _fileService.OpenAppFolder();

    public Task<string?> SelectInstanceDirectoryAsync()
    {
        // Folder picker is not available in Photino. Return the current/active
        // instance root so the frontend can show it and collect user input manually.
        return Task.FromResult<string?>(_instanceService.GetInstanceRoot());
    }
    
    /// <summary>
    /// Opens a folder browser dialog and returns the selected path.
    /// </summary>
    public async Task<string?> BrowseFolder(string? initialPath = null)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var script = $@"Add-Type -AssemblyName System.Windows.Forms; $dialog = New-Object System.Windows.Forms.FolderBrowserDialog; ";
                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                    script += $@"$dialog.SelectedPath = '{initialPath.Replace("'", "''")}'; ";
                script += @"if ($dialog.ShowDialog() -eq 'OK') { $dialog.SelectedPath }";
                
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"{script}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null) return null;
                
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                return string.IsNullOrWhiteSpace(output) ? null : output.Trim();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var initialDir = !string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath) 
                    ? $"default location \"{initialPath}\"" 
                    : "";
                    
                var script = $@"tell application ""Finder""
                    activate
                    set theFolder to choose folder with prompt ""Select Folder"" {initialDir}
                    return POSIX path of theFolder
                end tell";
                
                var psi = new ProcessStartInfo
                {
                    FileName = "osascript",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null) return null;
                
                await process.StandardInput.WriteAsync(script);
                process.StandardInput.Close();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                return string.IsNullOrWhiteSpace(output) ? null : output.Trim();
            }
            else
            {
                // Linux - use zenity
                var args = "--file-selection --directory --title=\"Select Folder\"";
                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                    args += $" --filename=\"{initialPath}/\"";
                    
                var psi = new ProcessStartInfo
                {
                    FileName = "zenity",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process == null) return null;
                
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                return string.IsNullOrWhiteSpace(output) ? null : output.Trim();
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("Files", $"Failed to browse folder: {ex.Message}");
            return null;
        }
    }

    // News - matches Go implementation
    public async Task<List<NewsItemResponse>> GetNewsAsync(int count) => await _newsService.GetNewsAsync(count);
    
    /// <summary>
    /// Synchronous wrapper for GetNewsAsync to maintain compatibility with frontend.
    /// </summary>
    public Task<List<NewsItemResponse>> GetNews(int count) => GetNewsAsync(count);

    /// <summary>
    /// Cleans news excerpt by removing HTML tags, duplicate title, and date prefixes.
    /// From PR #294
    /// </summary>
    // Update - download latest launcher per platform instead of in-place update
    public async Task<bool> UpdateAsync(JsonElement[]? args) => 
        await _updateService.UpdateAsync(args);

    // Browser
    public bool BrowserOpenURL(string url) => _browserService.OpenURL(url);

    // ========== Settings (delegated to SettingsService) ==========
    
    public bool GetMusicEnabled() => _settingsService.GetMusicEnabled();
    public bool SetMusicEnabled(bool enabled) => _settingsService.SetMusicEnabled(enabled);
    
    public string GetLauncherBranch() => _settingsService.GetLauncherBranch();
    public bool SetLauncherBranch(string branch) => _settingsService.SetLauncherBranch(branch);
    
    public bool GetCloseAfterLaunch() => _settingsService.GetCloseAfterLaunch();
    public bool SetCloseAfterLaunch(bool enabled) => _settingsService.SetCloseAfterLaunch(enabled);
    
    public bool GetShowDiscordAnnouncements() => _settingsService.GetShowDiscordAnnouncements();
    public bool SetShowDiscordAnnouncements(bool enabled) => _settingsService.SetShowDiscordAnnouncements(enabled);
    public bool IsAnnouncementDismissed(string announcementId) => _settingsService.IsAnnouncementDismissed(announcementId);
    public bool DismissAnnouncement(string announcementId) => _settingsService.DismissAnnouncement(announcementId);
    
    public bool GetDisableNews() => _settingsService.GetDisableNews();
    public bool SetDisableNews(bool disabled) => _settingsService.SetDisableNews(disabled);
    
    public string GetBackgroundMode() => _settingsService.GetBackgroundMode();
    public bool SetBackgroundMode(string mode) => _settingsService.SetBackgroundMode(mode);
    public List<string> GetAvailableBackgrounds() => _settingsService.GetAvailableBackgrounds();
    
    public string GetAccentColor() => _settingsService.GetAccentColor();
    public bool SetAccentColor(string color) => _settingsService.SetAccentColor(color);
    
    public bool GetHasCompletedOnboarding() => _settingsService.GetHasCompletedOnboarding();
    public bool SetHasCompletedOnboarding(bool completed) => _settingsService.SetHasCompletedOnboarding(completed);
    
    /// <summary>
    /// Generates a random username for the onboarding flow.
    /// </summary>
    public string GetRandomUsername() => UtilityService.GenerateRandomUsername();
    
    public bool ResetOnboarding() => _settingsService.ResetOnboarding();
    
    public bool GetOnlineMode() => _settingsService.GetOnlineMode();
    public bool SetOnlineMode(bool online) => _settingsService.SetOnlineMode(online);
    
    public string GetAuthDomain() => _settingsService.GetAuthDomain();
    public bool SetAuthDomain(string domain) => _settingsService.SetAuthDomain(domain);
    
    public string GetLauncherDataDirectory() => _settingsService.GetLauncherDataDirectory();
    public Task<string?> SetLauncherDataDirectoryAsync(string path) => _settingsService.SetLauncherDataDirectoryAsync(path);

    // Delegate to ModService
    public List<InstalledMod> GetInstanceInstalledMods(string instancePath) => 
        ModService.GetInstanceInstalledMods(instancePath);
    
    /// <summary>
    /// Convenience overload that gets installed mods by branch and version.
    /// </summary>
    public List<InstalledMod> GetInstanceInstalledMods(string branch, int version)
    {
        var instancePath = GetInstancePath(branch, version);
        return ModService.GetInstanceInstalledMods(instancePath);
    }
    
    /// <summary>
    /// Opens the instance folder in the file manager.
    /// </summary>
    public bool OpenInstanceFolder(string branch, int version) 
    {
         var path = _instanceService.ResolveInstancePath(UtilityService.NormalizeVersionType(branch), version, true);
         return _fileService.OpenFolder(path);
    }

    // CurseForge API constants
    private const string CurseForgeBaseUrl = "https://api.curseforge.com/v1";
    private const int HytaleGameId = 70216; // Hytale game ID on CurseForge
    private const string CurseForgeApiKey = "$2a$10$bL4bIL5pUWqfcO7KQtnMReakwtfHbNKh6v1uTpKlzhwoueEJQnPnm";

    // Mod Manager with CurseForge API
    public async Task<ModSearchResult> SearchModsAsync(string query, int page, int pageSize, string[] categories, int sortField, int sortOrder)
        => await _modService.SearchModsAsync(query, page, pageSize, categories, sortField, sortOrder);

    public async Task<ModFilesResult> GetModFilesAsync(string modId, int page, int pageSize)
        => await _modService.GetModFilesAsync(modId, page, pageSize);

    public async Task<List<ModCategory>> GetModCategoriesAsync()
        => await _modService.GetModCategoriesAsync();

    /// <summary>
    /// Browse for mod files using native OS dialog.
    /// Returns array of selected file paths or empty array if cancelled.
    /// </summary>
    public async Task<string[]> BrowseModFilesAsync() => await _fileDialogService.BrowseModFilesAsync();

    /// <summary>
    /// Triggers a test Discord announcement popup for developer testing.
    /// </summary>
    public DiscordAnnouncement? GetTestAnnouncement()
    {
        return new DiscordAnnouncement
        {
            Id = "test-announcement-" + DateTime.UtcNow.Ticks,
            AuthorName = "HyPrism Bot",
            AuthorAvatar = null,
            AuthorRole = "Developer",
            RoleColor = "#FFA845",
            Content = "🎉 This is a test announcement!\n\nThis is used to preview how Discord announcements will appear in the launcher. You can dismiss this by clicking the X button or disabling announcements.\n\n✨ Features:\n• Author info with avatar\n• Role colors\n• Images and attachments\n• Smooth animations",
            ImageUrl = null,
            Timestamp = DateTime.UtcNow.ToString("o")
        };
    }

    /// <summary>
    /// Sets the game language by copying translated language files to the game's language folder.
    /// Maps launcher locale codes to game locale codes and copies appropriate language files.
    /// </summary>
    /// <param name="languageCode">The launcher language code (e.g., "en", "es", "de", "fr")</param>
    /// <returns>True if language files were successfully copied, false otherwise</returns>
    public async Task<bool> SetGameLanguageAsync(string languageCode) => await _languageService.SetGameLanguageAsync(languageCode);

    /// <summary>
    /// Gets the list of available game languages that have translation files.
    /// </summary>
    public List<string> GetAvailableGameLanguages() => _languageService.GetAvailableGameLanguages();
    
    /// <summary>
    /// Gets the current music enabled state from configuration.
    /// </summary>
    public async Task<bool> GetMusicEnabledAsync()
    {
        await Task.CompletedTask;
        return _settingsService.GetMusicEnabled();
    }

    // ================== WRAPPER MODE METHODS ==================
    // Used for Flatpak/AppImage wrapper to install/launch HyPrism

    /// <summary>
    /// Wrapper Mode: Get status of the installed HyPrism binary and check for updates.
    /// Returns: { installed: bool, version: string, needsUpdate: bool, latestVersion: string }
    /// </summary>
    public async Task<Dictionary<string, object>> WrapperGetStatus()
    {
        var result = new Dictionary<string, object>
        {
            ["installed"] = false,
            ["version"] = "",
            ["needsUpdate"] = false,
            ["latestVersion"] = ""
        };

        try
        {
            var wrapperDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HyPrism", "wrapper");
            var binaryPath = Path.Combine(wrapperDir, "HyPrism");
            var versionFile = Path.Combine(wrapperDir, "version.txt");

            if (!File.Exists(binaryPath))
            {
                return result;
            }

            result["installed"] = true;

            if (File.Exists(versionFile))
            {
                result["version"] = (await File.ReadAllTextAsync(versionFile)).Trim();
            }

            // Check GitHub for latest release
            var latestVersion = await GetLatestLauncherVersionFromGitHub();
            if (!string.IsNullOrEmpty(latestVersion))
            {
                result["latestVersion"] = latestVersion;
                result["needsUpdate"] = result["version"].ToString() != latestVersion;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WrapperGetStatus error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Wrapper Mode: Install or update the latest HyPrism binary from GitHub releases.
    /// Downloads the appropriate release for the current OS and extracts it to wrapper directory.
    /// </summary>
    public async Task<bool> WrapperInstallLatest()
    {
        try
        {
            var wrapperDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HyPrism", "wrapper");
            Directory.CreateDirectory(wrapperDir);

            // Get latest release from GitHub
            var latestVersion = await GetLatestLauncherVersionFromGitHub();
            if (string.IsNullOrEmpty(latestVersion))
            {
                Console.WriteLine("Failed to get latest version from GitHub");
                return false;
            }

            // Determine the asset name based on OS
            string assetName;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                assetName = $"HyPrism-{latestVersion}-linux-x64.tar.gz";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                assetName = $"HyPrism-{latestVersion}-win-x64.zip";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                assetName = $"HyPrism-{latestVersion}-osx-x64.tar.gz";
            }
            else
            {
                Console.WriteLine($"Unsupported platform: {RuntimeInformation.OSDescription}");
                return false;
            }

            var downloadUrl = $"https://github.com/yyyumeniku/HyPrism/releases/download/{latestVersion}/{assetName}";
            var archivePath = Path.Combine(wrapperDir, assetName);

            // Download archive
            _progressNotificationService.SendProgress("wrapper-install", 0, "Downloading HyPrism...", 0, 100);
            
            var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to download: {response.StatusCode}");
                return false;
            }

            await using (var contentStream = await response.Content.ReadAsStreamAsync())
            await using (var fileStream = File.Create(archivePath))
            {
                await contentStream.CopyToAsync(fileStream);
            }

            _progressNotificationService.SendProgress("wrapper-install", 50, "Extracting...", 50, 100);

            // Extract archive
            if (assetName.EndsWith(".tar.gz"))
            {
                await ExtractTarGz(archivePath, wrapperDir);
            }
            else if (assetName.EndsWith(".zip"))
            {
                ZipFile.ExtractToDirectory(archivePath, wrapperDir, true);
            }

            // Set executable permission on Linux/Mac
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var binaryPath = Path.Combine(wrapperDir, "HyPrism");
                if (File.Exists(binaryPath))
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"+x \"{binaryPath}\"",
                            UseShellExecute = false
                        }
                    };
                    process.Start();
                    await process.WaitForExitAsync();
                }
            }

            // Save version
            await File.WriteAllTextAsync(Path.Combine(wrapperDir, "version.txt"), latestVersion);

            // Cleanup archive
            File.Delete(archivePath);

            _progressNotificationService.SendProgress("wrapper-install", 100, "Installation complete", 100, 100);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WrapperInstallLatest error: {ex.Message}");
            _progressNotificationService.SendErrorEvent("Wrapper Installation Error", ex.Message, null);
            return false;
        }
    }

    /// <summary>
    /// Wrapper Mode: Launch the installed HyPrism binary.
    /// </summary>
    public async Task<bool> WrapperLaunch()
    {
        try
        {
            var wrapperDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HyPrism", "wrapper");
            var binaryPath = Path.Combine(wrapperDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "HyPrism.exe" : "HyPrism");

            if (!File.Exists(binaryPath))
            {
                Console.WriteLine("HyPrism binary not found");
                return false;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = binaryPath,
                    UseShellExecute = true,
                    WorkingDirectory = wrapperDir
                }
            };

            process.Start();
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WrapperLaunch error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Helper: Extract .tar.gz archive (for Linux/Mac releases).
    /// </summary>
    private static async Task ExtractTarGz(string archivePath, string destinationDir)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf \"{archivePath}\" -C \"{destinationDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Failed to extract tar.gz: {error}");
        }
    }

    /// <summary>
    /// Helper: Get latest launcher version from GitHub releases API.
    /// </summary>
    private async Task<string> GetLatestLauncherVersionFromGitHub()
    {
        try
        {
            var response = await _httpClient.GetStringAsync("https://api.github.com/repos/yyyumeniku/HyPrism/releases/latest");
            var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("tag_name", out var tagName))
            {
                return tagName.GetString() ?? "";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to get latest version: {ex.Message}");
        }
        return "";
    }

    // ================== OPTIMIZATION MODS ==================

    /// <summary>
    /// Installs a predefined list of optimization mods to the current game instance.
    /// Optimization mods improve game performance and FPS.
    /// TODO: Define actual optimization mod list when Hytale mod ecosystem is available.
    /// </summary>
    public async Task<bool> InstallOptimizationModsAsync()
    {
        try
        {
            // Predefined list of optimization mod IDs from CurseForge
            // TODO: Add actual mod IDs when Hytale optimization mods become available
            var optimizationModIds = new List<string>
            {
                // Example: "sodium", "lithium", "phosphor", "ferritecore", etc.
            };

            if (optimizationModIds.Count == 0)
            {
                Logger.Warning("OptimizationMods", "No optimization mods defined yet - feature is a stub");
                return false;
            }

            // Get current game instance path
            var branch = UtilityService.NormalizeVersionType(_config.VersionType);
            var instancePath = GetLatestInstancePath(branch);
            
            if (!_instanceService.IsClientPresent(instancePath))
            {
                Logger.Warning("OptimizationMods", "Game client not installed - cannot install optimization mods");
                return false;
            }

            // Install each optimization mod
            foreach (var modId in optimizationModIds)
            {
                try
                {
                    // Get latest mod file
                    var filesResult = await _modService.GetModFilesAsync(modId, 1, 10);
                    if (filesResult.Files.Count == 0) continue;

                    var latestFile = filesResult.Files[0];
                    
                    // Install mod to instance
                    await _modService.InstallModFileToInstanceAsync(
                        modId, 
                        latestFile.Id.ToString(), 
                        instancePath,
                        (stage, msg) => _progressNotificationService.SendProgress(
                            "optimization-mods", 
                            0, 
                            $"Installing {latestFile.DisplayName}: {msg}", 
                            0, 
                            100
                        )
                    );
                    
                    Logger.Success("OptimizationMods", $"Installed optimization mod: {latestFile.DisplayName}");
                }
                catch (Exception ex)
                {
                    Logger.Warning("OptimizationMods", $"Failed to install mod {modId}: {ex.Message}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("OptimizationMods", $"InstallOptimizationModsAsync error: {ex.Message}");
            _progressNotificationService.SendErrorEvent("Optimization Mods Installation Error", ex.Message, null);
            return false;
        }
    }
}
