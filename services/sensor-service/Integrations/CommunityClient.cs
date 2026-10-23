using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SensorService.Integrations;

public sealed class CommunityClient(
    HttpClient httpClient)
{
    public sealed record CommunityResponse(
        Guid Id,
        string Name,
        string Location,
        bool IsActive);

    public async Task<CommunityResponse?> GetByIdAsync(
        Guid id,
        string? authorization,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request =
            new(
                HttpMethod.Get,
                $"/api/communities/{id}");

        if (!string.IsNullOrWhiteSpace(
                authorization))
        {
            request.Headers.Authorization =
                AuthenticationHeaderValue.Parse(
                    authorization);
        }

        using HttpResponseMessage response =
            await httpClient.SendAsync(
                request,
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<CommunityResponse>(
                cancellationToken:
                    cancellationToken);
    }
}
