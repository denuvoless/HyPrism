using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HyPrism.Services.Core;

namespace HyPrism.Services.User;

/// <summary>
/// Handles Hytale OAuth 2.0 Authorization Code flow with PKCE.
/// Used to authenticate official Hytale accounts and obtain session tokens
/// for launching the game in authenticated mode.
/// </summary>
public class HytaleAuthService
{
    private const string AuthUrl = "https://oauth.accounts.hytale.com/oauth2/auth";
    private const string TokenUrl = "https://oauth.accounts.hytale.com/oauth2/token";
    private const string LauncherDataUrl = "https://account-data.hytale.com/my-account/get-launcher-data";
    private const string SessionUrl = "https://sessions.hytale.com/game-session/new";
    private const string ClientId = "hytale-launcher";
    private const string RedirectUri = "https://accounts.hytale.com/consent/client";
    private const string Scopes = "openid offline auth:launcher";
    
    private readonly HttpClient _httpClient;
    private readonly string _appDir;
    private readonly IBrowserService _browserService;
    
    private string? _pendingCodeVerifier;
    private string? _pendingState;
    private TaskCompletionSource<string>? _authCodeTcs;
    private System.Net.HttpListener? _callbackListener;
    
    /// <summary>
    /// The current auth session, or null if not logged in.
    /// </summary>
    public HytaleAuthSession? CurrentSession { get; private set; }

    public HytaleAuthService(HttpClient httpClient, string appDir, IBrowserService browserService)
    {
        _httpClient = httpClient;
        _appDir = appDir;
        _browserService = browserService;

        // Try to restore session from disk
        LoadSession();
    }

    /// <summary>
    /// Starts the OAuth login flow: opens browser, waits for callback, exchanges code for tokens,
    /// fetches profile data.
    /// </summary>
    /// <returns>The authenticated session, or null on failure/cancellation.</returns>
    public async Task<HytaleAuthSession?> LoginAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Generate PKCE
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);
            _pendingCodeVerifier = codeVerifier;
            
            // Step 2: Start local HTTP listener for callback
            var listener = new System.Net.HttpListener();
            var port = FindAvailablePort();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            _callbackListener = listener;
            
            _authCodeTcs = new TaskCompletionSource<string>();
            
            // Step 3: Build auth URL
            var state = GenerateState(port);
            _pendingState = state;
            
            var authUrl = $"{AuthUrl}?access_type=offline" +
                          $"&client_id={Uri.EscapeDataString(ClientId)}" +
                          $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
                          $"&code_challenge_method=S256" +
                          $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                          $"&response_type=code" +
                          $"&scope={Uri.EscapeDataString(Scopes)}" +
                          $"&state={Uri.EscapeDataString(state)}";
            
            Logger.Info("HytaleAuth", "Opening browser for Hytale login...");
            _browserService.OpenURL(authUrl);
            
            // Step 4: Listen for callback (async)
            _ = ListenForCallbackAsync(listener, cancellationToken);
            
            // Wait for auth code with generous timeout (user may need to sign in via Google/other OAuth providers)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            linkedCts.Token.Register(() => _authCodeTcs.TrySetCanceled());
            
            string authCode;
            try
            {
                authCode = await _authCodeTcs.Task;
            }
            catch (OperationCanceledException)
            {
                Logger.Warning("HytaleAuth", "Login cancelled or timed out");
                return null;
            }
            finally
            {
                StopListener();
            }
            
            // Step 5: Exchange code for tokens
            Logger.Info("HytaleAuth", "Exchanging auth code for tokens...");
            var tokenResponse = await ExchangeCodeForTokensAsync(authCode);
            if (tokenResponse == null)
            {
                Logger.Error("HytaleAuth", "Failed to exchange auth code for tokens");
                return null;
            }

            // Step 6: Fetch profile data
            Logger.Info("HytaleAuth", "Fetching Hytale profile...");
            var profile = await FetchProfileAsync(tokenResponse.AccessToken);
            // FetchProfileAsync now throws HytaleNoProfileException or HytaleAuthException on failure
            
            // Step 7: Create game session
            Logger.Info("HytaleAuth", "Creating game session...");
            var gameSession = await CreateGameSessionAsync(tokenResponse.AccessToken, profile!.Uuid);

            var session = new HytaleAuthSession
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                Username = profile!.Username,
                UUID = profile.Uuid,
                AccountOwnerId = profile.Owner,
                SessionToken = gameSession?.SessionToken ?? "",
                IdentityToken = gameSession?.IdentityToken ?? ""
            };
            
            CurrentSession = session;
            SaveSession();
            
            Logger.Success("HytaleAuth", $"Logged in as {session.Username} ({session.UUID})");
            return session;
        }
        catch (Exception ex)
        {
            Logger.Error("HytaleAuth", $"Login failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Logs out: clears session from memory and disk.
    /// </summary>
    public void Logout()
    {
        CurrentSession = null;
        var path = GetSessionFilePath();
        if (File.Exists(path))
        {
            try { File.Delete(path); }
            catch (Exception ex) { Logger.Warning("HytaleAuth", $"Failed to delete session file: {ex.Message}"); }
        }
        Logger.Info("HytaleAuth", "Logged out");
    }
    
    /// <summary>
    /// Returns the current auth status (logged in, username, uuid).
    /// </summary>
    public object GetAuthStatus()
    {
        if (CurrentSession != null)
        {
            return new
            {
                loggedIn = true,
                username = CurrentSession.Username,
                uuid = CurrentSession.UUID
            };
        }
        return new { loggedIn = false, username = (string?)null, uuid = (string?)null };
    }

    /// <summary>
    /// Refreshes the access token if expired. Returns a valid session or null.
    /// </summary>
    public async Task<HytaleAuthSession?> GetValidSessionAsync()
    {
        if (CurrentSession == null) return null;
        
        if (CurrentSession.ExpiresAt <= DateTime.UtcNow.AddMinutes(1))
        {
            Logger.Info("HytaleAuth", "Access token expired, refreshing...");
            var refreshed = await RefreshTokenAsync();
            if (!refreshed)
            {
                Logger.Warning("HytaleAuth", "Token refresh failed, session invalid");
                Logout();
                return null;
            }
        }
        
        return CurrentSession;
    }

    /// <summary>
    /// Ensures a valid session with fresh game session tokens for launching.
    /// Always creates a new game session even if the access token is still valid,
    /// because game session tokens (identityToken/sessionToken) expire independently
    /// and must be refreshed before each launch.
    /// </summary>
    public async Task<HytaleAuthSession?> EnsureFreshSessionForLaunchAsync()
    {
        // Step 1: Ensure we have a valid access token
        var session = await GetValidSessionAsync();
        if (session == null) return null;

        // Step 2: Always create a fresh game session before launch
        Logger.Info("HytaleAuth", "Creating fresh game session for launch...");
        try
        {
            var gameSession = await CreateGameSessionAsync(session.AccessToken, session.UUID);
            if (gameSession != null)
            {
                session.SessionToken = gameSession.SessionToken;
                session.IdentityToken = gameSession.IdentityToken;
                SaveSession();
                Logger.Success("HytaleAuth", "Fresh game session tokens obtained");
            }
            else
            {
                Logger.Warning("HytaleAuth", "Failed to create fresh game session — will use cached tokens");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("HytaleAuth", $"Error creating fresh game session: {ex.Message}");
            // Fall through — cached tokens may still work
        }

        return session;
    }

    #region OAuth Helpers

    private async Task ListenForCallbackAsync(System.Net.HttpListener listener, CancellationToken ct)
    {
        try
        {
            while (listener.IsListening && !ct.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;
                
                var query = request.QueryString;
                var code = query["code"];
                var error = query["error"];
                
                string responseHtml;
                if (!string.IsNullOrEmpty(code))
                {
                    responseHtml = "<html><body><h1>Authorization successful!</h1><p>You can close this window and return to HyPrism.</p></body></html>";
                    _authCodeTcs?.TrySetResult(code);
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    responseHtml = $"<html><body><h1>Authorization failed</h1><p>{error}</p></body></html>";
                    _authCodeTcs?.TrySetException(new Exception($"OAuth error: {error}"));
                }
                else
                {
                    responseHtml = "<html><body><h1>Waiting for authorization...</h1></body></html>";
                    continue;
                }

                var buffer = Encoding.UTF8.GetBytes(responseHtml);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, ct);
                response.Close();
                break;
            }
        }
        catch (ObjectDisposedException) { /* listener stopped */ }
        catch (Exception ex)
        {
            Logger.Warning("HytaleAuth", $"Callback listener error: {ex.Message}");
            _authCodeTcs?.TrySetException(ex);
        }
    }
    
    private void StopListener()
    {
        try
        {
            _callbackListener?.Stop();
            _callbackListener?.Close();
        }
        catch { /* ignore */ }
        _callbackListener = null;
    }

    private async Task<TokenResponse?> ExchangeCodeForTokensAsync(string code)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = ClientId,
            ["code_verifier"] = _pendingCodeVerifier ?? ""
        });
        
        try
        {
            var response = await _httpClient.PostAsync(TokenUrl, content);
            var json = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                Logger.Error("HytaleAuth", $"Token exchange failed ({response.StatusCode}): {json}");
                return null;
            }
            
            return JsonSerializer.Deserialize<TokenResponse>(json);
        }
        catch (Exception ex)
        {
            Logger.Error("HytaleAuth", $"Token exchange exception: {ex.Message}");
            return null;
        }
    }

    private async Task<bool> RefreshTokenAsync()
    {
        if (CurrentSession?.RefreshToken == null) return false;
        
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = CurrentSession.RefreshToken,
            ["client_id"] = ClientId
        });
        
        try
        {
            var response = await _httpClient.PostAsync(TokenUrl, content);
            var json = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                Logger.Error("HytaleAuth", $"Token refresh failed ({response.StatusCode}): {json}");
                return false;
            }
            
            var tokenResp = JsonSerializer.Deserialize<TokenResponse>(json);
            if (tokenResp == null) return false;
            
            CurrentSession.AccessToken = tokenResp.AccessToken;
            if (!string.IsNullOrEmpty(tokenResp.RefreshToken))
                CurrentSession.RefreshToken = tokenResp.RefreshToken;
            CurrentSession.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResp.ExpiresIn);
            
            SaveSession();
            Logger.Info("HytaleAuth", "Token refreshed successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("HytaleAuth", $"Token refresh exception: {ex.Message}");
            return false;
        }
    }
    
    private async Task<HytaleProfile?> FetchProfileAsync(string accessToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, LauncherDataUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                Logger.Error("HytaleAuth", $"Profile fetch failed ({response.StatusCode}): {json}");
                throw new HytaleAuthException("profile_fetch_failed", $"HTTP {response.StatusCode}");
            }
            
            var profilesResp = JsonSerializer.Deserialize<ProfilesResponse>(json);
            if (profilesResp?.Profiles == null || profilesResp.Profiles.Count == 0)
            {
                Logger.Warning("HytaleAuth", "No profiles found in Hytale account");
                throw new HytaleNoProfileException("No game profiles found in this Hytale account");
            }
            
            var profile = profilesResp.Profiles[0];
            return new HytaleProfile
            {
                Username = profile.Username,
                Uuid = profile.Uuid,
                Owner = profilesResp.Owner
            };
        }
        catch (HytaleNoProfileException) { throw; }
        catch (HytaleAuthException) { throw; }
        catch (Exception ex)
        {
            Logger.Error("HytaleAuth", $"Profile fetch exception: {ex.Message}");
            throw new HytaleAuthException("profile_fetch_error", ex.Message);
        }
    }

    private async Task<GameSessionResponse?> CreateGameSessionAsync(string accessToken, string uuid)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, SessionUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { uuid }),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("HytaleAuth", $"Game session creation failed ({response.StatusCode}): {json}");
                return null;
            }
            
            return JsonSerializer.Deserialize<GameSessionResponse>(json);
        }
        catch (Exception ex)
        {
            Logger.Warning("HytaleAuth", $"Game session creation exception: {ex.Message}");
            return null;
        }
    }

    #endregion

    #region PKCE & State Helpers

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }
    
    private static string GenerateState(int port)
    {
        var stateObj = new { state = GenerateRandomString(26), port = port.ToString() };
        var json = JsonSerializer.Serialize(stateObj);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
    
    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[bytes[i] % chars.Length];
        return new string(result);
    }
    
    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
    
    private static int FindAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    #endregion

    #region Session Persistence

    private string GetSessionFilePath() => Path.Combine(_appDir, "hytale_session.json");

    private void SaveSession()
    {
        if (CurrentSession == null) return;
        try
        {
            var json = JsonSerializer.Serialize(CurrentSession, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetSessionFilePath(), json);
        }
        catch (Exception ex)
        {
            Logger.Warning("HytaleAuth", $"Failed to save session: {ex.Message}");
        }
    }

    private void LoadSession()
    {
        var path = GetSessionFilePath();
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            CurrentSession = JsonSerializer.Deserialize<HytaleAuthSession>(json);
            if (CurrentSession != null)
                Logger.Info("HytaleAuth", $"Restored session for {CurrentSession.Username}");
        }
        catch (Exception ex)
        {
            Logger.Warning("HytaleAuth", $"Failed to load session: {ex.Message}");
            CurrentSession = null;
        }
    }

    #endregion
}

#region Models

/// <summary>
/// Persisted auth session for Hytale account.
/// </summary>
public class HytaleAuthSession
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";
    
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";
    
    [JsonPropertyName("expires_at")]
    public DateTime ExpiresAt { get; set; }
    
    [JsonPropertyName("session_token")]
    public string SessionToken { get; set; } = "";
    
    [JsonPropertyName("identity_token")]
    public string IdentityToken { get; set; } = "";
    
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
    
    [JsonPropertyName("uuid")]
    public string UUID { get; set; } = "";
    
    [JsonPropertyName("account_owner_id")]
    public string AccountOwnerId { get; set; } = "";
}

internal class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";
    
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

internal class ProfilesResponse
{
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "";
    
    [JsonPropertyName("profiles")]
    public List<ProfileEntry> Profiles { get; set; } = new();
}

internal class ProfileEntry
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";
    
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";
}

internal class HytaleProfile
{
    public string Username { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string Owner { get; set; } = "";
}

internal class GameSessionResponse
{
    [JsonPropertyName("sessionToken")]
    public string SessionToken { get; set; } = "";
    
    [JsonPropertyName("identityToken")]
    public string IdentityToken { get; set; } = "";
    
    [JsonPropertyName("expiresAt")]
    public string ExpiresAt { get; set; } = "";
}

/// <summary>
/// Thrown when no game profiles are found in the Hytale account.
/// This is a non-critical warning — the user simply has no game profile yet.
/// </summary>
public class HytaleNoProfileException : Exception
{
    public HytaleNoProfileException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a general auth error occurs (network, HTTP status, etc.).
/// </summary>
public class HytaleAuthException : Exception
{
    public string ErrorType { get; }
    public HytaleAuthException(string errorType, string message) : base(message)
    {
        ErrorType = errorType;
    }
}

#endregion
