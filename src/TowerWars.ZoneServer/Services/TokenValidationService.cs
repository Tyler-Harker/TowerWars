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
        return Task.FromResult<(Guid UserId, Guid CharacterId)?>(
            (Guid.NewGuid(), Guid.NewGuid()));
    }
}
