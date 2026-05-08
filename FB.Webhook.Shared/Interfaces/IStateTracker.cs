using FB.Webhook.Shared.Enums;
using System.Threading.Tasks;

namespace FB.Webhook.Shared.Interfaces;

/// <summary>
/// Interface theo dõi trạng thái xử lý của Event (chống xử lý lặp)
/// </summary>
public interface IStateTracker
{
    /// <summary>
    /// Kiểm tra xem event đã tồn tại chưa, nếu chưa thì thêm vào hệ thống với trạng thái khởi tạo
    /// </summary>
    /// <returns>True nếu là event mới, False nếu đã có</returns>
    Task<bool> TryAddEventAsync(string eventId);
    
    /// <summary>
    /// Cập nhật trạng thái của event
    /// </summary>
    Task UpdateEventStatusAsync(string eventId, EventStatus status);
    
    /// <summary>
    /// Lấy trạng thái hiện tại của event
    /// </summary>
    Task<EventStatus?> GetEventStatusAsync(string eventId);
}
