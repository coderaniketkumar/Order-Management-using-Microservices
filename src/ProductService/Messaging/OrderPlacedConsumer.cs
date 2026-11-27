using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProductService.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ProductService.Messaging;

public class OrderPlacedConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderPlacedConsumer> _logger;
    private readonly ConnectionFactory _factory;

    public OrderPlacedConsumer(IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options, ILogger<OrderPlacedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var o = options.Value;
        _factory = new ConnectionFactory
        {
            HostName = o.Host,
            Port = o.Port,
            UserName = o.User,
            Password = o.Password
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // RabbitMQ abhi chalu na ho to har 5 second mein dobara koshish karte raho
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await _factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(Queues.OrderPlaced, durable: true, exclusive: false,
                    autoDelete: false, arguments: null, cancellationToken: stoppingToken);
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) => await HandleAsync(channel, ea, stoppingToken);

                await channel.BasicConsumeAsync(Queues.OrderPlaced, autoAck: false, consumer: consumer,
                    cancellationToken: stoppingToken);
                _logger.LogInformation("Listening on queue {Queue}", Queues.OrderPlaced);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ not reachable. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        try
        {
            //await Task.Delay(TimeSpan.FromSeconds(20), ct); // TEMP: sirf demo ke liye, baad mein hata dena
            var evt = JsonSerializer.Deserialize<OrderPlacedEvent>(ea.Body.Span)
                      ?? throw new InvalidOperationException("Empty or invalid message.");

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

            // Ek hi SQL statement: stock tabhi kam hoga jab kaafi ho, kabhi negative nahi jaayega
            var updated = await db.Products
                .Where(p => p.Id == evt.ProductId && p.StockQuantity >= evt.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, p => p.StockQuantity - evt.Quantity), ct);

            if (updated == 0)
                _logger.LogWarning("Stock not reduced for order {OrderId}: product {ProductId} missing or insufficient stock.",
                    evt.OrderId, evt.ProductId);
            else
                _logger.LogInformation("Stock reduced by {Quantity} for product {ProductId} (order {OrderId}).",
                    evt.Quantity, evt.ProductId, evt.OrderId);

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process OrderPlaced message.");
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
        }
    }
}