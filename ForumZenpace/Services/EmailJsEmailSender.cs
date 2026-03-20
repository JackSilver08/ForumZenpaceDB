using System.Net.Http.Json;
using ForumZenpace.Models;
using Microsoft.Extensions.Options;

namespace ForumZenpace.Services
{
    public class EmailJsEmailSender : IEmailSender
    {
        private readonly HttpClient _httpClient;
        private readonly EmailJsSettings _settings;

        public EmailJsEmailSender(HttpClient httpClient, IOptions<EmailJsSettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;
        }

        public async Task SendVerificationOtpAsync(
            string toEmail,
            string recipientName,
            string otpCode,
            DateTime expiresAt,
            CancellationToken cancellationToken = default)
        {
            var missingSettings = new List<string>();
            if (string.IsNullOrWhiteSpace(_settings.ServiceId))
            {
                missingSettings.Add(nameof(_settings.ServiceId));
            }

            if (string.IsNullOrWhiteSpace(_settings.TemplateId))
            {
                missingSettings.Add(nameof(_settings.TemplateId));
            }

            if (string.IsNullOrWhiteSpace(_settings.PublicKey))
            {
                missingSettings.Add(nameof(_settings.PublicKey));
            }

            if (missingSettings.Count > 0)
            {
                throw new InvalidOperationException($"EmailJsSettings chua duoc cau hinh day du: {string.Join(", ", missingSettings)}.");
            }

            var endpoint = $"{_settings.ApiBaseUrl.TrimEnd('/')}/email/send";
            var payload = new Dictionary<string, object>
            {
                ["service_id"] = _settings.ServiceId,
                ["template_id"] = _settings.TemplateId,
                ["user_id"] = _settings.PublicKey,
                ["template_params"] = new Dictionary<string, string>
                {
                    ["email"] = toEmail,
                    ["to_name"] = recipientName,
                    ["passcode"] = otpCode,
                    ["time"] = expiresAt.ToLocalTime().ToString("HH:mm dd/MM/yyyy"),
                    ["from_name"] = _settings.FromName,
                    ["company_name"] = _settings.CompanyName,
                    ["app_name"] = _settings.CompanyName
                }
            };

            if (!string.IsNullOrWhiteSpace(_settings.PrivateKey))
            {
                payload["accessToken"] = _settings.PrivateKey;
            }

            using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"EmailJS gui that bai: {(int)response.StatusCode} {body}");
        }
    }
}
