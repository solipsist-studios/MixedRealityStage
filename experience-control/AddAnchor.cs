using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Solipsist.Common;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Solipsist.ExperienceControl
{
    public static class AddAnchor
    {
        [FunctionName("AddAnchor")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "{expid}/addanchor")] HttpRequest req,
            string expid,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string anchorId = req.Query["id"];

            if (string.IsNullOrWhiteSpace(anchorId)) 
            {
                log.LogError("No anchor added: Invalid anchor ID supplied.");
                return new BadRequestResult();
            }

            // Connect to metadata db and query the experience metadata container
            var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);

            return await RunLocal(log, credential, expid, anchorId);
        }

        public static async Task<IActionResult> RunLocal(ILogger log, TokenCredential credential, string expID, string anchorID)
        {
            Microsoft.Azure.Cosmos.Container anchorContainer;

            using CosmosClient cosmosClient = new CosmosClient((await Utilities.GetKeyVaultSecretAsync("CosmosDBConnectionString", credential)).Value);
            anchorContainer = cosmosClient.GetContainer("experiences", expID);

            return await AddAnchorAsync(log, credential, expID, anchorID);
        }

        private static async Task<IActionResult> AddAnchorAsync(ILogger log, TokenCredential credential, string expId, string anchorId)
        {
            // Connect to metadata db and add this experience
            using CosmosClient client = new CosmosClient((await Utilities.GetKeyVaultSecretAsync("CosmosDBConnectionString", credential)).Value);
            var response = await client.GetContainer("experiences", expId).CreateItemAsync(
                new
                {
                    anchorId
                });

            return response.StatusCode == System.Net.HttpStatusCode.OK ? new OkResult() : new UnprocessableEntityResult();
        }
    }
}
