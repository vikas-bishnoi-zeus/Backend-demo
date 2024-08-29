using server.Services;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();


builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IConnectionFactory>(sp =>
{
    var factory = new ConnectionFactory { HostName = "localhost" };
    return factory;
});
builder.Services.AddSingleton<CsvConsumer>();
builder.Services.AddSingleton<CsvProducer>();
builder.Services.AddCors(options =>
  {
      options.AddPolicy("AllowAllOrigins",
          builder =>
          {
              builder.AllowAnyOrigin()
                     .AllowAnyMethod()
                     .AllowAnyHeader();
          });
  });

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
app.UseCors("AllowAllOrigins");

app.MapControllers();

app.Run();