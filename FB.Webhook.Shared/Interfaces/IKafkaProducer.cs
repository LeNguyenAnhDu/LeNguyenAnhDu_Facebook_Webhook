using System.Threading.Tasks;
using FB.Webhook.Shared.Models;

namespace FB.Webhook.Shared.Interfaces;

/// <summary>
/// Interface cho Kafka Producer
/// </summary>
public interface IKafkaProducer
{
    /// <summary>
    /// Publish một sự kiện thô lên topic `raw_events`
    /// </summary>
    /// <param name="topic">Tên topic (thường là raw_events)</param>
    /// <param name="eventData">Dữ liệu event</param>
    Task ProduceAsync(string topic, RawEvent eventData);
    
    /// <summary>
    /// Publish một sự kiện lỗi lên topic `send_failed`
    /// </summary>
    /// <param name="topic">Tên topic (send_failed)</param>
    /// <param name="failedEvent">Dữ liệu event lỗi</param>
    Task ProduceFailedEventAsync(string topic, FailedEvent failedEvent);
}
