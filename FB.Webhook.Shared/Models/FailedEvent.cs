using System;

namespace FB.Webhook.Shared.Models;

/// <summary>
/// Model chứa thông tin event bị lỗi để Retry Service xử lý
/// </summary>
public class FailedEvent
{
    /// <summary>
    /// Sự kiện gốc gây ra lỗi
    /// </summary>
    public RawEvent OriginalEvent { get; set; }
    
    /// <summary>
    /// Thông báo lỗi cụ thể
    /// </summary>
    public string Error { get; set; }
    
    /// <summary>
    /// Số lần đã thử lại
    /// </summary>
    public int RetryCount { get; set; } = 0;
    
    /// <summary>
    /// Thời gian xảy ra lỗi
    /// </summary>
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}
