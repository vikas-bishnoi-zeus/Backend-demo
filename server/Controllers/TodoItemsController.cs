using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
// using server.Models;
using MySqlConnector;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;
using Microsoft.CodeAnalysis.CSharp.Syntax;


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

    }
}