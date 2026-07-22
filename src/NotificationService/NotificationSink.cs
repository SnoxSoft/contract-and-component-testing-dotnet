using System.Collections.Concurrent;

namespace NotificationService;

public record Notification(Guid OrderId, string CustomerId, string Message);

public interface INotificationSink
{
    Task SendAsync(Notification notification, CancellationToken ct = default);
}

/// <summary>
/// Stand-in for a real email/SMS gateway. Keeps the service observable without
/// pulling a delivery provider into the course.
/// </summary>
public class InMemoryNotificationSink(ILogger<InMemoryNotificationSink> logger) : INotificationSink
{
    private readonly ConcurrentQueue<Notification> _sent = new();

    public IReadOnlyCollection<Notification> Sent => _sent;

    public Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        _sent.Enqueue(notification);
        logger.LogInformation(
            "Notified {CustomerId} about order {OrderId}", notification.CustomerId, notification.OrderId);

        return Task.CompletedTask;
    }
}
