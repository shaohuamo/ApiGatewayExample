using ApiGatewayExample.ConsulServiceBuilder;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services
    .AddOcelot(builder.Configuration)
    .AddConsul<MyConsulServiceBuilder>()
    .AddConfigStoredInConsul();//store ocelot.json in consul server


var app = builder.Build();
await app.UseOcelot();

app.Run();
