using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;

namespace ProductsMicroservice.Core.RabbitMQ;

public class RabbitMQPublisher : IRabbitMQPublisher
{
    private readonly IRabbitMQConnectionProvider _connectionProvider;
    private readonly string _exchangeName;

    public RabbitMQPublisher(IConfiguration configuration, IRabbitMQConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
        _exchangeName = configuration["RabbitMQ_Products_Exchange"]!;
    }


    public async Task Publish<T>(string routingKey, T message)
    {
        var connection = await _connectionProvider.GetConnectionAsync();

        await using var channel = await connection.CreateChannelAsync();

        string messageJson = JsonSerializer.Serialize(message);
        byte[] messageBodyInBytes = Encoding.UTF8.GetBytes(messageJson);

        //Create exchange
        //this exchange will be created automatically if not exist
        await channel.ExchangeDeclareAsync(exchange: _exchangeName, type: ExchangeType.Direct, durable: true);

        var properties = new BasicProperties
        {
            Persistent = true,//persist message
            ContentType = "application/json"
        };

        //Publish message
        await channel.BasicPublishAsync(exchange: _exchangeName, routingKey: routingKey, mandatory: false,
            basicProperties: properties, body: messageBodyInBytes);
    }

}