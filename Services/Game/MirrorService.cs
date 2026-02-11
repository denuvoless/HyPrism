using System.Text.Json;
using System.Text.Json.Serialization;
using HyPrism.Services.Core;

namespace HyPrism.Services.Game;

/// <summary>
/// Provides fallback mirror URLs for game patch downloads when the official
/// Hytale patch server (<c>game-patches.hytale.com</c>) is unavailable.
/// Mirror source: <c>thecute.cloud/ShipOfYarn/api.php</c>
/// </summary>
/// <remarks>
/// The mirror stores two distinct types of files:
/// <list type="bullet">
///   <item><b>Release</b>: Full standalone game copies — <c>v{N}-{os}-{arch}.pwr</c>.
///     Any single file can be unpacked with Butler to get the complete game at that version.
///     No need for sequential patching.</item>
///   <item><b>Pre-release</b>: Sequential diff patches — <c>v{from}~{to}-{os}-{arch}.pwr</c>.
///     Must be applied in order: v0~1, v1~2, …, v{N-1}~{N} to build up to version N.
///     Each diff updates from one version to the next.</item>
/// </list>
/// </remarks>
public class MirrorService
{
    private const string MirrorApiUrl = "https://thecute.cloud/ShipOfYarn/api.php";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly HttpClient _httpClient;
    private readonly string _appDir;
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    private MirrorIndex? _cachedIndex;
    private DateTime _cachedAt = DateTime.MinValue;

    /// <summary>
    /// Fallback URL maps loaded from versions.json cache.
    /// Key: branch → (filename → URL).
    /// Used when the live mirror index is unavailable.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string>> _cachedUrlsByBranch = new(StringComparer.OrdinalIgnoreCase);

    public MirrorService(HttpClient httpClient, string appDir)
    {
        _httpClient = httpClient;
        _appDir = appDir;
    }

    /// <summary>
    /// Whether the mirror has any data available (live index or cached URLs).
    /// </summary>
    public bool IsAvailable => _cachedIndex != null || _cachedUrlsByBranch.Count > 0;

    /// <summary>
    /// Whether the mirror stores this branch as sequential diff patches
    /// rather than full standalone copies.
    /// Pre-release uses diffs (v{from}~{to}), release uses full copies (v{N}).
    /// </summary>
    public bool IsDiffBasedBranch(string branch) =>
        branch.Equals("pre-release", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a mirror URL for a full standalone game copy (release branch).
    /// Looks for key <c>v{version}-{os}-{arch}.pwr</c>.
    /// </summary>
    /// <param name="os">OS identifier ("windows", "darwin", "linux").</param>
    /// <param name="arch">Architecture ("amd64", "arm64").</param>
    /// <param name="branch">Branch name ("release").</param>
    /// <param name="version">Game version number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mirror download URL, or null if not available.</returns>
    public async Task<string?> GetMirrorUrlAsync(string os, string arch, string branch, int version, CancellationToken ct = default)
    {
        string fileKey = $"v{version}-{os}-{arch}.pwr";

        // Try live mirror index first
        var files = await GetPlatformFilesAsync(os, branch, ct);
        if (files != null && files.TryGetValue(fileKey, out var element) && element.ValueKind == JsonValueKind.String)
            return element.GetString();

        // Fall back to cached URLs from versions.json
        if (_cachedUrlsByBranch.TryGetValue(branch, out var cached) && cached.TryGetValue(fileKey, out var url))
            return url;

        return null;
    }

    /// <summary>
    /// Gets a mirror URL for a diff patch between two consecutive versions (pre-release branch).
    /// Looks for key <c>v{fromVersion}~{toVersion}-{os}-{arch}.pwr</c>.
    /// </summary>
    /// <param name="os">OS identifier ("windows", "darwin", "linux").</param>
    /// <param name="arch">Architecture ("amd64", "arm64").</param>
    /// <param name="branch">Branch name ("pre-release").</param>
    /// <param name="fromVersion">Source version of the diff.</param>
    /// <param name="toVersion">Target version of the diff.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The mirror download URL, or null if not available.</returns>
    public async Task<string?> GetMirrorDiffUrlAsync(string os, string arch, string branch, int fromVersion, int toVersion, CancellationToken ct = default)
    {
        string fileKey = $"v{fromVersion}~{toVersion}-{os}-{arch}.pwr";

        // Try live mirror index first
        var files = await GetPlatformFilesAsync(os, branch, ct);
        if (files != null && files.TryGetValue(fileKey, out var element) && element.ValueKind == JsonValueKind.String)
            return element.GetString();

        // Fall back to cached URLs from versions.json
        if (_cachedUrlsByBranch.TryGetValue(branch, out var cached) && cached.TryGetValue(fileKey, out var url))
            return url;

        return null;
    }

    /// <summary>
    /// Returns all file URLs for a platform+branch as a flat dictionary (filename → URL).
    /// Used by VersionService to persist mirror URLs in versions.json.
    /// Only includes entries that are actual URL strings (skips nested objects).
    /// </summary>
    public async Task<Dictionary<string, string>> GetAllPlatformUrlsAsync(string os, string arch, string branch, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var files = await GetPlatformFilesAsync(os, branch, ct);
        if (files == null) return result;

        string suffix = $"-{os}-{arch}.pwr";
        foreach (var (key, val) in files)
        {
            if (val.ValueKind == JsonValueKind.String && key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                result[key] = val.GetString()!;
        }

        return result;
    }

    /// <summary>
    /// Injects cached mirror URLs (loaded from versions.json) so that URL lookups
    /// can work even when the live mirror index hasn't been fetched yet.
    /// </summary>
    public void SetCachedUrls(string branch, Dictionary<string, string> urls)
    {
        if (urls.Count > 0)
        {
            _cachedUrlsByBranch[branch] = urls;
            Logger.Info("Mirror", $"Loaded {urls.Count} cached URLs for branch '{branch}' from versions cache");
        }
    }

    /// <summary>
    /// Gets all available target version numbers for a specific platform and branch from the mirror.
    /// For release: parses v{N} keys. For pre-release: parses target version from v{from}~{to} keys.
    /// </summary>
    public async Task<List<int>> GetAvailableVersionsAsync(string os, string arch, string branch, CancellationToken ct = default)
    {
        var files = await GetPlatformFilesAsync(os, branch, ct);
        if (files == null) return new List<int>();

        var versions = new HashSet<int>();
        foreach (var (key, val) in files)
        {
            if (val.ValueKind != JsonValueKind.String) continue;
            if (TryParseVersionFromKey(key, os, arch, out int ver))
                versions.Add(ver);
        }

        var result = versions.ToList();
        result.Sort((a, b) => b.CompareTo(a)); // Descending
        return result;
    }

    /// <summary>
    /// Pre-fetches/refreshes the mirror index. Call this early to warm the cache.
    /// </summary>
    public async Task PreloadAsync(CancellationToken ct = default)
    {
        await FetchIndexAsync(ct);
    }

    /// <summary>
    /// Resolves the file map for a given OS and branch from the mirror index.
    /// Handles the platform key mapping (darwin → mac).
    /// </summary>
    private async Task<Dictionary<string, JsonElement>?> GetPlatformFilesAsync(string os, string branch, CancellationToken ct)
    {
        var index = await GetOrFetchIndexAsync(ct);
        if (index?.Hytale == null) return null;

        string platformKey = os == "darwin" ? "mac" : os;
        if (!index.Hytale.TryGetValue(branch, out var platforms)) return null;
        if (!platforms.TryGetValue(platformKey, out var files)) return null;
        return files;
    }

    #region Index Management

    private async Task<MirrorIndex?> GetOrFetchIndexAsync(CancellationToken ct)
    {
        if (_cachedIndex != null && DateTime.UtcNow - _cachedAt < CacheTtl)
            return _cachedIndex;

        return await FetchIndexAsync(ct);
    }

    private async Task<MirrorIndex?> FetchIndexAsync(CancellationToken ct)
    {
        await _fetchLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_cachedIndex != null && DateTime.UtcNow - _cachedAt < CacheTtl)
                return _cachedIndex;

            Logger.Info("Mirror", $"Fetching mirror index from {MirrorApiUrl}...");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15)); // Don't hang on mirror

            var response = await _httpClient.GetAsync(MirrorApiUrl, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("Mirror", $"Mirror API returned {response.StatusCode}");
                return _cachedIndex; // Return stale cache if available
            }

            var json = await response.Content.ReadAsStringAsync(cts.Token);
            var index = JsonSerializer.Deserialize<MirrorIndex>(json);

            if (index?.Hytale != null)
            {
                _cachedIndex = index;
                _cachedAt = DateTime.UtcNow;

                int totalFiles = CountFiles(index);
                Logger.Success("Mirror", $"Mirror index loaded: {totalFiles} files across {index.Hytale.Count} branches");
            }
            else
            {
                Logger.Warning("Mirror", "Mirror API returned invalid/empty data");
            }

            return _cachedIndex;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Logger.Warning("Mirror", "Mirror API request timed out");
            return _cachedIndex; // Return stale cache
        }
        catch (Exception ex)
        {
            Logger.Warning("Mirror", $"Failed to fetch mirror index: {ex.Message}");
            return _cachedIndex; // Return stale cache
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    #endregion

    #region Helpers

    private static bool TryParseVersionFromKey(string key, string os, string arch, out int version)
    {
        version = 0;
        // Expected suffix: "-{os}-{arch}.pwr"
        string expectedSuffix = $"-{os}-{arch}.pwr";
        if (!key.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase)) return false;
        if (!key.StartsWith("v", StringComparison.OrdinalIgnoreCase)) return false;

        // Extract part between "v" and the suffix
        string numPart = key[1..^expectedSuffix.Length];

        // Handle combined format: "v{from}~{to}" (pre-release)
        int tildeIdx = numPart.IndexOf('~');
        if (tildeIdx >= 0)
        {
            // Target version is the number after ~
            string targetPart = numPart[(tildeIdx + 1)..];
            return int.TryParse(targetPart, out version);
        }

        // Simple format: "v{N}" (release)
        return int.TryParse(numPart, out version);
    }

    private static int CountFiles(MirrorIndex index)
    {
        int count = 0;
        if (index.Hytale == null) return 0;
        foreach (var branch in index.Hytale.Values)
            foreach (var platform in branch.Values)
                count += platform.Count;
        return count;
    }

    #endregion
}

#region Models

/// <summary>
/// Root of the mirror API response.
/// </summary>
internal class MirrorIndex
{
    /// <summary>
    /// Map of branch -> platform -> filename -> value.
    /// Branch keys: "release", "pre-release"
    /// Platform keys: "windows", "mac", "linux"
    /// Filename keys: "v{N}-{os}-{arch}.pwr" -> URL string
    /// Some keys (e.g. "patch") may hold nested objects instead of strings.
    /// </summary>
    [JsonPropertyName("hytale")]
    public Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>? Hytale { get; set; }
}

#endregion
