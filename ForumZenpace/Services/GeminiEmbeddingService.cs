using System.Text;
using System.Text.Json;

namespace ForumZenpace.Services
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; } = string.Empty;
    }

    public class GeminiEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GeminiEmbeddingService> _logger;
        private const string ApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/text-embedding-004:embedContent";
        private const string ApiKeyEnvironmentVariable = "GeminiSettings__ApiKey";

        public GeminiEmbeddingService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiEmbeddingService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["GeminiSettings:ApiKey"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning(
                    "Gemini API key is not configured. Set the '{EnvironmentVariable}' environment variable.",
                    ApiKeyEnvironmentVariable);
            }
        }

        /// <summary>
        /// Calls Gemini text-embedding-004 to convert text into a 768-dim float vector.
        /// Returns null on failure (network error, invalid key, etc.).
        /// </summary>
        public async Task<float[]?> GetEmbeddingAsync(string textContent)
        {
            if (string.IsNullOrWhiteSpace(textContent))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return null;
            }

            try
            {
                // Truncate to ~8000 chars to stay within model limits
                if (textContent.Length > 8000)
                {
                    textContent = textContent[..8000];
                }

                var requestBody = new
                {
                    model = "models/text-embedding-004",
                    content = new
                    {
                        parts = new[] { new { text = textContent } }
                    }
                };

                var jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{ApiUrl}?key={_apiKey}", content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();

                using var document = JsonDocument.Parse(responseString);
                var vectorArray = document.RootElement
                    .GetProperty("embedding")
                    .GetProperty("values")
                    .EnumerateArray()
                    .Select(x => x.GetSingle())
                    .ToArray();

                return vectorArray;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get embedding from Gemini for text (length={Length})", textContent.Length);
                return null;
            }
        }
    }
}
