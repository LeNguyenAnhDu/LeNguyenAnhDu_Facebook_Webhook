using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FB.Webhook.Shared.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FB.Webhook.CoreService.Services;

public class FacebookApiService : IFacebookApiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FacebookApiService> _logger;
    private readonly string _pageAccessToken;
    private const string GraphApiBaseUrl = "https://graph.facebook.com/v25.0";

    public FacebookApiService(HttpClient httpClient, IConfiguration configuration, ILogger<FacebookApiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _pageAccessToken = _configuration["Facebook:PageAccessToken"];
    }

    public async Task<bool> HideCommentAsync(string commentId)
    {
        var url = $"{GraphApiBaseUrl}/{commentId}?access_token={_pageAccessToken}";
        var body = new { is_hidden = true };
        
        return await SendPostRequestAsync(url, body);
    }

    public async Task<bool> ReplyToCommentAsync(string commentId, string message)
    {
        var url = $"{GraphApiBaseUrl}/{commentId}/comments?access_token={_pageAccessToken}";
        var body = new { message = message };
        
        return await SendPostRequestAsync(url, body);
    }

    public async Task<bool> LikeCommentAsync(string commentId)
    {
        var url = $"{GraphApiBaseUrl}/{commentId}/likes?access_token={_pageAccessToken}";
        var body = new { };
        
        return await SendPostRequestAsync(url, body);
    }

    public Task<bool> BlockUserAsync(string userId)
    {
        // Tuỳ chọn, API block user phức tạp hơn (cần Page ID)
        _logger.LogInformation($"Blocked user {userId} (Mock)");
        return Task.FromResult(true);
    }

    private async Task<bool> SendPostRequestAsync(string url, object body)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Facebook API Error: {error}");
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling Facebook API");
            return false;
        }
    }
}
