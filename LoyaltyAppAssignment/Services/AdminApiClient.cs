using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LoyaltyAppAssignment.Services;

public sealed class AdminApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class AdminApiClient(HttpClient client)
{
    private static readonly HashSet<string> Secrets = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "successToken", "refreshToken", "otp", "otpCreated", "deviceId", "tokenMinutes"
    };

    public async Task<JsonNode?> SendAsync(string path, HttpMethod? method = null,
        HttpContent? content = null, string? token = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path) { Content = content };
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            JsonNode? data;
            try { data = JsonNode.Parse(text); }
            catch (JsonException) { data = JsonValue.Create(text); }
            if (!response.IsSuccessStatusCode)
            {
                var message = ErrorMessage(data);
                throw new AdminApiException((int)response.StatusCode, message);
            }
            return data;
        }
        catch (HttpRequestException)
        {
            throw new AdminApiException(503, "The loyalty API is unavailable. Please try again.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AdminApiException(504, "The loyalty API took too long to respond. Please try again.");
        }
    }

    public static string ErrorMessage(JsonNode? data)
    {
        if (data is JsonObject obj)
        {
            if (obj["errors"] is JsonObject errors)
                return string.Join(" ", errors.SelectMany(p => p.Value is JsonArray a
                    ? a.Select(v => v?.ToString() ?? "") : new[] { p.Value?.ToString() ?? "" }));
            return obj["message"]?.ToString() ?? obj["detail"]?.ToString()
                ?? obj["title"]?.ToString() ?? "The API could not complete this request.";
        }
        var message = data?.ToString();
        // Never forward an upstream HTML error page or stack trace to the browser.
        return string.IsNullOrWhiteSpace(message) || message.Contains('<') || message.Length > 700
            ? "The API could not complete this request." : message;
    }

    public static JsonNode? ForBrowser(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var clean = new JsonObject();
            foreach (var (key, value) in obj)
                if (!Secrets.Contains(key)) clean[JsonNamingPolicy.CamelCase.ConvertName(key)] = ForBrowser(value);
            return clean;
        }
        if (node is JsonArray array) return new JsonArray(array.Select(ForBrowser).ToArray());
        return node?.DeepClone();
    }
}
