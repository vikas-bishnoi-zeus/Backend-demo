using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Models;
using MySqlConnector;



using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Data;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.Elfie.Model.Strings;
using System.Text;



namespace server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class csvController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private MySqlConnection connection;

        public csvController(IConfiguration configuration)
        {
            _configuration = configuration;
            connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
        }

        [HttpGet]
        public async Task<IActionResult> GetItems()
        {
            var items = new List<string>();  // Assuming the data type is string; adjust as necessary
            // items.Add("1");
            await connection.OpenAsync();

            using var command = new MySqlCommand("SELECT * FROM user LIMIT 1000;", connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var value = reader.GetValue(1).ToString();  // Adjust indexing and type as per your actual table schema
                // Console.WriteLine(value);
                items.Add(value);
            }

            return Ok(items);
        }        

        [HttpPost]
        [Route("handleCsv")]
        public async Task<IActionResult> HandleCsv(IFormFile file){
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            if(file == null || file.Length == 0){
                return BadRequest("File not found!!");
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var csvContent = Encoding.UTF8.GetString(stream.ToArray());
            List<DataModels> jsonContent = ConverStringToJson(csvContent);
            await MultipleInsert(jsonContent);
            watch.Stop();
            Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
            return Ok("Csv data added to MySQL");
        }

        private List<DataModels> ConverStringToJson(string content){
            var line = content.Split(new[]{'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
            var headers = line[0].Split(',');
            var csvData = new List<DataModels>();
            foreach (var l in line.Skip(1))
            {
                var values = l.Split(',');
                var row = new DataModels{
                    email_id=values[0],
                    name = values[1],
                    country=values[2], 
                    state=values[3], 
                    city=values[4],
                    telephone_number =values[5],
                    address_line_1=values[6],
                    address_line_2=values[7],
                    date_of_birth=values[8],
                    gross_salary_FY2019_20=values[9],
                    gross_salary_FY2020_21=values[10],
                    gross_salary_FY2021_22=values[11],
                    gross_salary_FY2022_23=values[12],
                    gross_salary_FY2023_24=values[13],
                };
                csvData.Add(row);
            }
            return csvData;
        }

        private async Task MultipleInsert(List<DataModels> csvRecords){
            await connection.OpenAsync();
            var sql = new StringBuilder("TRUNCATE TABLE user;");
            // sql.Append("INSERT INTO user (email_id, name) VALUES ");
            sql.Append("INSERT INTO user (email_id, name, country, state,city, telephone_number, address_line_1, address_line_2, date_of_birth, gross_salary_FY2019_20, gross_salary_FY2020_21, gross_salary_FY2021_22, gross_salary_FY2022_23, gross_salary_FY2023_24) VALUES ");

            foreach (var record in csvRecords)
            {
                sql.Append($"('{record.email_id}', '{record.name}', '{record.country}', '{record.state}','{record.city}', '{record.telephone_number}', '{record.address_line_1}', '{record.address_line_2}', '{record.date_of_birth}', {record.gross_salary_FY2019_20}, {record.gross_salary_FY2020_21}, {record.gross_salary_FY2021_22}, {record.gross_salary_FY2022_23}, {record.gross_salary_FY2023_24}),");
            }
            sql.Length--;
            // Console.WriteLine(sql.ToString());
            using var command = new MySqlCommand(sql.ToString(), connection);
            await command.ExecuteNonQueryAsync();
        }



    }
}