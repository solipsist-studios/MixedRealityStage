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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "expc/{ownerID}")] HttpRequest req,
            string ownerID,
            ILogger log)
        {
            // Input parameters are obtained from the route
            log.LogInformation($"GetExperiences HTTP function triggered for user {ownerID}");

            // Get cconnection strings
            string storageConnectionString = Utilities.GetBlobStorageConnectionString("ExperienceStorage");
            string cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDBConnectionString");

            return await RunLocal(log, storageConnectionString, cosmosConnectionString, ownerID);
        }

        public static async Task<IActionResult> RunLocal(ILogger log, string storageConnectionString, string cosmosConnectionString, string ownerID)
        {
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
            log.LogInformation(experienceResult.ToString());

            return new OkObjectResult(experienceResult);
        }
    }
}
