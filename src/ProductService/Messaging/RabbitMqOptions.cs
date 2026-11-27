namespace ProductService.Messaging;

public class RabbitMqOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}