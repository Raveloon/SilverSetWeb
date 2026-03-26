using System.Text.Json.Serialization;

namespace SilverSet.Services;

public class TurnstileResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class TurnstileService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public TurnstileService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<bool> VerifyAsync(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;

        var secretKey = _config["CloudflareTurnstile:SecretKey"];
        if (string.IsNullOrEmpty(secretKey)) return false;

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("secret", secretKey),
            new KeyValuePair<string, string>("response", token)
        });

        try
        {
            var response = await _httpClient.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", content);
            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>();
            return result?.Success ?? false;
        }
        catch
        {
            return false;
        }
    }
}