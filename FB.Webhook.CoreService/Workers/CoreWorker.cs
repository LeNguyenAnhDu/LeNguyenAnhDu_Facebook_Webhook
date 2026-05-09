using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using FB.Webhook.CoreService.Services;
using FB.Webhook.Shared.Enums;
using FB.Webhook.Shared.Interfaces;
using FB.Webhook.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FB.Webhook.CoreService.Workers;

public class CoreWorker : BackgroundService
{
    private readonly ILogger<CoreWorker> _logger;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly IAiService _aiService;
    private readonly IFacebookApiService _fbApiService;
    private readonly ISpamDetectorService _spamDetector;
    private readonly IStateTracker _stateTracker;
    private readonly string _bootstrapServers;

    public CoreWorker(
        ILogger<CoreWorker> logger,
        IConfiguration configuration,
        IKafkaProducer kafkaProducer,
        IAiService aiService,
        IFacebookApiService fbApiService,
        ISpamDetectorService spamDetector,
        IStateTracker stateTracker)
    {
        _logger = logger;
        _kafkaProducer = kafkaProducer;
        _aiService = aiService;
        _fbApiService = fbApiService;
        _spamDetector = spamDetector;
        _stateTracker = stateTracker;
        _bootstrapServers = configuration["Kafka:BootstrapServers"];
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            GroupId = "core-service-group",
            BootstrapServers = _bootstrapServers,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false // Tắt auto commit để quản lý thủ công
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe("raw_events");

        _logger.LogInformation("CoreWorker is starting to consume 'raw_events' topic.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(cancellationToken);
                    var eventJson = consumeResult.Message.Value;
                    
                    var rawEvent = JsonSerializer.Deserialize<RawEvent>(eventJson);
                    if (rawEvent != null)
                    {
                        await ProcessEventAsync(rawEvent);
                        
                        // Commit offset sau khi xử lý thành công
                        consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException e)
                {
                    _logger.LogError($"Consume error: {e.Error.Reason}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event loop");
                }
            }
        }
        catch (OperationCanceledException)
        {
            consumer.Close();
        }
    }

    private async Task ProcessEventAsync(RawEvent rawEvent)
    {
        try
        {
            // IDEMPOTENT CHECK: Tránh xử lý lặp sự kiện đã hoàn thành
            var currentStatus = await _stateTracker.GetEventStatusAsync(rawEvent.EventId);
            if (currentStatus == EventStatus.Processed || currentStatus == EventStatus.Replied || currentStatus == EventStatus.Hidden || currentStatus == EventStatus.PendingReview)
            {
                _logger.LogInformation($"[IDEMPOTENT] Event {rawEvent.EventId} already processed (Status: {currentStatus}). Skipping duplicate.");
                return;
            }

            _logger.LogInformation($"Processing event {rawEvent.EventId}");
            await _stateTracker.UpdateEventStatusAsync(rawEvent.EventId, EventStatus.Processing);
            
            // 1. Phân tích JsonElement (Payload) để lấy userId và text
            string text = "";
            string userId = "";
            
            if (rawEvent.Payload.TryGetProperty("message", out var messageProp))
                text = messageProp.GetString();
                
            if (rawEvent.Payload.TryGetProperty("from", out var fromProp) && 
                fromProp.TryGetProperty("id", out var idProp))
                userId = idProp.GetString();

            if (string.IsNullOrEmpty(text))
            {
                _logger.LogWarning($"Event {rawEvent.EventId} has no message. Skipping.");
                return;
            }

            // 2. Spam Check
            if (await _spamDetector.IsSpamAsync(userId, text))
            {
                _logger.LogInformation($"Event {rawEvent.EventId} detected as LINK SPAM. Hiding comment and pushing to manual_review.");
                await _fbApiService.HideCommentAsync(rawEvent.EventId);
                await _stateTracker.UpdateEventStatusAsync(rawEvent.EventId, EventStatus.Hidden);
                
                // Đẩy sang hàng chờ duyệt thủ công
                await _kafkaProducer.ProduceAsync("manual_review", rawEvent);
                return;
            }

            int spamCount = await _spamDetector.GetSpamCountIn24hAsync(userId);
            if (spamCount >= 3)
            {
                _logger.LogInformation($"User {userId} spam repeat limit reached. Hiding comment and blocking.");
                await _fbApiService.HideCommentAsync(rawEvent.EventId);
                await _fbApiService.BlockUserAsync(userId);
                await _stateTracker.UpdateEventStatusAsync(rawEvent.EventId, EventStatus.Hidden);
                return;
            }

            // 2.5 Kiểm tra Rate Limiting (20 comments / minute)
            if (await _spamDetector.IsRateLimitedAsync(userId))
            {
                _logger.LogWarning($"User {userId} hit RATE LIMIT. Sending to manual_review.");
                await _stateTracker.UpdateEventStatusAsync(rawEvent.EventId, EventStatus.PendingReview);
                await _kafkaProducer.ProduceAsync("manual_review", rawEvent);
                return;
            }

            // 3. AI Analysis
            var aiResult = await _aiService.AnalyzeContentAsync(text);
            _logger.LogInformation($"AI Intent for {rawEvent.EventId}: {aiResult.Intent} | Sentiment: {aiResult.Sentiment} | Confidence: {(aiResult.Confidence * 100):0}%");

            // 4. Action dựa trên Intent
            switch (aiResult.Intent)
            {
                case Intent.AskPrice:
                    await _fbApiService.ReplyToCommentAsync(rawEvent.EventId, "Dạ, sản phẩm này bên em có giá niêm yết là XYZ. Anh/chị check inbox để được tư vấn thêm nhé ạ!");
                    await _stateTracker.UpdateEventStatusAsync(rawEvent.EventId, EventStatus.Replied);
                    break;
                case Intent.Complaint:
                    await _fbApiService.ReplyToCommentAsync(rawEvent.EventId, "Dạ, shop rất xin lỗi về trải nghiệm của mình. Shop đã inbox hỗ trợ giải quyết ngay ạ.");
                    await _stateTracker.UpdateEventStatusAsync(rawEvent.EventId, EventStatus.Replied);
                    break;
                case Intent.Positive:
                    await _fbApiService.LikeCommentAsync(rawEvent.EventId);
                    await _fbApiService.ReplyToCommentAsync(rawEvent.EventId, "Cảm ơn bạn đã ủng hộ shop!");
                    await _stateTracker.UpdateEventStatusAsync(rawEvent.EventId, EventStatus.Replied);
                    break;
                case Intent.SpamLink:
                case Intent.SpamRepeat:
                    await _fbApiService.HideCommentAsync(rawEvent.EventId);
                    await _stateTracker.UpdateEventStatusAsync(rawEvent.EventId, EventStatus.Hidden);
                    if (aiResult.Intent == Intent.SpamLink)
                    {
                        await _kafkaProducer.ProduceAsync("manual_review", rawEvent);
                    }
                    break;
                default:
                    _logger.LogInformation("No specific action needed.");
                    await _stateTracker.UpdateEventStatusAsync(rawEvent.EventId, EventStatus.Processed);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to process event {rawEvent.EventId}. Sending to send_failed topic.");
            await _stateTracker.UpdateEventStatusAsync(rawEvent.EventId, EventStatus.Failed);

            
            var failedEvent = new FailedEvent
            {
                OriginalEvent = rawEvent,
                Error = ex.Message,
                RetryCount = 0,
                FailedAt = DateTime.UtcNow
            };
            
            // Push lên Kafka retry
            await _kafkaProducer.ProduceFailedEventAsync("send_failed", failedEvent);
        }
    }
}
