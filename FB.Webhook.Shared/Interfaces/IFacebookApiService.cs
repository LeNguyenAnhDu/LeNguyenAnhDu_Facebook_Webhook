using System.Threading.Tasks;

namespace FB.Webhook.Shared.Interfaces;

/// <summary>
/// Interface gọi tới Facebook Graph API
/// </summary>
public interface IFacebookApiService
{
    /// <summary>
    /// Ẩn một bình luận trên page
    /// </summary>
    Task<bool> HideCommentAsync(string commentId);
    
    /// <summary>
    /// Trả lời một bình luận
    /// </summary>
    Task<bool> ReplyToCommentAsync(string commentId, string message);
    
    /// <summary>
    /// Thích một bình luận
    /// </summary>
    Task<bool> LikeCommentAsync(string commentId);
    
    /// <summary>
    /// Block/Ban user (tuỳ chọn)
    /// </summary>
    Task<bool> BlockUserAsync(string userId);
}
