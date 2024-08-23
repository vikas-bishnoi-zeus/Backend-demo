using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using MySqlConnector;
using server.Models;
using System.Threading.Channels;

namespace server.Services;
public class CsvConsumer
{
     private readonly IConfiguration _configuration;
    private readonly IConnectionFactory _connectionFactory;
    private readonly IConnection _connection;
    private readonly string? _sqlConnectionString;
    private readonly IModel _channel;

    long timeExc=0;
    public CsvConsumer(IConfiguration configuration,IConnectionFactory connectionFactory)
    {
        _configuration = configuration;
        _sqlConnectionString = _configuration.GetConnectionString("DefaultConnection");

        _connectionFactory = connectionFactory;
        _connection = _connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();

        int numberOfQueues = 5;

        for (int i = 0; i < numberOfQueues; i++)
        {
            _channel.QueueDeclare(queue: $"queue{i}",
                            durable: false,
                            exclusive: false,
                            arguments: null);
        }
    }

    public void Consume(int queueNumber)
    {

        var consumer = new EventingBasicConsumer(_channel);
        long time=0;
        
        consumer.Received +=  (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            var csvRecords = JsonSerializer.Deserialize<List<DataModels>>(message);

            if (csvRecords != null)
            {
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                Task.Run(async () => {
                    MultipleInsert(csvRecords);
                });
                watch.Stop();
                // Console.WriteLine(watch.ElapsedMilliseconds);
                time+=watch.ElapsedMilliseconds;
                Console.WriteLine($"total time:-{time}");

            }
        };

        _channel.BasicConsume(queue: $"queue{queueNumber}", autoAck: true, consumer: consumer);
    }

    private async void MultipleInsert(List<DataModels> csvRecords)
    {
        using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
        var sql = new StringBuilder();
        sql.Append("INSERT INTO user (email_id, name, country, state, city, telephone_number, address_line_1, address_line_2, date_of_birth, gross_salary_FY2019_20, gross_salary_FY2020_21, gross_salary_FY2021_22, gross_salary_FY2022_23, gross_salary_FY2023_24) VALUES ");

        foreach (var record in csvRecords)
        {
            sql.Append("(");
            var properties = record.GetType().GetProperties();
            foreach (var field in properties)
            {
                sql.Append($"'{field.GetValue(record)}',");
            }
            sql.Length--;
            sql.Append("),");
            //sql.Append($"('{record.email_id}', '{record.name}', '{record.country}', '{record.state}', '{record.city}', '{record.telephone_number}', '{record.address_line_1}', '{record.address_line_2}', '{record.date_of_birth}', {record.gross_salary_FY2019_20}, {record.gross_salary_FY2020_21}, {record.gross_salary_FY2021_22}, {record.gross_salary_FY2022_23}, {record.gross_salary_FY2023_24+2}),");
        }
        sql.Length--;

        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        await connection.OpenAsync();

        // Console.WriteLine(sql.ToString());
        using var command = new MySqlCommand(sql.ToString(), connection);
        await command.ExecuteNonQueryAsync();
        await connection.CloseAsync();
        watch.Stop();
        timeExc+=watch.ElapsedMilliseconds;
                    Console.WriteLine("end time " +( ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds()+3000));
    }

}
