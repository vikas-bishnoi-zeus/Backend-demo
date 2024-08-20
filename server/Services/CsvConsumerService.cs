using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using MySqlConnector;
using server.Models;

public class CsvConsumerService
{
    private readonly IConfiguration _configuration;

    public CsvConsumerService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Start()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: "csvQueue",
                             durable: true,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        long time=0;
        
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            var csvRecords = JsonSerializer.Deserialize<List<DataModels>>(message);

            if (csvRecords != null)
            {
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                await MultipleInsert(csvRecords);
                watch.Stop();
                Console.WriteLine(watch.ElapsedMilliseconds);
                time+=watch.ElapsedMilliseconds;
                Console.WriteLine(time);

            }
        };

        channel.BasicConsume(queue: "csvQueue",
                             autoAck: true,
                             consumer: consumer);

        Console.WriteLine(" Press [enter] to exit.");
        Console.ReadLine();
    }

    private async Task MultipleInsert(List<DataModels> csvRecords)
    {
        using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")!);

        await connection.OpenAsync();
        var sql = new StringBuilder();
        sql.Append("INSERT INTO user (email_id, name, country, state, city, telephone_number, address_line_1, address_line_2, date_of_birth, gross_salary_FY2019_20, gross_salary_FY2020_21, gross_salary_FY2021_22, gross_salary_FY2022_23, gross_salary_FY2023_24) VALUES ");

        foreach (var record in csvRecords)
        {
            sql.Append($"('{record.email_id}', '{record.name}', '{record.country}', '{record.state}', '{record.city}', '{record.telephone_number}', '{record.address_line_1}', '{record.address_line_2}', '{record.date_of_birth}', {record.gross_salary_FY2019_20}, {record.gross_salary_FY2020_21}, {record.gross_salary_FY2021_22}, {record.gross_salary_FY2022_23}, {record.gross_salary_FY2023_24}),");
        }
        sql.Length--;

        using var command = new MySqlCommand(sql.ToString(), connection);
        await command.ExecuteNonQueryAsync();
    }
}
