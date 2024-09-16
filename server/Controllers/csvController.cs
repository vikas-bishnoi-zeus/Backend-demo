using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Models;
using MySqlConnector;
using System.Text;
using server.Services;
using NuGet.Protocol.Core.Types;

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
                int totalChunck = 0;
                // Process the data in chunks and send to RabbitMQ
                foreach (var chunk in jsonContent.Chunk(10000))
                {
                    totalChunck++;
                    _producer.produce(chunk);
                }
                _producer.ResetProgress(totalChunck);

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
            // Console.WriteLine("Starting update for record with row_num: " + record.row_num);

            // Build the SQL update statement dynamically
            var sql = new StringBuilder("UPDATE USER SET ");
            // Console.WriteLine(sql.ToString());
            var properties = record.GetType().GetProperties();

            // Append each property and its value to the SQL update statement
            foreach (var property in properties)
            {
                if (property.Name == "row_num")
                {
                    continue;
                }
                // Ensure the value is correctly formatted, handling nulls as empty strings
                string value = property.GetValue(record)?.ToString() ?? string.Empty;
                // Properly escape single quotes to prevent SQL injection
                string escapedValue = value.Replace("'", "''");
                sql.Append($"{property.Name}='{escapedValue}',");
                // Console.WriteLine(sql.ToString());
            }
            // Remove the trailing comma from the SQL statement
            sql.Length--;

            // Add the WHERE clause to target the specific record by row number
            sql.Append($" WHERE email_id='{record.email_id}';");

            // Log the generated SQL query for debugging purposes
            // String temp=sql.ToString();
            // Console.WriteLine(temp);

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


        [HttpPost()]
        [Route("BulkUpdate")]
        public async Task<IActionResult> BulkUpdate(DataModels[] records)
        {
            // Validate the incoming array of records
            Console.WriteLine("Bulk Updating");
            Console.WriteLine(records);
            if (records == null || records.Length == 0)
            {
                return BadRequest("No records provided for update.");
            }

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return StatusCode(500, "Database connection string is not configured.");
                }

                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                await using var transaction = await connection.BeginTransactionAsync(); // Start transaction

                var sql = new StringBuilder();

                foreach (var record in records)
                {
                    if (record == null) continue;

                    // Build the SQL update statement for each record
                    var updateSql = new StringBuilder("UPDATE USER SET ");
                    var properties = record.GetType().GetProperties();

                    // Append each property and its value to the SQL update statement
                    foreach (var property in properties)
                    {
                        if (property.Name == "email_id" || property.Name == "row_num")
                        {
                            continue;
                        }
                        string value = property.GetValue(record)?.ToString() ?? string.Empty;
                        string escapedValue = value.Replace("'", "''");
                        updateSql.Append($"{property.Name}='{escapedValue}',");
                    }

                    updateSql.Length--;  // Remove trailing comma
                    updateSql.Append($" WHERE email_id='{record.email_id}';");

                    // Append this update query to the main SQL command
                    sql.Append(updateSql);
                }

                // Execute the combined SQL update in one command
                await using var command = new MySqlCommand(sql.ToString(), connection, transaction);
                var result = await command.ExecuteNonQueryAsync();

                // Commit the transaction
                await transaction.CommitAsync();

                await connection.CloseAsync();

                // Return the result of the bulk update
                return Ok(new { Message = "Bulk update successful", TotalUpdatedRecords = result });
            }
            catch (MySqlException ex)
            {
                // Log the exception and return an InternalServerError response
                Console.WriteLine($"Database error occurred: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the records.");
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
            // Console.WriteLine(headers[0]);
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

        [HttpPost()]
        [Route("DeleteRow")]
        public async Task<IActionResult> DeleteRow([FromBody] string email_id)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return StatusCode(500, "Database connection string is not configured.");
                }

                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                var sql = new StringBuilder("delete from user where email_id=@email_id;");

                // Execute the combined SQL update in one command
                await using var command = new MySqlCommand(sql.ToString(), connection);
                // Add parameters to avoid SQL injection
                command.Parameters.AddWithValue("@email_id", email_id);
                Console.WriteLine(email_id);
                var result = await command.ExecuteNonQueryAsync();

                await connection.CloseAsync();

                // Return the result of the bulk update
                return Ok(new { Message = "Delete successful", email_id });
            }
            catch (MySqlException ex)
            {
                // Log the exception and return an InternalServerError response
                Console.WriteLine($"Database error occurred: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while Deleting the records.");
            }
            catch (Exception ex)
            {
                // Log unexpected exceptions and return an InternalServerError response
                Console.WriteLine($"Unexpected error occurred: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }

        }

        [HttpPost()]
        [Route("FindAndReplace")]
        public async Task<IActionResult> FindAndReplace([FromBody] FindReplaceRequest request)
        {

            if (request == null || string.IsNullOrEmpty(request.FindText) || request.ReplaceText == null)
            {
                return BadRequest("Invalid find and replace parameters.");
            }
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return StatusCode(500, "Database connection string is not configured.");
                }

                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                // Building the SQL query to update all fields containing the FindText
                var query = new StringBuilder();
                query.Append("UPDATE user SET ");
                query.Append("name = REPLACE(name, @FindText, @ReplaceText), ");
                query.Append("country = REPLACE(country, @FindText, @ReplaceText), ");
                query.Append("state = REPLACE(state, @FindText, @ReplaceText), ");
                query.Append("telephone_number = REPLACE(telephone_number, @FindText, @ReplaceText), ");
                query.Append("address_line_1 = REPLACE(address_line_1, @FindText, @ReplaceText), ");
                query.Append("address_line_2 = REPLACE(address_line_2, @FindText, @ReplaceText), ");
                query.Append("date_of_birth = REPLACE(date_of_birth, @FindText, @ReplaceText), ");
                query.Append("gross_salary_FY2019_20 = REPLACE(gross_salary_FY2019_20, @FindText, @ReplaceText), ");
                query.Append("gross_salary_FY2020_21 = REPLACE(gross_salary_FY2020_21, @FindText, @ReplaceText), ");
                query.Append("gross_salary_FY2021_22 = REPLACE(gross_salary_FY2021_22, @FindText, @ReplaceText), ");
                query.Append("gross_salary_FY2022_23 = REPLACE(gross_salary_FY2022_23, @FindText, @ReplaceText), ");
                query.Append("gross_salary_FY2023_24 = REPLACE(gross_salary_FY2023_24, @FindText, @ReplaceText);");

                // Prepare and execute the SQL command
                using var command = new MySqlCommand(query.ToString(), connection);
                command.Parameters.AddWithValue("@FindText", request.FindText);
                command.Parameters.AddWithValue("@ReplaceText", request.ReplaceText);
                var result = await command.ExecuteNonQueryAsync();

                await connection.CloseAsync();

                // Return the result of the bulk update
                return Ok(new { Message = "Find and Replace successful", RowsAffected = result });
            }
            catch (MySqlException ex)
            {
                // Log the exception and return an InternalServerError response
                Console.WriteLine($"Database error occurred: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while Deleting the records.");
            }
            catch (Exception ex)
            {
                // Log unexpected exceptions and return an InternalServerError response
                Console.WriteLine($"Unexpected error occurred: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}