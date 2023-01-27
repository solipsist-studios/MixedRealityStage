using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Solipsist.ExperienceControl
{
    public static class DeleteAnchor
    {
        [FunctionName("DeleteAnchor")]
        public static async Task<IActionResult> Run(
           [HttpTrigger(AuthorizationLevel.Function, "put", Route = "{expid}/deleteanchor")] HttpRequest req,
           string expid,
           [CosmosDB(databaseName: "experiences", 
                containerName: "{expid}", 
                Connection = "CosmosDbConnectionString")]//,
                //PartitionKey = "{expid}")]
                CosmosClient cosmosClient,
           ILogger log)
        {
            log.LogInformation($"DeleteAnchor function triggered for {expid}");

            string anchorData;
            using (StreamReader reader = new StreamReader(req.Body, Encoding.UTF8))
            {
                anchorData = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(anchorData))
            {
                log.LogError("Anchor not deleted: Invalid anchor ID supplied.");
                return new BadRequestResult();
            }

            AnchorModel anchor = JsonConvert.DeserializeObject<AnchorModel>(anchorData);
            var expContainer = cosmosClient.GetContainer("experiences", expid);
            string anchorID = anchor.id.ToString();
            var response = await expContainer.DeleteItemAsync<AnchorModel>(anchorID, new PartitionKey(anchorID));

            if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                log.LogError("Failed to delete anchor.  Status code: {0}", response.StatusCode);
                return new BadRequestResult();
            }

            string responseMessage = $"Successfully deleted the anchor: {anchorData}";
            return new NoContentResult();
        }
    }
}
