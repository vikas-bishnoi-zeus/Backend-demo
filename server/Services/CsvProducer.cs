using RabbitMQ.Client;
using server.Models;
using System.Text;
using System.Text.Json;



namespace server.Services;
public class CsvProducer  {

    private readonly IConnectionFactory _connectionFactory;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly CsvConsumer _consumer;
    public int currQueueIndex;
    public CsvProducer(IConnectionFactory connectionFactory, CsvConsumer consumer)
    {
        _connectionFactory = connectionFactory;
        _connection = _connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();
        _consumer = consumer;
        
        int numberOfQueues = 5;
        currQueueIndex = 0; 

        for (int i = 0; i < numberOfQueues; i++){
        _channel.QueueDeclare(queue: $"queue{i}", 
                        durable: false,
                        exclusive: false,
                        arguments: null);
        }
    }

    public void produce(DataModels[] chunk) {{
        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        currQueueIndex = (currQueueIndex == 5) ? 0 : currQueueIndex;
        var message = JsonSerializer.Serialize(chunk);
        var body = Encoding.UTF8.GetBytes(message);
        
        _channel.BasicPublish(exchange: "", routingKey: $"queue{currQueueIndex}", body: body);

        _consumer.Consume(currQueueIndex);

        // currQueueIndex++;

        watch.Stop();
    }}

}


