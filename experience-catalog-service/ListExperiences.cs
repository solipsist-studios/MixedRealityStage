using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Solipsist.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Solipsist.ExperienceCatalog
{
    public static class ListExperiences
    {
        [FunctionName("ListExperiences")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "list", Route = "expc/list")] HttpRequest req,
            ILogger log)
        {
            // Connect to metadata db and query the experience metadata container
            var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);

            string resourceId = "https://solipsiststudios.onmicrosoft.com/experience-catalog";
            string[] scopes = new string[]
            {
                $"{resourceId}/experiences.read"
            };

            var jsonToken = Utilities.GetTokenFromConfidentialClient(log, credential, scopes);
            string ownerID = Utilities.GetUserIdentityFromToken(log, jsonToken);

            // Input parameters are obtained from the route
            log.LogInformation($"GetExperiences HTTP function triggered for user {ownerID}");

            return await RunLocal(log, credential, ownerID);
        }

        public static async Task<IActionResult> RunLocal(ILogger log, TokenCredential credential, string ownerID)
        {
            using CosmosClient client = new CosmosClient((await Utilities.GetKeyVaultSecretAsync("CosmosDBConnectionString", credential)).Value);
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
