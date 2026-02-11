using System.Text.Json;
using System.Runtime.InteropServices;
using HyPrism.Models;
using HyPrism.Services.Core;

namespace HyPrism.Services.Game;

/// <summary>
/// Manages game version detection, update checking, and version caching.
/// Queries the Hytale patch server to determine available versions.
/// </summary>
/// <remarks>
/// Version information is cached to avoid excessive network requests.
/// Uses parallel batch checking for efficient version discovery.
/// </remarks>
public class VersionService : IVersionService
{
    private readonly string _appDir;
    private readonly HttpClient _httpClient;
    private readonly IConfigService _configService;
    private readonly MirrorService _mirrorService;
    private readonly HashSet<string> _mirrorSourcedBranches = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// Temporarily holds mirror URLs fetched during GetVersionListAsync,
    /// to be persisted in the next SaveVersionsCacheSnapshot call.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string>> _pendingMirrorUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _versionFetchLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionService"/> class.
    /// </summary>
    /// <param name="appDir">The application data directory for storing cache files.</param>
    /// <param name="httpClient">The HTTP client for API requests.</param>
    /// <param name="configService">The configuration service for accessing settings.</param>
    /// <param name="mirrorService">Fallback mirror service for version discovery when official servers are down.</param>
    public VersionService(string appDir, HttpClient httpClient, IConfigService configService, MirrorService mirrorService)
    {
        _appDir = appDir;
        _httpClient = httpClient;
        _configService = configService;
        _mirrorService = mirrorService;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uses caching to avoid re-checking all versions every time.
    /// Checks versions in parallel batches for performance.
    /// </remarks>
    public async Task<List<int>> GetVersionListAsync(string branch, CancellationToken ct = default)
    {
        var normalizedBranch = NormalizeBranch(branch);
        string osName = UtilityService.GetOS();
        string arch = UtilityService.GetArch();

        // Fast path: return from cache without locking
        var freshCache = TryLoadFreshVersionsCache(normalizedBranch, osName, arch, TimeSpan.FromMinutes(15));
        if (freshCache != null)
        {
            Logger.Info("Version", $"Using cached versions for {branch}");
            return freshCache;
        }

        // Serialize network fetches so parallel callers don't duplicate work
        await _versionFetchLock.WaitAsync(ct);
        try
        {
            // Re-check cache: another caller may have populated it while we waited
            freshCache = TryLoadFreshVersionsCache(normalizedBranch, osName, arch, TimeSpan.FromMinutes(15));
            if (freshCache != null)
            {
                Logger.Info("Version", $"Using cached versions for {branch}");
                return freshCache;
            }

            return await FetchVersionListCoreAsync(normalizedBranch, osName, arch, ct);
        }
        finally
        {
            _versionFetchLock.Release();
        }
    }

    private async Task<List<int>> FetchVersionListCoreAsync(string normalizedBranch, string osName, string arch, CancellationToken ct)
    {
        var result = new List<int>();

        const int batchSize = 20;
        const int maxConsecutiveFailures = 20;
        int batchStart = 0;
        int consecutiveFailures = 0;
        using var semaphore = new SemaphoreSlim(8);

        while (consecutiveFailures < maxConsecutiveFailures)
        {
            ct.ThrowIfCancellationRequested();

            var tasks = Enumerable.Range(batchStart, batchSize)
                .Select(async version =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        return await CheckVersionExistsAsync(osName, arch, normalizedBranch, version, ct);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

            var batchResults = await Task.WhenAll(tasks);

            foreach (var (version, exists) in batchResults.OrderBy(r => r.version))
            {
                if (exists)
                {
                    result.Add(version);
                    consecutiveFailures = 0;
                }
                else
                {
                    consecutiveFailures++;
                    if (consecutiveFailures >= maxConsecutiveFailures) break;
                }
            }

            batchStart += batchSize;
        }

        result.Sort((a, b) => b.CompareTo(a)); // Sort descending (latest first)

        // If official server returned nothing, fall back to mirror
        if (result.Count == 0)
        {
            Logger.Warning("Version", $"Official server returned no versions for {normalizedBranch}, trying mirror...");
            try
            {
                var mirrorVersions = await _mirrorService.GetAvailableVersionsAsync(osName, arch, normalizedBranch, ct);
                if (mirrorVersions.Count > 0)
                {
                    result = mirrorVersions;
                    _mirrorSourcedBranches.Add(normalizedBranch);

                    // Also fetch and cache the mirror URLs so we can use them from disk later
                    var mirrorUrls = await _mirrorService.GetAllPlatformUrlsAsync(osName, arch, normalizedBranch, ct);
                    if (mirrorUrls.Count > 0)
                        _pendingMirrorUrls[normalizedBranch] = mirrorUrls;

                    Logger.Success("Version", $"Mirror provided {result.Count} versions for {normalizedBranch}: [{string.Join(", ", result)}]");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("Version", $"Mirror fallback also failed: {ex.Message}");
            }
        }

        SaveVersionsCacheSnapshot(normalizedBranch, osName, arch, result);
        
        Logger.Info("Version", $"Found {result.Count} versions for {normalizedBranch}: [{string.Join(", ", result)}]");
        return result;
    }

    public bool TryGetCachedVersions(string branch, TimeSpan maxAge, out List<int> versions)
    {
        versions = new List<int>();
        var normalizedBranch = NormalizeBranch(branch);
        string osName = UtilityService.GetOS();
        string arch = UtilityService.GetArch();

        var cached = TryLoadFreshVersionsCache(normalizedBranch, osName, arch, maxAge);
        if (cached == null)
        {
            return false;
        }

        versions = cached;
        return versions.Count > 0;
    }

    /// <summary>
    /// Check if a specific version exists on the server.
    /// </summary>
    private async Task<(int version, bool exists)> CheckVersionExistsAsync(string os, string arch, string versionType, int version, CancellationToken ct = default)
    {
        try
        {
            string url = $"https://game-patches.hytale.com/patches/{os}/{arch}/{versionType}/0/{version}.pwr";
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            return (version, response.IsSuccessStatusCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return (version, false);
        }
    }

    /// <summary>
    /// Check if latest instance needs an update.
    /// </summary>
    public async Task<bool> CheckLatestNeedsUpdateAsync(string branch, Func<string, bool> isClientPresent, Func<string> getLatestInstancePath, Func<string, LatestVersionInfo?> loadLatestInfo)
    {
        var normalizedBranch = NormalizeBranch(branch);
        var versions = await GetVersionListAsync(normalizedBranch);
        if (versions.Count == 0) return false;

        var latest = versions[0];
        var latestPath = getLatestInstancePath();
        var info = loadLatestInfo(normalizedBranch);
        var baseOk = isClientPresent(latestPath);
        if (!baseOk) return true;
        if (info == null)
        {
            Logger.Info("Update", $"No latest.json found for {normalizedBranch}, assuming update may be needed");
            return true;
        }
        return info.Version != latest;
    }
    
    /// <summary>
    /// Gets the version status for the latest instance.
    /// Returns detailed status with installed and latest version numbers.
    /// </summary>
    public async Task<VersionStatus> GetLatestVersionStatusAsync(string branch, Func<string, bool> isClientPresent, Func<string> getLatestInstancePath, Func<string, LatestVersionInfo?> loadLatestInfo)
    {
        try
        {
            var normalizedBranch = NormalizeBranch(branch);
            var versions = await GetVersionListAsync(normalizedBranch);
            
            if (versions.Count == 0)
            {
                return new VersionStatus { Status = "none", InstalledVersion = 0, LatestVersion = 0 };
            }
            
            var latestAvailable = versions[0];
            var latestPath = getLatestInstancePath();
            var info = loadLatestInfo(normalizedBranch);
            var baseOk = isClientPresent(latestPath);
            
            // Not installed
            if (!baseOk)
            {
                return new VersionStatus 
                { 
                    Status = "not_installed", 
                    InstalledVersion = 0, 
                    LatestVersion = latestAvailable 
                };
            }
            
            // No version info - assume update needed
            if (info == null)
            {
                return new VersionStatus 
                { 
                    Status = "update_available", 
                    InstalledVersion = 0, 
                    LatestVersion = latestAvailable 
                };
            }
            
            // Compare versions
            if (info.Version < latestAvailable)
            {
                return new VersionStatus 
                { 
                    Status = "update_available", 
                    InstalledVersion = info.Version, 
                    LatestVersion = latestAvailable 
                };
            }
            
            // Current version
            return new VersionStatus 
            { 
                Status = "current", 
                InstalledVersion = info.Version, 
                LatestVersion = latestAvailable 
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Version", $"Failed to get latest version status: {ex.Message}");
            return new VersionStatus { Status = "error", InstalledVersion = 0, LatestVersion = 0 };
        }
    }

    /// <summary>
    /// Get pending update information.
    /// </summary>
    public async Task<UpdateInfo?> GetPendingUpdateInfoAsync(string branch, Func<string> getLatestInstancePath, Func<string, LatestVersionInfo?> loadLatestInfo)
    {
        try
        {
            var normalizedBranch = NormalizeBranch(branch);
            var versions = await GetVersionListAsync(normalizedBranch);
            if (versions.Count == 0) return null;

            var latestVersion = versions[0];
            var latestPath = getLatestInstancePath();
            var info = loadLatestInfo(normalizedBranch);
            
            if (info == null || info.Version == latestVersion) return null;
            
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
    /// Get sequence of patches to apply for differential update.
    /// </summary>
    public List<int> GetPatchSequence(int fromVersion, int toVersion)
    {
        var patches = new List<int>();
        for (int v = fromVersion + 1; v <= toVersion; v++)
        {
            patches.Add(v);
        }
        return patches;
    }

    /// <inheritdoc/>
    public bool IsOfficialServerDown(string branch)
    {
        var normalizedBranch = NormalizeBranch(branch);
        return _mirrorSourcedBranches.Contains(normalizedBranch);
    }

    // Utility methods
    private string NormalizeBranch(string branch)
    {
        return branch.ToLowerInvariant() switch
        {
            "release" => "release",
            "pre-release" => "pre-release",
            "prerelease" => "pre-release",
            "pre_release" => "pre-release",
            "beta" => "beta",
            "alpha" => "alpha",
            _ => "release"
        };
    }

    private sealed class VersionsCacheSnapshot
    {
        public DateTime FetchedAtUtc { get; set; }
        public string Os { get; set; } = "";
        public string Arch { get; set; } = "";
        public Dictionary<string, List<int>> Versions { get; set; } = new();
        public List<string> MirrorSourcedBranches { get; set; } = new();
        /// <summary>
        /// Cached mirror file URLs per branch: branch → (filename → download URL).
        /// Only populated for mirror-sourced branches.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> MirrorUrls { get; set; } = new();
    }

    private string GetVersionsCacheSnapshotPath()
        => Path.Combine(_appDir, "Cache", "Game", "versions.json");

    private List<int>? TryLoadFreshVersionsCache(string branch, string osName, string arch, TimeSpan maxAge)
    {
        try
        {
            var path = GetVersionsCacheSnapshotPath();
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;

            var snapshot = JsonSerializer.Deserialize<VersionsCacheSnapshot>(json);
            if (snapshot == null) return null;

            if (!string.Equals(snapshot.Os, osName, StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.Equals(snapshot.Arch, arch, StringComparison.OrdinalIgnoreCase)) return null;

            var age = DateTime.UtcNow - snapshot.FetchedAtUtc;
            if (age > maxAge) return null;

            if (snapshot.Versions.TryGetValue(branch, out var versions) && versions.Count > 0)
            {
                // Restore mirror-sourced flag from cache
                if (snapshot.MirrorSourcedBranches?.Contains(branch) == true)
                {
                    _mirrorSourcedBranches.Add(branch);

                    // Inject cached mirror URLs into MirrorService for offline lookups
                    if (snapshot.MirrorUrls?.TryGetValue(branch, out var urls) == true && urls.Count > 0)
                        _mirrorService.SetCachedUrls(branch, urls);
                }
                return new List<int>(versions);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("Version", $"Failed to load versions cache snapshot: {ex.Message}");
        }

        return null;
    }

    private void SaveVersionsCacheSnapshot(string branch, string osName, string arch, List<int> versions)
    {
        try
        {
            var path = GetVersionsCacheSnapshotPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            VersionsCacheSnapshot snapshot;
            if (File.Exists(path))
            {
                try
                {
                    var existingJson = File.ReadAllText(path);
                    snapshot = (!string.IsNullOrWhiteSpace(existingJson)
                        ? JsonSerializer.Deserialize<VersionsCacheSnapshot>(existingJson)
                        : null) ?? new VersionsCacheSnapshot();
                }
                catch
                {
                    snapshot = new VersionsCacheSnapshot();
                }
            }
            else
            {
                snapshot = new VersionsCacheSnapshot();
            }

            snapshot.FetchedAtUtc = DateTime.UtcNow;
            snapshot.Os = osName;
            snapshot.Arch = arch;
            snapshot.Versions[branch] = [.. versions];
            snapshot.MirrorSourcedBranches = [.. _mirrorSourcedBranches];

            // Persist mirror URLs for branches that were sourced from mirror
            if (_pendingMirrorUrls.TryGetValue(branch, out var pendingUrls))
            {
                snapshot.MirrorUrls[branch] = pendingUrls;
                _pendingMirrorUrls.Remove(branch);
            }
            // Keep existing mirror URLs for other branches
            // (they're already in snapshot if loaded from existing file)

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Logger.Warning("Version", $"Failed to save versions cache snapshot: {ex.Message}");
        }
    }
}

