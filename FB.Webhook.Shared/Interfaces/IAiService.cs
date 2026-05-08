using System.Threading.Tasks;
using FB.Webhook.Shared.Models;

namespace FB.Webhook.Shared.Interfaces;

/// <summary>
/// Interface gọi tới Gemini AI
/// </summary>
public interface IAiService
{
    /// <summary>
    /// Phân tích nội dung văn bản (comment/message) để lấy Intent
    /// </summary>
    /// <param name="text">Nội dung text cần phân tích</param>
    /// <returns>Kết quả phân tích JSON đã parse</returns>
    Task<AiAnalysisResult> AnalyzeContentAsync(string text);
}
