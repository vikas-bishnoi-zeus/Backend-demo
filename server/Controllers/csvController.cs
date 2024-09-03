using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Models;
using MySqlConnector;
using System.Text;
using server.Services;

namespace server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class csvController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        private readonly CsvProducer _producer;

        // Constructor to inject configuration and producer dependencies
        public csvController(IConfiguration configuration, CsvProducer producer)
        {
            _configuration = configuration;
            _producer = producer;
        }

        // Endpoint to get items from the database with lazy loading
        [HttpPost]
        [Route("GetItems")]
        public async Task<IActionResult> GetItems([FromBody] DataRange range)
        {
            if (range == null || range.limit <= 0 || range.offset < 0)
            {
                return BadRequest("Invalid range specified.");
            }
            var items = new List<List<string>>();  // List to store the retrieved data

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return StatusCode(500, "Database connection string is not configured.");
            }
            try
            {
                // Create and open a new database connection
                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();


                // SQL command to fetch data with offset and limit
                var sqlQuery = "SELECT * FROM user ORDER BY row_num LIMIT @offset, @limit;";
                using var command = new MySqlCommand(sqlQuery, connection);

                // Add parameters to avoid SQL injection
                command.Parameters.AddWithValue("@offset", range.offset);
                command.Parameters.AddWithValue("@limit", range.limit);

                // Execute the command and read the data
                using var reader = await command.ExecuteReaderAsync();

                // Reading each row and adding to the items list
                while (await reader.ReadAsync())
                {
                    var value = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        // Handle null values safely
                        string cell = reader.GetValue(i).ToString() ?? string.Empty;
                        value.Add(reader.IsDBNull(i) ? string.Empty : cell);

                    }
                    items.Add(value);
                }
                Console.WriteLine("get Request");

            }
            catch (Exception ex)
            {
                // Log the error and return an appropriate response
                Console.WriteLine($"Error retrieving items: {ex.Message}");
                return StatusCode(500, "An error occurred while retrieving data.");
            }


            return Ok(items);
        }

        [HttpPost]
        [Route("UploadCsv")]
        public async Task<IActionResult> UploadCsv(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("File not found or is empty.");
            }
            List<DataModels> jsonContent;
            try
            {
                // Read the CSV file content
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                var csvContent = Encoding.UTF8.GetString(stream.ToArray());

                // Convert CSV content to a list of DataModels
                jsonContent = ConvertStringToJson(csvContent);
            }
            catch (Exception ex)
            {
                // Log and return an error if something goes wrong during file processing
                Console.WriteLine($"Error processing file: {ex.Message}");
                return StatusCode(500, "Error processing the CSV file.");
            }

            try
            {
                // Process the data in chunks and send to RabbitMQ
                foreach (var chunk in jsonContent.Chunk(10000))
                {
                    await _producer.produce(chunk);
                }

                Console.WriteLine("Data added to RabbitMQ successfully.");
                return Ok("CSV data added to RabbitMQ");
            }
            catch (Exception ex)
            {
                // Log and return an error if something goes wrong during the RabbitMQ processing
                Console.WriteLine($"Error adding data to RabbitMQ: {ex.Message}");
                return StatusCode(500, "Error sending data to RabbitMQ.");
            }
        }


        [HttpPost()]
        [Route("UpdateRecord")]
        public async Task<IActionResult> UpdateRecord([FromBody] DataModels record)
        {
            // Validate the incoming record
            if (record == null)
            {
                return BadRequest("Invalid record data.");
            }
            // Log the start of the update process
            Console.WriteLine("Starting update for record with row_num: " + record.row_num);

            // Build the SQL update statement dynamically
            var sql = new StringBuilder("UPDATE USER SET ");

            var properties = record.GetType().GetProperties();

            // Append each property and its value to the SQL update statement
            foreach (var property in properties)
            {
                // Ensure the value is correctly formatted, handling nulls as empty strings
                string value = property.GetValue(record)?.ToString() ?? string.Empty;
                // Properly escape single quotes to prevent SQL injection
                string escapedValue = value.Replace("'", "''");
                sql.Append($"{property.Name}='{escapedValue}',");
            }
            // Remove the trailing comma from the SQL statement
            sql.Length--;

            // Add the WHERE clause to target the specific record by row number
            sql.Append($" WHERE row_num={record.row_num};");

            // Log the generated SQL query for debugging purposes
            Console.WriteLine(sql.ToString());

            // Execute the update query within a try-catch block for error handling
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return StatusCode(500, "Database connection string is not configured.");
                }
                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                await using var command = new MySqlCommand(sql.ToString(), connection);

                // Execute the query and get the number of affected rows
                var result = await command.ExecuteNonQueryAsync();

                // Close the connection explicitly (optional due to using statement)
                await connection.CloseAsync();

                // Check if any rows were affected, indicating a successful update
                if (result == 0)
                {
                    // Log the failure and return a BadRequest response
                    Console.WriteLine("Update failed: No records were affected.");
                    return BadRequest("Update failed: No records were affected.");
                }

                // Log the success and return an Ok response with the number of affected rows
                Console.WriteLine($"Update successful: {result} record(s) updated.");
                return Ok(new { Message = "Update successful", RowsAffected = result });
            }
            catch (MySqlException ex)
            {
                // Log the exception and return an InternalServerError response
                Console.WriteLine($"Database error occurred: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the record.");
            }
            catch (Exception ex)
            {
                // Log unexpected exceptions and return an InternalServerError response
                Console.WriteLine($"Unexpected error occurred: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }


        private List<DataModels> ConvertStringToJson(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                throw new ArgumentException("Content cannot be null or empty", nameof(content));
            }
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

    }
}