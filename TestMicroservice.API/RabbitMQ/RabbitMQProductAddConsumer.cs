using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace TestMicroservice.API.RabbitMQ
{
    public class RabbitMQProductAddConsumer: IRabbitMQProductAddConsumer
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQProductAddConsumer> _logger;
        private readonly string _queueName;

        private IConnection? _connection;
        private IChannel? _channel;
        

        public RabbitMQProductAddConsumer(IConfiguration configuration, ILogger<RabbitMQProductAddConsumer> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _queueName = _configuration["RabbitMQ_Products_Queue"]!;
        }

        public async Task Consume()
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ_HostName"]!,
                UserName = _configuration["RabbitMQ_UserName"]!,
                Password = _configuration["RabbitMQ_Password"]!,
                Port = int.Parse(_configuration["RabbitMQ_Port"]!)
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            //declare Exchange and Queue
            await DeclareAsync();

            await ConfigureConsumer();
        }

        /// <summary>
        /// declare Exchange and Queue
        /// </summary>
        /// <returns></returns>
        private async Task DeclareAsync()
        {
            string routingKey = _configuration["RabbitMQ_Products_RoutingKey"]!;
            string exchangeName = _configuration["RabbitMQ_Products_Exchange"]!;

            //Create exchange
            await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct, durable: true);

            //Create message queue
            await _channel!.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false,
                arguments: null);

            //Consumer get once only one message
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

            //Bind the message to exchange
            await _channel!.QueueBindAsync(queue: _queueName, exchange: exchangeName, routingKey: routingKey);
        }

        /// <summary>
        /// Configure Consumer
        /// </summary>
        /// <returns></returns>
        private async Task ConfigureConsumer()
        {
            //AsyncEventingBasicConsumer : Handles messages received from RabbitMQ
            var consumer = new AsyncEventingBasicConsumer(_channel!);

            //ReceivedAsync: Event triggered when a message is received
            consumer.ReceivedAsync += async (_, ea) =>
            {
                var body = ea.Body.ToArray();
                string message = Encoding.UTF8.GetString(body);

                var productAddMessage = JsonSerializer.Deserialize<ProductAddMessage>(message);

                if (productAddMessage != null)
                {
                    _logger.LogInformation("Message was received");
                    _logger.LogInformation("Message is processing");
                    Thread.Sleep(30000);
                    _logger.LogInformation("Product was added");
                    _logger.LogInformation($"{nameof(productAddMessage.ProductId)} : {productAddMessage.ProductId}," +
                                           $"{nameof(productAddMessage.ProductName)} : {productAddMessage.ProductName}," +
                                           $"{nameof(productAddMessage.UnitPrice)} : {productAddMessage.UnitPrice}," +
                                           $"{nameof(productAddMessage.QuantityInStock)} : {productAddMessage.QuantityInStock},");
                }
            };

            //BasicConsumeAsync: Starts consuming messages from the specified queue
            await _channel!.BasicConsumeAsync(queue: _queueName, consumer: consumer, autoAck: true);
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel != null)
            {
                await _channel.DisposeAsync();
            }

            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
