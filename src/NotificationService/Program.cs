using MassTransit;
using NotificationService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<INotificationSink, InMemoryNotificationSink>();

builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<OrderPlacedConsumer>();

    bus.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"]!, host =>
        {
            host.Username(builder.Configuration["RabbitMq:Username"]!);
            host.Password(builder.Configuration["RabbitMq:Password"]!);
        });

        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
