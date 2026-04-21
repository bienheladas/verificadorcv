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

        // Regenerar QR (mismo URI → mismo QR)
        var requestUri = $"{baseUrl}/verifier/request/{sessionId}";
        var qrUri = BuildQrUri(baseUrl!, sessionId, requestUri);
        PresentationUri = qrUri;
        PresentationUriDecoded = WebUtility.UrlDecode(qrUri);

        var qrGen = new QRCodeGenerator();
        var qrData = qrGen.CreateQrCode(qrUri, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        QrImageBase64 = Convert.ToBase64String(qrCode.GetGraphic(6));

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

    private string BuildQrUri(string baseUrl, string sessionId, string requestUri)
    {
        string Encode(string v) => Uri.EscapeDataString(v);
        var callbackUrl = $"{baseUrl.TrimEnd('/')}/verifier/callback/{sessionId}";
        var clientMetadata = "{\"vp_formats\":{\"ldp_vc\":{\"proof_type\":[\"JsonWebSignature2020\"]}}}";
        return $"openid4vp://authorize?" +
               $"client_id={Encode(callbackUrl)}" +
               $"&client_id_scheme=redirect_uri" +
               $"&client_metadata={Encode(clientMetadata)}" +
               $"&request_uri={Encode(requestUri)}";
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
