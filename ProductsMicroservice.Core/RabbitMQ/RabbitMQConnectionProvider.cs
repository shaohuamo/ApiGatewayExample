using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace ProductsMicroservice.Core.RabbitMQ;

public class RabbitMQConnectionProvider: IRabbitMQConnectionProvider
{
    private readonly ConnectionFactory _connectionFactory;
    private IConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RabbitMQConnectionProvider(IConfiguration configuration)
    {
        string hostName = configuration["RabbitMQ_HostName"]!;
        string userName = configuration["RabbitMQ_UserName"]!;
        string password = configuration["RabbitMQ_Password"]!;
        string port = configuration["RabbitMQ_Port"]!;

        _connectionFactory = new ConnectionFactory()
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            Port = Convert.ToInt32(port)
        };
    }

    //using double check locking to get a singleton connection
    public async Task<IConnection> GetConnectionAsync()
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _lock.WaitAsync();
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            _connection = await _connectionFactory.CreateConnectionAsync();
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
}