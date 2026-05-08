namespace FB.Webhook.Shared.Enums;

public enum Intent
{
    /// <summary>
    /// Hỏi giá sản phẩm
    /// </summary>
    AskPrice,
    
    /// <summary>
    /// Phàn nàn, khiếu nại
    /// </summary>
    Complaint,
    
    /// <summary>
    /// Phản hồi tích cực, khen ngợi
    /// </summary>
    Positive,
    
    /// <summary>
    /// Spam chứa link
    /// </summary>
    SpamLink,
    
    /// <summary>
    /// Spam lặp nội dung
    /// </summary>
    SpamRepeat,
    
    /// <summary>
    /// Ý định khác (chưa xác định rõ)
    /// </summary>
    Other
}
