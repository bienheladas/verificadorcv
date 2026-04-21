using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;
using System.Net;
using System.Text.Json;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public string? QrImageBase64 { get; set; }
    public string? PresentationUri { get; set; }
    public string? PresentationUriDecoded { get; set; }
    public string? SessionId { get; set; }
    public string? Profile { get; set; }
    public VerificationResultDto? Result { get; set; }

    public IndexModel(IHttpClientFactory factory, IConfiguration config)
    {
        _httpClientFactory = factory;
        _config = config;
    }

    public async Task OnGetAsync(string? sessionId, string? profile)
    {
        if (string.IsNullOrEmpty(sessionId)) return;

        SessionId = sessionId;
        Profile = profile;

        var baseUrl = _config["VerifierApiBaseUrl"];
        var client = _httpClientFactory.CreateClient();

        // Obtener qr_uri desde la API (evita regenerarlo localmente)
        var sessionRes = await client.GetAsync($"{baseUrl}/verifier/sessions/{sessionId}");
        if (sessionRes.IsSuccessStatusCode)
        {
            var sessionJson = await sessionRes.Content.ReadAsStringAsync();
            using var sessionDoc = JsonDocument.Parse(sessionJson);
            var qrUri = sessionDoc.RootElement.GetProperty("qr_uri").GetString();
            if (!string.IsNullOrEmpty(qrUri))
            {
                PresentationUri = qrUri;
                PresentationUriDecoded = WebUtility.UrlDecode(qrUri);

                var qrGen = new QRCodeGenerator();
                var qrData = qrGen.CreateQrCode(qrUri, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrData);
                QrImageBase64 = Convert.ToBase64String(qrCode.GetGraphic(6));
            }
        }

        // Consultar resultado una sola vez
        var res = await client.GetAsync($"{baseUrl}/verifier/result/{sessionId}");
        if (res.IsSuccessStatusCode)
        {
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("completed", out var completed) && completed.GetBoolean())
                Result = JsonSerializer.Deserialize<VerificationResultDto>(json);
        }
    }

    public async Task<IActionResult> OnPostAsync(string profile)
    {
        var baseUrl = _config["VerifierApiBaseUrl"];
        var client = _httpClientFactory.CreateClient();

        var response = await client.PostAsync($"{baseUrl}/verifier/sessions?profile={profile}", null);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var sessionId = doc.RootElement.GetProperty("session_id").GetString();
        return RedirectToPage(new { sessionId, profile });
    }

    public class VerificationResultDto
    {
        public bool Valid { get; set; }
        public string Reason { get; set; } = "";
        public Dictionary<string, object> Summary { get; set; } = new();
        public List<VerificationCheckDto> Checks { get; set; } = new();
    }

    public class VerificationCheckDto
    {
        public string Name { get; set; } = "";
        public bool Passed { get; set; }
        public string Message { get; set; } = "";
    }
}
