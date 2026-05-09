using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace FB.Webhook.API.Services;

public class FacebookProxyService : IFacebookProxyService
{
    private readonly HttpClient _httpClient;
    private readonly string _pageAccessToken;
    private readonly ILogger<FacebookProxyService> _logger;
    private const string GraphApiBaseUrl = "https://graph.facebook.com/v25.0/me";

    // Retry 3 lần nếu có lỗi mạng hoặc lỗi HTTP 5xx
    private static readonly AsyncRetryPolicy RetryPolicy = Policy
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2s, 4s, 8s
            onRetry: (exception, timespan, retryAttempt, context) =>
            {
                Console.WriteLine($"[PROXY RETRY] Lần thử lại {retryAttempt} sau {timespan.TotalSeconds}s do lỗi: {exception.Message}");
            });

    // Circuit Breaker bọc thêm bên ngoài Retry
    private static readonly AsyncCircuitBreakerPolicy CircuitBreakerPolicy = Policy
        .Handle<HttpRequestException>()
        .CircuitBreakerAsync(
            exceptionsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (Exception ex, TimeSpan timespan) => { Console.WriteLine($"[PROXY CIRCUIT BREAKER] Ngắt mạch {timespan.TotalSeconds}s do lỗi {ex.Message}"); },
            onReset: () => { Console.WriteLine("[PROXY CIRCUIT BREAKER] Khôi phục mạch."); },
            onHalfOpen: () => { Console.WriteLine("[PROXY CIRCUIT BREAKER] Dùng thử mạch."); }
        );

    public FacebookProxyService(IConfiguration configuration, ILogger<FacebookProxyService> logger)
    {
        _httpClient = new HttpClient();
        _pageAccessToken = configuration["Facebook:PageAccessToken"];
        _logger = logger;
    }

    public async Task<string> GetPostsAsync()
    {
        var url = $"{GraphApiBaseUrl}/feed?access_token={_pageAccessToken}";
        _logger.LogInformation($"[PROXY GET] Calling FB API to get posts.");
        
        return await SendGetRequestAsync(url);
    }

    public async Task<string> CreatePostAsync(string message)
    {
        var url = $"{GraphApiBaseUrl}/feed?access_token={_pageAccessToken}";
        var body = new { message };
        _logger.LogInformation($"[PROXY POST] Calling FB API to create post: {message}");

        return await SendPostRequestAsync(url, body);
    }

    public async Task<string> GetCommentsAsync(string postId)
    {
        // URL = graph.facebook.com/v25.0/{postId}/comments
        var url = $"https://graph.facebook.com/v25.0/{postId}/comments?access_token={_pageAccessToken}";
        _logger.LogInformation($"[PROXY GET] Calling FB API to get comments for post {postId}");

        return await SendGetRequestAsync(url);
    }

    private async Task<string> SendGetRequestAsync(string url)
    {
        // Kết hợp Retry Policy bọc bên trong Circuit Breaker Policy
        var fallbackPolicy = Policy.WrapAsync(CircuitBreakerPolicy, RetryPolicy);
        
        var response = await fallbackPolicy.ExecuteAsync(async () => 
        {
            var res = await _httpClient.GetAsync(url);
            res.EnsureSuccessStatusCode(); // Ném lỗi để Polly catch và Retry
            return res;
        });
        
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> SendPostRequestAsync(string url, object body)
    {
        var jsonContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var fallbackPolicy = Policy.WrapAsync(CircuitBreakerPolicy, RetryPolicy);

        var response = await fallbackPolicy.ExecuteAsync(async () => 
        {
            var res = await _httpClient.PostAsync(url, jsonContent);
            res.EnsureSuccessStatusCode();
            return res;
        });
        
        return await response.Content.ReadAsStringAsync();
    }
}
