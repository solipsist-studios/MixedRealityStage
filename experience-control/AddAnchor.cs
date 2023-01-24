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
        // TODO: Need on-device authentication to enable User-level here.
        [FunctionName("AddAnchor")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "{expid}/addanchor")] HttpRequest req,
            string expid,
            ILogger log)
        {
            log.LogInformation($"AddAnchor function triggered for {expid}");

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
            // Connect to metadata db and add this experience
            using CosmosClient client = new CosmosClient((await Utilities.GetKeyVaultSecretAsync("CosmosDBConnectionString", credential)).Value);
            var response = await client.GetContainer("experiences", expID).CreateItemAsync(
                new
                {
                    id = anchorID
                }, 
                new PartitionKey(anchorID));

            if (response.StatusCode != System.Net.HttpStatusCode.Created)
            {
                log.LogError("Failed to add anchor.  Status code: {0}", response.StatusCode);
            }

            string responseMessage = $"Successfully added the anchor: {anchorID}";
            return response.StatusCode == System.Net.HttpStatusCode.Created ? new CreatedResult(anchorID, responseMessage) : new BadRequestResult();
        }
    }
}
