using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Solipsist.ExperienceCatalog
{
    public static class GetExperiences
    {
        [FunctionName("GetExperiences")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "expc/{ownerID}")] HttpRequest req,
            ILogger log,
            [CosmosDB(databaseName: "experiences", 
                collectionName: "metadata", 
                ConnectionStringSetting = "CosmosDBConnectionString",
                SqlQuery = "select * from metadata m where m.ownerID = {ownerID}")]
            IEnumerable<ExperienceMetadata> experiences,
            [Queue("outqueue"), StorageAccount("AzureWebJobsStorage")] ICollector<string> msg)
        {
            log.LogInformation("GetExperiences HTTP function triggered.");
            
            JsonResult experienceResult = new JsonResult(experiences);
            log.LogInformation(experienceResult.ToString());

            return new OkObjectResult(experienceResult);
        }
    }

    
}
