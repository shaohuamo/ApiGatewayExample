using Steeltoe.Discovery.Consul;
using TestMicroservice.API.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddConsulDiscoveryClient();

builder.Services.AddSingleton<IRabbitMQProductAddConsumer, RabbitMQProductAddConsumer>();
builder.Services.AddHostedService<RabbitMQProductAddHostedService>();

var app = builder.Build();

app.MapControllers();

app.Run();
