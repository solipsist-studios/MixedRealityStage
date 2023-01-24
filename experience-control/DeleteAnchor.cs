using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Solipsist.Common;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Solipsist.ExperienceControl
{
    public static class DeleteAnchor
    {
        [FunctionName("DeleteAnchor")]
        public static async Task<IActionResult> Run(
           [HttpTrigger(AuthorizationLevel.Function, "post", Route = "{expid}/deleteanchor")] HttpRequest req,
           string expid,
           ILogger log)
        {
            log.LogInformation($"DeleteAnchor function triggered for {expid}");

            string anchorId;
            using (StreamReader reader = new StreamReader(req.Body, Encoding.UTF8))
            {
                anchorId = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(anchorId))
            {
                log.LogError("Anchor not deleted: Invalid anchor ID supplied.");
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
            var response = await client.GetContainer("experiences", expID).DeleteItemAsync<AnchorModel>(anchorID, new PartitionKey(anchorID));

            if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                log.LogError("Failed to delete anchor.  Status code: {0}", response.StatusCode);
            }

            string responseMessage = $"Successfully deleted the anchor: {anchorID}";
            return response.StatusCode == System.Net.HttpStatusCode.NoContent ? new CreatedResult(anchorID, responseMessage) : new BadRequestResult();
        }
    }
}
