using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FB.Webhook.Shared.Interfaces;
using FB.Webhook.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FB.Webhook.API.Controllers;

[ApiController]
[Route("[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly IStateTracker _stateTracker;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IConfiguration configuration,
        IKafkaProducer kafkaProducer,
        IStateTracker stateTracker,
        ILogger<WebhookController> logger)
    {
        _configuration = configuration;
        _kafkaProducer = kafkaProducer;
        _stateTracker = stateTracker;
        _logger = logger;
    }

    /// <summary>
    /// Xử lý yêu cầu xác minh từ Facebook
    /// </summary>
    [HttpGet]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string token,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var verifyToken = _configuration["Facebook:VerifyToken"];

        if (mode == "subscribe" && token == verifyToken)
        {
            _logger.LogInformation("Webhook verified successfully.");
            return Ok(challenge);
        }

        _logger.LogWarning("Webhook verification failed.");
        return Forbid();
    }

    /// <summary>
    /// Nhận event từ Facebook
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook()
    {
        // 1. Đọc body thô
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        // 2. Xác thực chữ ký
        var signatureHeader = Request.Headers["X-Hub-Signature-256"].ToString();
        var appSecret = _configuration["Facebook:AppSecret"];

        if (string.IsNullOrEmpty(appSecret) || !VerifySignature(body, signatureHeader, appSecret))
        {
            _logger.LogWarning("Invalid signature.");
            return BadRequest("Invalid signature");
        }

        try
        {
            // In ra toàn bộ nội dung thật của Facebook gửi tới để debug
            _logger.LogInformation($"RAW PAYLOAD FROM FACEBOOK: {body}");

            // 3. Deserialize payload
            var payload = JsonSerializer.Deserialize<FacebookWebhookPayload>(body);
            if (payload == null || payload.Object != "page" || payload.Entry == null)
            {
                _logger.LogWarning("Payload is null or not a page object.");
                return Ok("EVENT_RECEIVED");
            }

            // 4. Phân tích và đẩy vào Kafka
            foreach (var entry in payload.Entry)
            {
                var pageId = entry.Id;
                
                // Xử lý Comments/Posts (Feed)
                if (entry.Changes != null)
                {
                    foreach (var change in entry.Changes)
                    {
                        if (change.Value == null) continue;
                        
                        var eventId = change.Value.CommentId ?? change.Value.PostId;
                        if (string.IsNullOrEmpty(eventId)) continue;

                        await PublishEventAsync(eventId, change.Field, pageId, change.Value);
                    }
                }

                // Xử lý Messages (Inbox)
                if (entry.Messaging != null)
                {
                    foreach (var msg in entry.Messaging)
                    {
                        if (msg.Message == null) continue;
                        
                        var eventId = msg.Message.Mid;
                        if (string.IsNullOrEmpty(eventId)) continue;

                        await PublishEventAsync(eventId, "messages", pageId, msg);
                    }
                }
            }

            // Luôn trả về 200 OK ngay lập tức để Facebook không retry
            return Ok("EVENT_RECEIVED");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook payload");
            // Vẫn trả về 200 OK để FB không báo lỗi liên tục nếu là lỗi logic nội bộ
            return Ok("EVENT_RECEIVED"); 
        }
    }

    private async Task PublishEventAsync(string eventId, string eventType, string pageId, object payloadData)
    {
        // 5. Kiểm tra lặp event
        var isNew = await _stateTracker.TryAddEventAsync(eventId);
        if (!isNew)
        {
            _logger.LogInformation($"Event {eventId} is already processing or processed. Skipping.");
            return;
        }

        // 6. Tạo RawEvent và publish lên Kafka
        var rawEvent = new RawEvent
        {
            EventId = eventId,
            EventType = eventType,
            PageId = pageId,
            Payload = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(payloadData))
        };

        await _kafkaProducer.ProduceAsync("raw_events", rawEvent);
        _logger.LogInformation($"Published event {eventId} to Kafka.");
    }

    /// <summary>
    /// Xác thực HMAC-SHA256 signature
    /// </summary>
    private bool VerifySignature(string payload, string signatureHeader, string appSecret)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256="))
        {
            return false;
        }

        var expectedSignature = signatureHeader.Substring(7);
        var secretBytes = Encoding.UTF8.GetBytes(appSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLower();

        return hashString == expectedSignature;
    }
}
