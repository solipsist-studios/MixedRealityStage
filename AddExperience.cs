using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Solipsist.ExperienceCatalog
{
    [StorageAccount("solipsistexperiencecatal")]
    public static class AddExperience
    {
        [FunctionName("AddExperience")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "expc")] HttpRequest req,
            ILogger log,
            [Queue("outqueue"), StorageAccount("AzureWebJobsStorage")] ICollector<string> msg)
        {
            log.LogInformation("AddExperience HTTP function triggered.");

            // Try getting parameters from query string
            // TODO: make this more robust
            string experienceName = req.Query["name"];
            string ownerID = req.Query["ownerID"];

            // Validate inputs
            if (experienceName == null ||  ownerID == null) 
            {
                return new BadRequestResult();
            }

            // TODO: return a failure if an experience with the same name already exists
                
            // Upload blob
            Stream myBlob = new MemoryStream();
            var file = req.Form.Files["payload"];
            myBlob = file.OpenReadStream();

            // Get cconnection strings
            string storageConnectionString = Utilities.GetBlobStorageConnectionString("ExperienceStorage");
            string cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDBConnectionString");

            return await RunLocal(log, storageConnectionString, cosmosConnectionString, experienceName, ownerID, myBlob);
        }

        public static async Task<IActionResult> RunLocal(ILogger log, string storageConnectionString, string cosmosConnectionString, string experienceName, string ownerID, Stream fileStream)
        {
            // Create a random ID
            string experienceID = System.Guid.NewGuid().ToString();

            // Set container to OwnerID
            BlobServiceClient storageClient = new BlobServiceClient(storageConnectionString);
            BlobContainerClient blobClient = null;
            try
            {
                blobClient = storageClient.CreateBlobContainer(ownerID);
            }
            catch (Exception)
            {
                // This is expected behaviour, but there is no other way to check for the existence of a container... :(
                blobClient = storageClient.GetBlobContainerClient(ownerID);
            }

            BlobClient blob = blobClient.GetBlobClient(experienceID);
            await blob.UploadAsync(fileStream);

            // Connect to metadata db and add this experience
            using CosmosClient client = new(connectionString: cosmosConnectionString);
            await client.GetContainer("experiences", "metadata").CreateItemAsync(
                new
                {
                    id = experienceID,
                    ownerID = ownerID,
                    name = experienceName,
                });

            string responseMessage = $"Successfully created the experience: {experienceName}";

            return new CreatedResult(experienceID, responseMessage);
        }
    }

    
}
