using server.Services;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();


builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IConnectionFactory>(sp => {
    var factory = new ConnectionFactory { HostName = "localhost" };
    return factory;
});
builder.Services.AddSingleton<CsvConsumer>();
builder.Services.AddSingleton<CsvProducer>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Task.Run(() =>
// {
//     var csvConsumerService = new CsvConsumerService(app.Configuration);
//     csvConsumerService.Start();
// });


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();