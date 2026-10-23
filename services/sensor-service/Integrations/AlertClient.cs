using System.Net.Http.Headers;
using System.Net.Http.Json;
using SensorService.Domain;

namespace SensorService.Integrations;

public sealed class AlertClient(
    HttpClient httpClient)
{
    private sealed record EvaluateReadingRequest(
        Guid ReadingId,
        Guid SensorId,
        Guid CommunityId,
        ClimateVariable Variable,
        decimal Value,
        DateTimeOffset MeasuredAt,
        DateTimeOffset ReceivedAt,
        bool SensorActive);

    public async Task EvaluateAsync(
        SensorReading reading,
        string? authorization,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request =
            new(
                HttpMethod.Post,
                "/api/alerts/evaluate");

        if (!string.IsNullOrWhiteSpace(
                authorization))
        {
            request.Headers.Authorization =
                AuthenticationHeaderValue.Parse(
                    authorization);
        }

        request.Content =
            JsonContent.Create(
                new EvaluateReadingRequest(
                    reading.Id,
                    reading.SensorId,
                    reading.Sensor.CommunityId,
                    reading.Variable,
                    reading.Value,
                    reading.MeasuredAt,
                    reading.ReceivedAt,
                    reading.SensorStatusAtMeasurement
                        == SensorStatus.Active));

        using HttpResponseMessage response =
            await httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
