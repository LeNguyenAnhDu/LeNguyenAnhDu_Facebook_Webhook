using System.Text.Json;
using FB.Webhook.Shared.Enums;

namespace FB.Webhook.Shared.Models;

/// <summary>
/// Model chuẩn hóa event nhận được từ Facebook
/// </summary>
public class RawEvent
{
    /// <summary>
    /// ID của comment/message từ Facebook
    /// </summary>
    public string EventId { get; set; }
    
    /// <summary>
    /// Loại event (feed, messages, v.v.)
    /// </summary>
    public string EventType { get; set; }
    
    /// <summary>
    /// ID của page nhận event
    /// </summary>
    public string PageId { get; set; }
    
    /// <summary>
    /// Dữ liệu payload gốc dạng JSON
    /// </summary>
    public JsonElement Payload { get; set; }
    
    /// <summary>
    /// Thời điểm nhận
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Trạng thái xử lý hiện tại
    /// </summary>
    public EventStatus Status { get; set; } = EventStatus.Received;
}
