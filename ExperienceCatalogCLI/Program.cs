using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Solipsist.ExperienceCatalog;
using System.CommandLine;

namespace Solipsist.CLI
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Set up logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("Solipsist.CLI.Program", LogLevel.Debug)
                    .AddConsole();
            });

            ILogger logger = loggerFactory.CreateLogger<Program>();
            logger.LogInformation("Example log message");

            #region options
            var storageConnectionStringOption = new Option<string?>(
                name: "--storage-connection-string",
                description: "The blob storage connection string");

            var cosmosConnectionStringOption = new Option<string?>(
                name: "--cosmos-connection-string",
                description: "The Cosmos DB connection string");

            var nameOption = new Option<string?>(
                name: "--name",
                description: "The name of the experience");

            var ownerOption = new Option<string?>(
                name: "--owner",
                description: "The ID of the owner of the experience");

            var fileOption = new Option<FileInfo?>(
                name: "--file",
                description: "The packaged Unity linux server build as a .tar.gz");
            #endregion

            var rootCommand = new RootCommand("Solipsist Experience Catalog CLI");

            #region add_command
            var addCommand = new Command("add", "Creates a new experience in the catalog")
            {
                storageConnectionStringOption,
                cosmosConnectionStringOption,
                nameOption,
                ownerOption,
                fileOption
            };
            rootCommand.AddCommand(addCommand);

            addCommand.SetHandler(async (storageConnectionString, cosmosConnectionString, name, owner, file) =>
            {
                var fileStream = file != null ? file.OpenRead() : null;
                await AddExperience.RunLocal(logger, storageConnectionString, cosmosConnectionString, name, owner, fileStream);
            },
            storageConnectionStringOption, cosmosConnectionStringOption, nameOption, ownerOption, fileOption);
            #endregion

            #region get_command
            var getCommand = new Command("get", "Retrieves available experiences by owner")
            {
                storageConnectionStringOption,
                cosmosConnectionStringOption,
                ownerOption
            };
            rootCommand.AddCommand(getCommand);

            getCommand.SetHandler(async (storageConnectionString, cosmosConnectionString, owner) =>
            {
                OkObjectResult? result = await GetExperiences.RunLocal(logger, storageConnectionString, cosmosConnectionString, owner) as OkObjectResult;
                if (result != null)
                {
                    JsonResult? experiences = result.Value as JsonResult;
                    if (experiences != null)
                    {
                        string output = JsonConvert.SerializeObject(experiences.Value);
                        logger.LogInformation(output);
                    }
                }

            },
            storageConnectionStringOption, cosmosConnectionStringOption, ownerOption);
            #endregion

            return await rootCommand.InvokeAsync(args);
        }
    }
}