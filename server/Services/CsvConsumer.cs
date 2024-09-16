using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using MySqlConnector;
using server.Models;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using server.Hubs;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Razor.Language.Intermediate;



namespace server.Services;
public class CsvConsumer
{
     private readonly IConfiguration _configuration;
    private readonly IConnectionFactory _connectionFactory;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    private readonly IHubContext<ProgressHub>? _hubContext;

    private List<Task> insert=new List<Task>();
    private int totalChunksProcessed;
    private int totalChunksExpected;
    private int temp=0;
    public CsvConsumer(IConfiguration configuration,IConnectionFactory connectionFactory, IHubContext<ProgressHub> hubContext)
    {
        _configuration = configuration;

        _connectionFactory = connectionFactory;
        _hubContext = hubContext;
        _connection = _connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();
        int expectChunk=10;
        ResetProgress(expectChunk);

    }
    // Call this when a new upload begins
    public void ResetProgress(int totalChunck)
    {
        totalChunksProcessed = 0;
        totalChunksExpected=totalChunck;
    }
    public async Task Consume(int queueNumber)
    {
        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            var csvRecords = JsonSerializer.Deserialize<List<DataModels>>(message);

            if (csvRecords != null)
            {
                insert.Add(Task.Run(async () => {
                    temp++;
                    await MultipleInsert(csvRecords);
                    await UpdateProgress(temp);
                }));

            }
        };
        await Task.WhenAll(insert.Where(t=>t!=null));
        Console.WriteLine(queueNumber);
        _channel.BasicConsume(queue: $"queue{queueNumber}", autoAck: true, consumer: consumer);
    }
    private async Task UpdateProgress(int e)
    {
        if (_hubContext == null){
            throw new InvalidOperationException("HubContext is not available.");
        }
        totalChunksProcessed++;

        // Calculate percentage (assume 10 chunks)
        var progressPercentage = totalChunksProcessed /(double)totalChunksExpected;

        Console.WriteLine($"Chunk{e} {totalChunksProcessed} processed. Progress: {progressPercentage}%");

        // Send progress to frontend via SignalR
        await _hubContext.Clients.All.SendAsync("ReceiveProgress", progressPercentage*100);
    }
    private async Task MultipleInsert(List<DataModels> csvRecords)
    {
        using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
        var sql = new StringBuilder();
        sql.Append("INSERT INTO user (row_num,email_id, name, country, state, city, telephone_number, address_line_1, address_line_2, date_of_birth, gross_salary_FY2019_20, gross_salary_FY2020_21, gross_salary_FY2021_22, gross_salary_FY2022_23, gross_salary_FY2023_24) VALUES ");

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
        }
        sql.Length--;

        var watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        await connection.OpenAsync();

        Console.WriteLine("Sql complete");
        Console.WriteLine(temp);
        using var command = new MySqlCommand(sql.ToString(), connection);
        await command.ExecuteNonQueryAsync();
        await connection.CloseAsync();
    }

}
