using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HttpClient = System.Net.Http.HttpClient;
using HttpMethod = System.Net.Http.HttpMethod;
using HttpRequestMessage = System.Net.Http.HttpRequestMessage;
using FormUrlEncodedContent = System.Net.Http.FormUrlEncodedContent;

namespace TowerWars.Client.Auth;

public partial class OidcAuthManager : Node
{
    private const string Authority = "https://identity.harker.dev/tenant/harker";
    private const string ClientId = "tower-wars-godot";
    private const string RedirectUri = "http://localhost:8080/callback";
    private const string Scope = "openid profile email offline_access";

    private HttpServer _callbackServer = null!;
    private string _codeVerifier = null!;
    private string _state = null!;
    private TaskCompletionSource<string> _authCodeReceived = null!;
    private OidcDiscoveryDocument? _discoveryDocument;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? IdToken { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    [Signal]
    public delegate void AuthenticationCompletedEventHandler(bool success);

    [Signal]
    public delegate void AuthenticationFailedEventHandler(string error);

    public override void _Ready()
    {
        _callbackServer = new HttpServer();
        _callbackServer.ListenFinished += OnCallbackReceived;
        AddChild(_callbackServer);
    }

    private async Task<OidcDiscoveryDocument?> GetDiscoveryDocumentAsync()
    {
        if (_discoveryDocument != null)
            return _discoveryDocument;

        try
        {
            using var httpClient = new HttpClient();
            var discoveryUrl = $"{Authority}/.well-known/openid-configuration";
            GD.Print($"Fetching OIDC discovery document from: {discoveryUrl}");

            var response = await httpClient.GetStringAsync(discoveryUrl);
            _discoveryDocument = JsonSerializer.Deserialize<OidcDiscoveryDocument>(response);

            GD.Print($"Authorization endpoint: {_discoveryDocument?.AuthorizationEndpoint}");
            GD.Print($"Token endpoint: {_discoveryDocument?.TokenEndpoint}");

            return _discoveryDocument;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to fetch discovery document: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> LoginAsync()
    {
        try
        {
            // Fetch discovery document
            var discovery = await GetDiscoveryDocumentAsync();
            if (discovery == null)
            {
                EmitSignal(SignalName.AuthenticationFailed, "Failed to fetch OIDC configuration");
                return false;
            }

            // Generate PKCE parameters
            _codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(_codeVerifier);
            _state = GenerateState();

            // Build authorization URL using discovered endpoint
            var authUrl = $"{discovery.AuthorizationEndpoint}?" +
                         $"client_id={ClientId}&" +
                         $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
                         $"response_type=code&" +
                         $"scope={Uri.EscapeDataString(Scope)}&" +
                         $"state={_state}&" +
                         $"code_challenge={codeChallenge}&" +
                         $"code_challenge_method=S256";

            GD.Print($"Opening browser for authentication: {authUrl}");

            // Start local callback server
            _authCodeReceived = new TaskCompletionSource<string>();
            _callbackServer.Listen(8080);

            // Open browser
            OS.ShellOpen(authUrl);

            // Wait for callback
            var authCode = await _authCodeReceived.Task;

            // Exchange code for tokens
            var success = await ExchangeCodeForTokensAsync(authCode);

            EmitSignal(SignalName.AuthenticationCompleted, success);
            return success;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Authentication failed: {ex.Message}");
            EmitSignal(SignalName.AuthenticationFailed, ex.Message);
            return false;
        }
        finally
        {
            _callbackServer.Stop();
        }
    }

    private void OnCallbackReceived(Godot.Collections.Dictionary<string, string> parameters)
    {
        if (parameters.TryGetValue("code", out var code) &&
            parameters.TryGetValue("state", out var state))
        {
            if (state == _state)
            {
                _authCodeReceived?.TrySetResult(code);
            }
            else
            {
                GD.PrintErr("State mismatch in OAuth callback");
                _authCodeReceived?.TrySetException(new Exception("State mismatch"));
            }
        }
        else if (parameters.TryGetValue("error", out var error))
        {
            var errorDescription = parameters.TryGetValue("error_description", out var desc) ? desc : error;
            GD.PrintErr($"OAuth error: {errorDescription}");
            _authCodeReceived?.TrySetException(new Exception(errorDescription));
        }
    }

    private async Task<bool> ExchangeCodeForTokensAsync(string code)
    {
        try
        {
            var discovery = await GetDiscoveryDocumentAsync();
            if (discovery?.TokenEndpoint == null)
            {
                GD.PrintErr("Token endpoint not available");
                return false;
            }

            using var httpClient = new HttpClient();

            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["client_id"] = ClientId,
                ["code_verifier"] = _codeVerifier
            };

            var request = new HttpRequestMessage(HttpMethod.Post, discovery.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(requestBody)
            };

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                GD.PrintErr($"Token exchange failed: {responseContent}");
                return false;
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);

            AccessToken = tokenResponse?.AccessToken;
            RefreshToken = tokenResponse?.RefreshToken;
            IdToken = tokenResponse?.IdToken;

            GD.Print("Authentication successful!");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Token exchange error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken))
            return false;

        try
        {
            var discovery = await GetDiscoveryDocumentAsync();
            if (discovery?.TokenEndpoint == null)
                return false;

            using var httpClient = new HttpClient();

            var requestBody = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = RefreshToken,
                ["client_id"] = ClientId
            };

            var request = new HttpRequestMessage(HttpMethod.Post, discovery.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(requestBody)
            };

            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                GD.PrintErr($"Token refresh failed: {responseContent}");
                return false;
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);

            AccessToken = tokenResponse?.AccessToken;
            if (!string.IsNullOrEmpty(tokenResponse?.RefreshToken))
                RefreshToken = tokenResponse.RefreshToken;
            IdToken = tokenResponse?.IdToken;

            GD.Print("Token refreshed successfully!");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Token refresh error: {ex.Message}");
            return false;
        }
    }

    public void Logout()
    {
        AccessToken = null;
        RefreshToken = null;
        IdToken = null;
        _discoveryDocument = null;
        GD.Print("Logged out");
    }

    private string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private string GenerateCodeChallenge(string verifier)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private string GenerateState()
    {
        var bytes = new byte[16];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private class OidcDiscoveryDocument
    {
        [System.Text.Json.Serialization.JsonPropertyName("authorization_endpoint")]
        public string? AuthorizationEndpoint { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token_endpoint")]
        public string? TokenEndpoint { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("userinfo_endpoint")]
        public string? UserInfoEndpoint { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("end_session_endpoint")]
        public string? EndSessionEndpoint { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("jwks_uri")]
        public string? JwksUri { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("issuer")]
        public string? Issuer { get; set; }
    }

    private class TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = null!;

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string TokenType { get; set; } = null!;
    }
}
