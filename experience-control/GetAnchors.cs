using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace Solipsist.ExperienceControl
{
    internal class GetAnchors
    {
        [FunctionName("GetAnchors")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "{expid}/getanchors")] HttpRequest req,
            string expid,
            [CosmosDB(
                databaseName: "experiences", 
                containerName: "{expid}", 
                Connection = "CosmosDbConnectionString",
                //PartitionKey = "{expid}",
                SqlQuery = "select * from c")]
                IEnumerable<AnchorModel> anchors,
            ILogger log)
        {
            log.LogInformation($"GetAnchorIDs function triggered for {expid}");

            JsonResult anchorsResult = new JsonResult(anchors.ToList());
            return new OkObjectResult(anchorsResult);
        }
    }
}
