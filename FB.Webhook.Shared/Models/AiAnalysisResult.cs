using FB.Webhook.Shared.Enums;

namespace FB.Webhook.Shared.Models;

/// <summary>
/// Model chứa kết quả phân tích từ Gemini AI
/// </summary>
public class AiAnalysisResult
{
    /// <summary>
    /// Phân loại ý định của người dùng
    /// </summary>
    public Intent Intent { get; set; }
    
    /// <summary>
    /// Thái độ của bình luận (Tích cực, tiêu cực, trung tính)
    /// </summary>
    public string Sentiment { get; set; }
    
    /// <summary>
    /// Mức độ tự tin của AI (0.0 đến 1.0)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Hành động đề xuất nếu có
    /// </summary>
    public string SuggestedAction { get; set; }
}
