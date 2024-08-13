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
    public class TodoItemController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private MySqlConnection connection;

        public TodoItemController(IConfiguration configuration)
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

            using var command = new MySqlCommand("SELECT * FROM user;", connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var value = reader.GetValue(1).ToString();  // Adjust indexing and type as per your actual table schema
                Console.WriteLine(value);
                items.Add(value);
            }

            return Ok(items);
        }
    // [HttpPost]
    // public async Task<IActionResult> PostTodoItem([FromBody] TodoItem newTodoItem)
    // {
    //     if (newTodoItem == null)
    //     {
    //         return BadRequest("TodoItem is null.");
    //     }

    //     using var connection = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")!);
    //     await connection.OpenAsync();

    //     // Insert the new todo item into the database
    //     using var command = new MySqlCommand("INSERT INTO Id (id) VALUES (@id);", connection);
    //     command.Parameters.AddWithValue("@id", newTodoItem.Name);
    //     await command.ExecuteNonQueryAsync();

    //     return CreatedAtAction(nameof(PostTodoItem), new { id = newTodoItem.Id }, newTodoItem);
    // }

        // [HttpGet("{id}")]
        // public async Task<ActionResult<TodoItem>> GetTodoItem(long id)
        //     {
        //         await connection.OpenAsync();
        //           using var command = new MySqlCommand("insert into Id (id) values (1)", connection);

        //         if (todoItem == null)
        //         {
        //             return NotFound();
        //         }

        //         return todoItem;
        //     }


        

        [HttpPost]
        [Route("handleCsv")]
        public async Task<IActionResult> HandleCsv(IFormFile file){
            if(file == null || file.Length == 0){
                return BadRequest("File not found!!");
            }
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var csvContent = Encoding.UTF8.GetString(stream.ToArray());
            List<TodoItem> jsonContent = ConverStringToJson(csvContent);
            await MultipleInsert(jsonContent);
            return Ok("Csv data added to MySQL");
        }

        private List<TodoItem> ConverStringToJson(string content){
            var line = content.Split(new[]{'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
            var headers = line[0].Split(',');
            var csvData = new List<TodoItem>();
            foreach (var l in line.Skip(1))
            {
                var values = l.Split(',');
                var row = new TodoItem{
                    Id = Convert.ToInt32(values[0]),
                    Name = values[1],
                
                };
                csvData.Add(row);
            }
            return csvData;
        }

        private async Task MultipleInsert(List<TodoItem> csvRecords){
            await connection.OpenAsync();
            var sql = new StringBuilder();
            sql.Append("INSERT INTO user (Id, FirstName, LastName , Username , Password) VALUES");
            foreach (var record in csvRecords)
            {
                sql.Append($"({record.Id}, '{record.Name}','{record.Name}','{record.Name}','{record.Name}'),");
            }
            sql.Length--;
            Console.WriteLine(sql.ToString());
            using var command = new MySqlCommand(sql.ToString(), connection);
            await command.ExecuteNonQueryAsync();
        }



    }
}