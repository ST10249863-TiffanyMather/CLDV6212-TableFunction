using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace ABCRetail_TableStorageFunction
{
    public class TableStorageFunction
    {
        private readonly TableClient _tableClient;
        private readonly ILogger<TableStorageFunction> _logger; 

        public TableStorageFunction(ILogger<TableStorageFunction> logger)
        {
            _logger = logger; 
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            var serviceClient = new TableServiceClient(connectionString);
            _tableClient = serviceClient.GetTableClient("Products");
            _tableClient.CreateIfNotExists();
        }

        //****************
        //Code Attribution
        //The following coode was taken from StackOverflow:
        //Author: Jose Roberto
        //Link: https://stackoverflow.com/questions/44243901/azure-functions-table-storage-trigger-with-azure-functions
        //****************

        [Function("AddProductFunction")]
        public async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("AddProductFunction processed a request for a product");

            //read request body to get product data
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Product product = JsonConvert.DeserializeObject<Product>(requestBody);

            if (product == null)
            {
                return new BadRequestObjectResult("Invalid product data.");
            }

            //set PartitionKey and RowKey 
            product.PartitionKey = "ProductsPartition";
            product.RowKey = Guid.NewGuid().ToString();

            //fetch all products to find the maximum ProductId
            Pageable<Product> existingProducts = _tableClient.Query<Product>(p => p.PartitionKey == "ProductsPartition");
            int maxProductId = existingProducts.Any() ? existingProducts.Max(p => p.ProductId) : 0;

            product.ProductId = maxProductId + 1;

            //add product to table storage
            try
            {
                await _tableClient.AddEntityAsync(product);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to add product to table storage: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            string responseMessage = $"Product {product.ProductName} added successfully with ProductId {product.ProductId}.";
            return new OkObjectResult(responseMessage);
        }
    }

    // define Product class 
    public class Product : ITableEntity
    {
        [Key]
        public int ProductId { get; set; }

        public string? ProductName { get; set; }

        public string? Description { get; set; }

        public string? Category { get; set; }

        public double? Price { get; set; }

        public int? StockQuantity { get; set; }

        public string? ImageURL { get; set; }

        //required by ITableEntity - ITableEntity properties 
        public string? PartitionKey { get; set; }
        public string? RowKey { get; set; }
        public ETag ETag { get; set; } = ETag.All;
        public DateTimeOffset? Timestamp { get; set; }
    }
}