namespace FB.Webhook.Shared.Enums;

public enum EventStatus
{
    /// <summary>
    /// Vừa nhận được từ webhook
    /// </summary>
    Received,
    
    /// <summary>
    /// Đang được xử lý (ví dụ gọi AI)
    /// </summary>
    Processing,
    
    /// <summary>
    /// Đã xử lý xong và phân loại thành công
    /// </summary>
    Processed,
    
    /// <summary>
    /// Đã thực hiện phản hồi (Reply comment)
    /// </summary>
    Replied,
    
    /// <summary>
    /// Đã thực hiện ẩn bình luận (Hide comment)
    /// </summary>
    Hidden,
    
    /// <summary>
    /// Xử lý thất bại
    /// </summary>
    Failed
}
