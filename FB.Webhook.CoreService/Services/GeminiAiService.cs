using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FB.Webhook.Shared.Enums;
using FB.Webhook.Shared.Interfaces;
using FB.Webhook.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace FB.Webhook.CoreService.Services;

public class GeminiAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiAiService> _logger;

    // Circuit Breaker: Ngắt mạch 30 giây nếu lỗi liên tiếp 10 lần
    private static readonly AsyncCircuitBreakerPolicy CircuitBreakerPolicy = Policy
        .Handle<HttpRequestException>()
        .CircuitBreakerAsync(
            exceptionsAllowedBeforeBreaking: 10,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (Exception ex, TimeSpan timespan) => { Console.WriteLine($"[CIRCUIT BREAKER] Gemini API broken for {timespan.TotalSeconds}s. Reason: {ex.Message}"); },
            onReset: () => { Console.WriteLine("[CIRCUIT BREAKER] Gemini API reset."); },
            onHalfOpen: () => { Console.WriteLine("[CIRCUIT BREAKER] Gemini API half-open."); }
        );

    public GeminiAiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AiAnalysisResult> AnalyzeContentAsync(string text)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        var model = _configuration["Gemini:Model"] ?? "gemini-1.5-flash";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var prompt = $@"
Phân tích bình luận/tin nhắn sau đây từ Facebook. 
Trả về KẾT QUẢ DUY NHẤT LÀ ĐỊNH DẠNG JSON với cấu trúc sau, không kèm bất kỳ markdown hay text nào khác:
{{
  ""intent"": ""AskPrice"" | ""Complaint"" | ""Positive"" | ""Other"",
  ""sentiment"": ""Tích cực"" | ""Tiêu cực"" | ""Trung lập"",
  ""confidence"": số_thực_từ_0_đến_1,
  ""suggestedAction"": ""hành động gợi ý ngắn gọn""
}}

Ví dụ tham khảo:
- ""Shop ơi giá bao nhiêu?"" -> intent: AskPrice, sentiment: Trung lập
- ""Mình chưa nhận được hàng"" -> intent: Complaint, sentiment: Tiêu cực
- ""Bài viết hay quá"" -> intent: Positive, sentiment: Tích cực
- ""Spam test link http://scam.com"" -> intent: Other, sentiment: Tiêu cực

Văn bản cần phân tích: ""{text}""
";

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        try
        {
            var response = await CircuitBreakerPolicy.ExecuteAsync(() => _httpClient.PostAsync(url, content));
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseJson);
            
            var textResult = document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            // Loại bỏ các markdown code block nếu Gemini lỡ sinh ra
            textResult = textResult?.Replace("```json", "").Replace("```", "").Trim();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            
            var result = JsonSerializer.Deserialize<AiAnalysisResult>(textResult, options);
            return result ?? new AiAnalysisResult { Intent = Intent.Other, Confidence = 0 };
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Gemini API Circuit Breaker is OPEN. Halting requests.");
            throw; // Đẩy ra để chui vào send_failed
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP Error calling Gemini API. Request may have been rate limited (429) or failed.");
            throw; // Ném ngược ra ngoài để CoreWorker bắt được và đưa vào topic send_failed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unknown Error calling Gemini API");
            throw;
        }
    }
}
