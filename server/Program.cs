using server.Services;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Enable API documentation and exploration.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register RabbitMQ connection factory as a singleton service.
builder.Services.AddSingleton<IConnectionFactory>(sp =>
{
    var factory = new ConnectionFactory { HostName = "localhost" };
    return factory;
});

// Register custom services for CSV processing with RabbitMQ.
builder.Services.AddSingleton<CsvConsumer>();
builder.Services.AddSingleton<CsvProducer>();

// Configure CORS to allow requests from any origin.
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
    // Enable Swagger for API documentation in development.
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Enable CORS using the specified policy.
app.UseCors("AllowAllOrigins");

// Enable HTTPS redirection.
app.UseHttpsRedirection();

// Enable authorization middleware (no specific policies defined here).
app.UseAuthorization();

// Map controller routes to handle API requests.
app.MapControllers();

// Run the application.
app.Run();