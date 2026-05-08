using System.Collections.Concurrent;
using System.Threading.Tasks;
using FB.Webhook.Shared.Enums;
using FB.Webhook.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace FB.Webhook.Shared.Services;

/// <summary>
/// Service theo dõi trạng thái sử dụng In-Memory Dictionary
/// </summary>
public class StateTrackerService : IStateTracker
{
    private readonly ILogger<StateTrackerService> _logger;
    // Lưu trữ trạng thái event: Key = EventId, Value = EventStatus
    private readonly ConcurrentDictionary<string, EventStatus> _eventStates = new();

    public StateTrackerService(ILogger<StateTrackerService> logger)
    {
        _logger = logger;
    }

    public Task<bool> TryAddEventAsync(string eventId)
    {
        var added = _eventStates.TryAdd(eventId, EventStatus.Received);
        if (added)
        {
            _logger.LogInformation($"[STATE TRACKER] Event {eventId} -> {EventStatus.Received}");
        }
        return Task.FromResult(added);
    }

    public Task UpdateEventStatusAsync(string eventId, EventStatus status)
    {
        _eventStates.AddOrUpdate(eventId, status, (_, _) => status);
        _logger.LogWarning($"[STATE TRACKER] Event {eventId} -> {status}"); // Dùng LogWarning màu vàng cho dễ nhìn trên console
        return Task.CompletedTask;
    }

    public Task<EventStatus?> GetEventStatusAsync(string eventId)
    {
        if (_eventStates.TryGetValue(eventId, out var status))
        {
            return Task.FromResult<EventStatus?>(status);
        }
        return Task.FromResult<EventStatus?>(null);
    }
}
