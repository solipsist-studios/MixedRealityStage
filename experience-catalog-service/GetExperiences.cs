using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Solipsist.ExperienceCatalog
{
    public static class GetExperiences
    {
        [FunctionName("GetExperiences")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "expc/list")] HttpRequest req,
            ILogger log)
        {
            // Get query strings
            string storageConnectionString = req.Query["storageconnectionstring"];
            string cosmosConnectionString = req.Query["cosmosconnectionstring"];
            string ownerID = req.Query["ownerid"];

            // Input parameters are obtained from the route
            log.LogInformation($"GetExperiences HTTP function triggered for user {ownerID}");
            
            return await RunLocal(log, storageConnectionString, cosmosConnectionString, ownerID);
        }

        public static async Task<IActionResult> RunLocal(ILogger log, string? storageConnectionString, string? cosmosConnectionString, string ownerID)
        {
            storageConnectionString = storageConnectionString ?? Utilities.GetBlobStorageConnectionString("ExperienceStorage");
            cosmosConnectionString = cosmosConnectionString ?? Environment.GetEnvironmentVariable("CosmosDBConnectionString");

            // Connect to metadata db and query the experience metadata container
            using CosmosClient client = new(connectionString: cosmosConnectionString);
            var container = client.GetContainer("experiences", "metadata");

            QueryDefinition queryDefinition = new QueryDefinition(
                "select * from metadata m where m.ownerID = @ownerID")
                .WithParameter("@ownerID", ownerID);
            FeedIterator<ExperienceMetadata> resultSet = container.GetItemQueryIterator<ExperienceMetadata>(queryDefinition);

            // Format results
            var experiences = await resultSet.ToAsyncEnumerable().ToListAsync();
            JsonResult experienceResult = new JsonResult(experiences);

            return new OkObjectResult(experienceResult);
        }
    }
}
