using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Solipsist.ExperienceCatalog
{
    public static class AddExperience
    {
        [FunctionName("AddExperience")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expc")] HttpRequest req,
            ILogger log,
            [CosmosDB(databaseName: "experiences", 
                collectionName: "metadata", 
                ConnectionStringSetting = "CosmosDBConnectionString")]
            IAsyncCollector<dynamic> db,
            [Queue("outqueue"), StorageAccount("AzureWebJobsStorage")] ICollector<string> msg)
        {
            log.LogInformation("AddExperience HTTP function triggered.");

            // Try getting parameters from query string
            string experienceName = req.Query["name"];
            string ownerID = req.Query["ownerID"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            // Fall back to request body if not found in query string
            experienceName = experienceName ?? data?.name;
            ownerID = ownerID ?? data?.ownerID;

            if (experienceName != null && ownerID != null) 
            {
                string experienceID = System.Guid.NewGuid().ToString();

                // Add a JSON document to the output container.
                await db.AddAsync(new
                {
                    // create a random ID
                    id = experienceID,
                    ownerID = ownerID,
                    name = experienceName,
                });

                string responseMessage = $"Successfully created the experience {experienceName}";
            
                return new CreatedResult(experienceID, responseMessage);
            }

            return new BadRequestResult();
        }
    }

    
}
