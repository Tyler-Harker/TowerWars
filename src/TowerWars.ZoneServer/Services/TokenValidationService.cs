using System.Net.Http.Json;

namespace TowerWars.ZoneServer.Services;

public interface ITokenValidationService
{
    Task<(Guid UserId, Guid CharacterId)?> ValidateAsync(string token);
}

public sealed class TokenValidationService : ITokenValidationService
{
    private readonly HttpClient _httpClient;
    private readonly string _authServiceUrl;

    public TokenValidationService(HttpClient httpClient, string authServiceUrl)
    {
        _httpClient = httpClient;
        _authServiceUrl = authServiceUrl.TrimEnd('/');
    }

    public async Task<(Guid UserId, Guid CharacterId)?> ValidateAsync(string token)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_authServiceUrl}/internal/validate-connection-token?token={Uri.EscapeDataString(token)}",
                null);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadFromJsonAsync<ValidationResponse>();
            if (content == null)
                return null;

            return (content.UserId, content.CharacterId);
        }
        catch
        {
            return null;
        }
    }

    private sealed record ValidationResponse(Guid UserId, Guid CharacterId);
}

public sealed class LocalTokenValidationService : ITokenValidationService
{
    public Task<(Guid UserId, Guid CharacterId)?> ValidateAsync(string token)
    {
        // Use a deterministic UserId based on the token so the same token
        // always maps to the same player in the database
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token));
        var userId = new Guid(hash.AsSpan(0, 16));
        var characterId = new Guid(hash.AsSpan(16, 16));
        return Task.FromResult<(Guid UserId, Guid CharacterId)?>((userId, characterId));
    }
}
