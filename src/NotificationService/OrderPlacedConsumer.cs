using Contracts;
using MassTransit;

namespace NotificationService;

public class OrderPlacedConsumer(INotificationSink sink) : IConsumer<OrderPlaced>
{
    public async Task Consume(ConsumeContext<OrderPlaced> context)
    {
        var message = context.Message;
        var itemCount = message.Lines.Sum(line => line.Quantity);

        var notification = new Notification(
            message.OrderId,
            message.CustomerId,
            $"Thanks for your order of {itemCount} item(s), totalling {message.TotalCents / 100m:0.00} {message.Currency}.");

        await sink.SendAsync(notification, context.CancellationToken);
    }
}
