using ProductsMicroservice.Core;
using ProductsMicroservice.Infrastructure;
using ProductsMicroService.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddProductsMicroserviceCore(builder.Configuration);
builder.Services.ProductsMicroserviceInfrastructure(builder.Configuration);

builder.Services.AddControllers();

//Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    //generate api.xml by comment of action method
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "api.xml"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandlingMiddleware();

//Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => {

        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProductsMicroService.API");
        //set swagger as root path
        options.RoutePrefix = string.Empty;
    });
}


app.MapControllers();

app.Run();
