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
    public VerificationResultDto? Result { get; set; }
    public string? PresentationUri { get; set; }   // NUEVO
    public string? PresentationUriDecoded { get; set; } // NUEVO

    public IndexModel(IHttpClientFactory factory, IConfiguration config)
    {
        _httpClientFactory = factory;
        _config = config;
    }

    public async Task<IActionResult> OnPostAsync(string profile)
    {
        var baseUrl = _config["VerifierApiBaseUrl"];
        var client = _httpClientFactory.CreateClient();

        // Crear sesión en el backend verificador
        var response = await client.PostAsync($"{baseUrl}/verifier/sessions?profile={profile}", null);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var sessionId = doc.RootElement.GetProperty("session_id").GetString();
        var qrUri = doc.RootElement.GetProperty("qr_uri").GetString();
        PresentationUri = qrUri;
        PresentationUriDecoded = WebUtility.UrlDecode(qrUri);

        // Generar QR con QRCoder
        var qrGen = new QRCodeGenerator();
        var qrData = qrGen.CreateQrCode(qrUri, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        QrImageBase64 = Convert.ToBase64String(qrCode.GetGraphic(6));

        // Iniciar polling del resultado
        _ = Task.Run(async () => await PollResult(baseUrl!, sessionId!));

        return Page();
    }

    private async Task PollResult(string baseUrl, string sessionId)
    {
        var client = _httpClientFactory.CreateClient();
        for (int i = 0; i < 12; i++)
        {
            await Task.Delay(5000);
            var res = await client.GetAsync($"{baseUrl}/verifier/result/{sessionId}");
            if (!res.IsSuccessStatusCode) continue;
            var json = await res.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("completed", out var completed) && completed.GetBoolean())
            {
                Result = JsonSerializer.Deserialize<VerificationResultDto>(json);
                break;
            }
        }
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
