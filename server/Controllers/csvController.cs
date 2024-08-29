using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Models;
using MySqlConnector;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Swashbuckle.AspNetCore.SwaggerUI;
using server.Services;



namespace server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class csvController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        // private MySqlConnection sqlconnection;

        private readonly CsvProducer _producer;

        public csvController(IConfiguration configuration, CsvProducer producer)
        {
            _configuration = configuration;
            // sqlconnection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
            _producer = producer;
        }

        [HttpPost]
        [Route("GetItems")]
        public async Task<IActionResult> GetItems([FromBody] DataRange range)
        {
            var items = new List<List<string>>();  // Assuming the data type is string; adjust as necessary
            // items.Add("1");
            using (var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")!))
            {
                await connection.OpenAsync();
                using var command = new MySqlCommand($"SELECT * FROM user order By row_num LIMIT {range.offset},{range.limit};", connection);
                using var reader = await command.ExecuteReaderAsync();
                // Console.WriteLine(range.offset);
                // Console.WriteLine(range.limit);
                while (await reader.ReadAsync())
                {

                    // Console.WriteLine(reader.FieldCount);
                    var value = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        String? cur = reader.GetValue(i).ToString();
                        value.Add(cur);
                    }
                    items.Add(value);
                }
                Console.WriteLine("get");

            }


            return Ok(items);
        }

        [HttpPost]
        [Route("uploadCsv")]
        public async Task<IActionResult> uploadCsv(IFormFile file)
        {
            // Console.WriteLine("uploading backend");
            if (file == null || file.Length == 0)
            {
                return BadRequest("File not found!");
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var csvContent = Encoding.UTF8.GetString(stream.ToArray());
            List<DataModels> jsonContent = ConverStringToJson(csvContent);

            Console.WriteLine("start time " + ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds());
            List<Task> upload = new List<Task>();
            foreach (var chunk in jsonContent.Chunk(10000))
            {
                // Console.WriteLine("Radha Krishna");
                // upload.Add(Task.Run(()=>_producer.produce(chunk)));
                await _producer.produce(chunk);
            }
            // await Task.WhenAll(upload.Where(t=>t!=null));
            Console.WriteLine("Adding to mq");
            return Ok("CSV data added to RabbitMQ");
        }

        [HttpPost()]
        [Route("updateRecord")]
        public async Task<IActionResult> updateRecord([FromBody] DataModels record)
        {
            Console.WriteLine("Updating");
            var sql = new StringBuilder("UPDATE USER SET ");
            var properties = record.GetType().GetProperties();
            foreach (var field in properties)
            {
                sql.Append($"{field.Name}='{field.GetValue(record)}',");
            }
            sql.Length--;
            sql.Append($" WHERE row_num={record.row_num};");
            Console.WriteLine(sql.ToString());
            using (var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")!))
            {
                await connection.OpenAsync();
                using var command = new MySqlCommand(sql.ToString(), connection);
                var result = await command.ExecuteNonQueryAsync();
                await connection.CloseAsync();
                if (result == 0) { 
                    return BadRequest(); 
                }
                else{
                    return Ok(result);
                }
            }
        }


        private List<DataModels> ConverStringToJson(string content)
        {
            int rowNo = 0;
            var line = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var headers = line[0].Split(',');
            Console.WriteLine(headers[0]);
            var csvData = new List<DataModels>();
            foreach (var l in line)
            {
                var values = l.Split(',');
                var row = new DataModels
                {
                    row_num = rowNo,
                    email_id = values[0],
                    name = values[1],
                    country = values[2],
                    state = values[3],
                    city = values[4],
                    telephone_number = values[5],
                    address_line_1 = values[6],
                    address_line_2 = values[7],
                    date_of_birth = values[8],
                    gross_salary_FY2019_20 = values[9],
                    gross_salary_FY2020_21 = values[10],
                    gross_salary_FY2021_22 = values[11],
                    gross_salary_FY2022_23 = values[12],
                    gross_salary_FY2023_24 = values[13],
                };
                csvData.Add(row);
                rowNo++;
            }
            return csvData;
        }



        // [HttpPost]
        // [Route("handleCsv")]
        // public async Task<IActionResult> HandleCsv(IFormFile file)
        // {
        //     var watch = new System.Diagnostics.Stopwatch();
        //     watch.Start();
        //     if (file == null || file.Length == 0)
        //     {
        //         return BadRequest("File not found!!");
        //     }
        //     using var stream = new MemoryStream();
        //     await file.CopyToAsync(stream);
        //     var csvContent = Encoding.UTF8.GetString(stream.ToArray());
        //     List<DataModels> jsonContent = ConverStringToJson(csvContent);
        //     await MultipleInsert(jsonContent);
        //     watch.Stop();
        //     Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
        //     return Ok("Csv data added to MySQL");
        // }

        // private List<DataModels> ConverStringToJson(string content)
        // {
        //     var line = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        //     var headers = line[0].Split(',');
        //     var csvData = new List<DataModels>();
        //     foreach (var l in line.Skip(1))
        //     {
        //         var values = l.Split(',');
        //         var row = new DataModels
        //         {
        //             email_id = values[0],
        //             name = values[1],
        //             country = values[2],
        //             state = values[3],
        //             city = values[4],
        //             telephone_number = values[5],
        //             address_line_1 = values[6],
        //             address_line_2 = values[7],
        //             date_of_birth = values[8],
        //             gross_salary_FY2019_20 = values[9],
        //             gross_salary_FY2020_21 = values[10],
        //             gross_salary_FY2021_22 = values[11],
        //             gross_salary_FY2022_23 = values[12],
        //             gross_salary_FY2023_24 = values[13],
        //         };
        //         csvData.Add(row);
        //     }
        //     return csvData;
        // }

        // private async Task MultipleInsert(List<DataModels> csvRecords)
        // {
        //     await connection.OpenAsync();
        //     var sql = new StringBuilder("TRUNCATE TABLE user;");
        //     // sql.Append("INSERT INTO user (email_id, name) VALUES ");
        //     sql.Append("INSERT INTO user (email_id, name, country, state,city, telephone_number, address_line_1, address_line_2, date_of_birth, gross_salary_FY2019_20, gross_salary_FY2020_21, gross_salary_FY2021_22, gross_salary_FY2022_23, gross_salary_FY2023_24) VALUES ");

        //     foreach (var record in csvRecords)
        //     {
        //         sql.Append($"('{record.email_id}', '{record.name}', '{record.country}', '{record.state}','{record.city}', '{record.telephone_number}', '{record.address_line_1}', '{record.address_line_2}', '{record.date_of_birth}', {record.gross_salary_FY2019_20}, {record.gross_salary_FY2020_21}, {record.gross_salary_FY2021_22}, {record.gross_salary_FY2022_23}, {record.gross_salary_FY2023_24}),");
        //     }
        //     sql.Length--;
        //     // Console.WriteLine(sql.ToString());
        //     using var command = new MySqlCommand(sql.ToString(), connection);
        //     await command.ExecuteNonQueryAsync();
        // }



    }
}