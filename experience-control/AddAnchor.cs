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
using System.IO;
using System.Text;

namespace Solipsist.ExperienceControl
{
    public static class AddAnchor
    {
        [FunctionName("AddAnchor")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.User, "post", Route = "{expid}/addanchor")] HttpRequest req,
            string expid,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string anchorId;
            using (StreamReader reader = new StreamReader(req.Body, Encoding.UTF8))
            {
                anchorId = await reader.ReadToEndAsync();
            }

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
                    id = anchorId
                });

            if (response.StatusCode != System.Net.HttpStatusCode.Created)
            {
                log.LogError("Failed to add anchor.  Status code:\n{0}", response.StatusCode);
            }

            string responseMessage = $"Successfully added the anchor: {anchorId}";
            return response.StatusCode == System.Net.HttpStatusCode.Created ? new CreatedResult(anchorId, responseMessage) : new BadRequestResult();
        }
    }
}
