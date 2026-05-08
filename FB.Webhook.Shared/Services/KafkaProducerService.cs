using System;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using FB.Webhook.Shared.Interfaces;
using FB.Webhook.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FB.Webhook.Shared.Services;

public class KafkaProducerService : IKafkaProducer, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        
        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        var config = new ProducerConfig { BootstrapServers = bootstrapServers };
        
        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public async Task ProduceAsync(string topic, RawEvent eventData)
    {
        try
        {
            var message = new Message<Null, string>
            {
                Value = JsonSerializer.Serialize(eventData)
            };

            var deliveryResult = await _producer.ProduceAsync(topic, message);
            _logger.LogInformation($"Delivered message to {deliveryResult.TopicPartitionOffset}");
        }
        catch (ProduceException<Null, string> e)
        {
            _logger.LogError($"Delivery failed: {e.Error.Reason}");
            throw;
        }
    }

    public async Task ProduceFailedEventAsync(string topic, FailedEvent failedEvent)
    {
         try
        {
            var message = new Message<Null, string>
            {
                Value = JsonSerializer.Serialize(failedEvent)
            };

            var deliveryResult = await _producer.ProduceAsync(topic, message);
            _logger.LogInformation($"Delivered failed event to {deliveryResult.TopicPartitionOffset}");
        }
        catch (ProduceException<Null, string> e)
        {
            _logger.LogError($"Delivery failed: {e.Error.Reason}");
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
