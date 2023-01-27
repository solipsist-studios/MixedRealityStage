using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace Solipsist.ExperienceControl
{
    public static class AddAnchor
    {
        // TODO: Need on-device authentication to enable User-level here.
        [FunctionName("AddAnchor")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "{expid}/addanchor")] HttpRequest req,
            string expid,
            [CosmosDB(databaseName: "experiences", 
                containerName: "{expid}", 
                Connection = "CosmosDbConnectionString")]//, 
                //PartitionKey = "{expid}",
                //CreateIfNotExists = true)]
                out dynamic documentOut,
            ILogger log)
        {
            log.LogInformation($"AddAnchor function triggered for {expid}");

            string anchorData;
            using (StreamReader reader = new StreamReader(req.Body, Encoding.UTF8))
            {
                anchorData = reader.ReadToEnd();
            }

            if (string.IsNullOrWhiteSpace(anchorData)) 
            {
                log.LogError("No anchor added: Invalid anchor ID supplied.");
                documentOut = null;
                return new BadRequestResult();
            }

            AnchorModel anchor = JsonConvert.DeserializeObject<AnchorModel>(anchorData);

            documentOut = new 
            { 
                id = anchor.id,
                data = anchor.data
            };

            string responseMessage = $"Successfully added the anchor: {anchor.id}";
            return new CreatedResult(anchor.id.ToString(), responseMessage);
        }
    }
}
