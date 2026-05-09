using System;
using System.Text.Json;
using System.Threading.Tasks;
using FB.Webhook.API.Attributes;
using FB.Webhook.API.Models;
using FB.Webhook.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FB.Webhook.API.Controllers;

[ApiController]
[Route("api/facebook")]
[AdminOnly] // Đảm bảo mọi API trong controller này đều cần quyền Admin
public class FacebookProxyController : ControllerBase
{
    private readonly IFacebookProxyService _proxyService;

    // Thay vì dùng DI truyền thống từ Program.cs, chúng ta khởi tạo Service trực tiếp ở đây 
    // để không đụng chạm tới các file code cũ.
    public FacebookProxyController(IConfiguration configuration, ILogger<FacebookProxyService> logger)
    {
        _proxyService = new FacebookProxyService(configuration, logger);
    }

    [HttpGet("posts")]
    public async Task<IActionResult> GetPosts()
    {
        try
        {
            var jsonResult = await _proxyService.GetPostsAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(jsonResult);
            return Ok(ApiResponse<JsonElement>.Ok(data));
        }
        catch (Exception ex)
        {
            var errorCode = ex.Message.Contains("401") ? "UNAUTHORIZED" : "FB_API_ERROR";
            return StatusCode(errorCode == "UNAUTHORIZED" ? 401 : 500, ApiResponse<object>.Fail(ex.Message, errorCode));
        }
    }

    [HttpPost("post")]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request)
    {
        if (string.IsNullOrEmpty(request?.Message))
            return BadRequest(ApiResponse<object>.Fail("Message cannot be empty", "BAD_REQUEST"));

        try
        {
            var jsonResult = await _proxyService.CreatePostAsync(request.Message);
            var data = JsonSerializer.Deserialize<JsonElement>(jsonResult);
            return Ok(ApiResponse<JsonElement>.Ok(data));
        }
        catch (Exception ex)
        {
            var errorCode = ex.Message.Contains("401") ? "UNAUTHORIZED" : "FB_API_ERROR";
            return StatusCode(errorCode == "UNAUTHORIZED" ? 401 : 500, ApiResponse<object>.Fail(ex.Message, errorCode));
        }
    }

    [HttpGet("comments/{postId}")]
    public async Task<IActionResult> GetComments(string postId)
    {
        try
        {
            var jsonResult = await _proxyService.GetCommentsAsync(postId);
            var data = JsonSerializer.Deserialize<JsonElement>(jsonResult);
            return Ok(ApiResponse<JsonElement>.Ok(data));
        }
        catch (Exception ex)
        {
            var errorCode = ex.Message.Contains("401") ? "UNAUTHORIZED" : "FB_API_ERROR";
            return StatusCode(errorCode == "UNAUTHORIZED" ? 401 : 500, ApiResponse<object>.Fail(ex.Message, errorCode));
        }
    }
}

public class CreatePostRequest
{
    public string Message { get; set; }
}
