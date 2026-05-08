using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using FB.Webhook.Shared.Interfaces;
using FB.Webhook.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FB.Webhook.RetryService.Workers;

public class RetryWorker : BackgroundService
{
    private readonly ILogger<RetryWorker> _logger;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly string _bootstrapServers;
    private const int MaxRetryCount = 3;

    public RetryWorker(
        ILogger<RetryWorker> logger,
        IConfiguration configuration,
        IKafkaProducer kafkaProducer)
    {
        _logger = logger;
        _kafkaProducer = kafkaProducer;
        _bootstrapServers = configuration["Kafka:BootstrapServers"];
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => StartRetryLoop(stoppingToken), stoppingToken);
    }

    private async Task StartRetryLoop(CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            GroupId = "retry-service-group",
            BootstrapServers = _bootstrapServers,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe("send_failed");

        _logger.LogInformation("RetryWorker is starting to consume 'send_failed' topic.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(cancellationToken);
                    var eventJson = consumeResult.Message.Value;
                    
                    var failedEvent = JsonSerializer.Deserialize<FailedEvent>(eventJson);
                    if (failedEvent != null)
                    {
                        await ProcessFailedEventAsync(failedEvent);
                        consumer.Commit(consumeResult);
                    }
                }
                catch (ConsumeException e)
                {
                    _logger.LogError($"Consume error: {e.Error.Reason}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing retry loop");
                }
            }
        }
        catch (OperationCanceledException)
        {
            consumer.Close();
        }
    }

    private async Task ProcessFailedEventAsync(FailedEvent failedEvent)
    {
        _logger.LogInformation($"Processing failed event {failedEvent.OriginalEvent?.EventId}. Retry count: {failedEvent.RetryCount}");

        if (failedEvent.RetryCount >= MaxRetryCount)
        {
            _logger.LogError($"Event {failedEvent.OriginalEvent?.EventId} has reached max retry count ({MaxRetryCount}). Moving to Dead Letter (Mock).");
            return;
        }

        failedEvent.RetryCount++;

        // Delay trước khi retry (Backoff strategy đơn giản)
        var delaySeconds = 30 * failedEvent.RetryCount;
        _logger.LogInformation($"Waiting for {delaySeconds} seconds before retrying...");
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

        try
        {
            _logger.LogInformation($"Republishing event {failedEvent.OriginalEvent?.EventId} to raw_events topic.");
            // Đẩy lại vào topic chính để xử lý lại
            await _kafkaProducer.ProduceAsync("raw_events", failedEvent.OriginalEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to republish event {failedEvent.OriginalEvent?.EventId} to raw_events. Re-queueing to send_failed.");
            
            // Nếu lỗi tiếp, lại đẩy vào queue failed để loop sau xử lý tiếp
            await _kafkaProducer.ProduceFailedEventAsync("send_failed", failedEvent);
        }
    }
}
