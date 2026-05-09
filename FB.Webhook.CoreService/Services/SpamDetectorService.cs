using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FB.Webhook.CoreService.Services;

public interface ISpamDetectorService
{
    Task<bool> IsSpamAsync(string userId, string text);
    Task<int> GetSpamCountIn24hAsync(string userId);
    Task<bool> IsRateLimitedAsync(string userId);
}

public class SpamDetectorService : ISpamDetectorService
{
    // Regex nhận diện link
    private static readonly Regex LinkRegex = new(
        @"(http|ftp|https):\/\/([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:\/~+#-]*[\w@?^=%&\/~+#-])?|\b(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+(?:com|vn|net|org)\b", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Lưu trữ số lần bị tính là spam thực sự (để tính block)
    private readonly ConcurrentDictionary<string, ConcurrentBag<DateTime>> _userSpamRecords = new();
    
    // Lưu trữ lịch sử tin nhắn để check "Lặp nội dung nhiều lần"
    private readonly ConcurrentDictionary<string, ConcurrentQueue<(DateTime Time, string Text)>> _userMessageHistory = new();

    public Task<bool> IsSpamAsync(string userId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(false);

        bool isSpam = false;

        // 1. Kiểm tra có chứa Link không
        if (LinkRegex.IsMatch(text))
        {
            isSpam = true;
        }

        // 2. Kiểm tra có lặp nội dung quá nhiều lần không (spam ký tự)
        if (!string.IsNullOrEmpty(userId))
        {
            var history = _userMessageHistory.GetOrAdd(userId, _ => new ConcurrentQueue<(DateTime, string)>());
            history.Enqueue((DateTime.UtcNow, text.ToLower().Trim()));

            // Chỉ giữ lại history trong 10 phút gần nhất cho nhẹ RAM
            while (history.TryPeek(out var oldest) && (DateTime.UtcNow - oldest.Time).TotalMinutes > 10)
            {
                history.TryDequeue(out _);
            }

            // Đếm số lần tin nhắn Y HỆT NHAU xuất hiện trong 10 phút
            int repeatCount = history.Count(m => m.Text == text.ToLower().Trim());
            
            // Nếu lặp lại từ 3 lần trở lên -> Tính là Spam
            if (repeatCount >= 3)
            {
                isSpam = true;
            }
        }

        // Nếu bị phát hiện là spam (do link hoặc do lặp nội dung), ghi nhận vào hồ sơ đen
        if (isSpam && !string.IsNullOrEmpty(userId))
        {
            _userSpamRecords.AddOrUpdate(userId, 
                _ => new ConcurrentBag<DateTime> { DateTime.UtcNow }, 
                (_, bag) => { bag.Add(DateTime.UtcNow); return bag; });
        }

        return Task.FromResult(isSpam);
    }

    public Task<int> GetSpamCountIn24hAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId) || !_userSpamRecords.TryGetValue(userId, out var bag))
        {
            return Task.FromResult(0);
        }

        var threshold = DateTime.UtcNow.AddDays(-1);
        int count = bag.Count(time => time > threshold);

        return Task.FromResult(count);
    }

    public Task<bool> IsRateLimitedAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId) || !_userMessageHistory.TryGetValue(userId, out var history))
        {
            return Task.FromResult(false);
        }

        // Đếm số lượng tin nhắn trong 1 phút vừa qua (Threshold = 20)
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        int messagesInLastMinute = history.Count(m => m.Time > oneMinuteAgo);

        // Nếu lớn hơn hoặc bằng 20 tin nhắn / phút -> Rate Limited
        return Task.FromResult(messagesInLastMinute >= 20);
    }
}
