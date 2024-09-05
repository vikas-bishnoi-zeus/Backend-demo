using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using server.Services;
using RabbitMQ.Client;
using server.Hubs;

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

// Register SignalR services.
builder.Services.AddSignalR();

// Configure CORS to allow requests from a specific origin.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        builder =>
        {
            builder.WithOrigins("http://127.0.0.1:5500") // Specify the frontend origin
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials(); // Required for SignalR with credentials
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
app.UseCors("AllowSpecificOrigins"); // Use the correct CORS policy

// Enable HTTPS redirection.
app.UseHttpsRedirection();

// Enable authorization middleware (no specific policies defined here).
app.UseAuthorization();

// Map controller routes to handle API requests.
app.MapControllers();

// Map the SignalR hub to handle SignalR requests.
app.MapHub<ProgressHub>("/progressHub");

// Run the application.
app.Run();
