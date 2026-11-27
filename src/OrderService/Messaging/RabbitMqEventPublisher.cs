using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderService.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(string queue, T message, CancellationToken ct = default);
}

public sealed class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqEventPublisher(IOptions<RabbitMqOptions> options)
    {
        var o = options.Value;
        _factory = new ConnectionFactory
        {
            HostName = o.Host,
            Port = o.Port,
            UserName = o.User,
            Password = o.Password
        };
    }

    public async Task PublishAsync<T>(string queue, T message, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var props = new BasicProperties { Persistent = true };

        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queue, mandatory: false,
            basicProperties: props, body: body, cancellationToken: ct);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true }) return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is not { IsOpen: true })
                _connection = await _factory.CreateConnectionAsync(ct);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
        _lock.Dispose();
    }
}