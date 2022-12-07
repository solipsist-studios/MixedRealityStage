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
                description: "The name of the experience")
            { IsRequired = true };

            var ownerOption = new Option<string?>(
                name: "--owner",
                description: "The ID of the owner of the experience")
            { IsRequired = true };

            var fileOption = new Option<FileInfo?>(
                name: "--file",
                description: "The packaged Unity linux server build as a .tar.gz")
            { IsRequired = true };

            var experienceOption = new Option<string?>(
                name: "--experience-id",
                description: "The GUID value of the experience")
            { IsRequired = true };

            var adminUsernameOption = new Option<string?>(
                name: "--admin-username",
                description: "The username of the VM admin account",
                getDefaultValue: () => "solipsistadmin");

            var adminPasswordOption = new Option<string?>(
                name: "--admin-password",
                description: "The password for the VM admin account",
                getDefaultValue: () => "solipsist4ever!");

            var locationOption = new Option<string?>(
                name: "--location",
                description: "The region in which to locate the resources",
                getDefaultValue: () => "EastUS2");
            #endregion

            var rootCommand = new RootCommand("Solipsist Experience Platform CLI");
            var catCommand = new Command("cat", "Commands relating to management of the Experience Catalog");
            catCommand.AddGlobalOption(cosmosConnectionStringOption);
            catCommand.AddGlobalOption(storageConnectionStringOption);
            rootCommand.AddCommand(catCommand);

            #region add_command
            var addCommand = new Command("add", "Creates a new experience in the catalog")
            {
                nameOption,
                ownerOption,
                fileOption
            };
            catCommand.AddCommand(addCommand);

            addCommand.SetHandler(async (storageConnectionString, cosmosConnectionString, name, owner, file) =>
            {
                var fileStream = file != null ? file.OpenRead() : null;
                var result = await AddExperience.RunLocal(logger, storageConnectionString, cosmosConnectionString, name, owner, fileStream);
                logger.LogInformation(result.ToString());
            },
            storageConnectionStringOption, cosmosConnectionStringOption, nameOption, ownerOption, fileOption);
            #endregion

            #region get_command
            var getCommand = new Command("get", "Retrieves available experiences by owner")
            {
                ownerOption
            };
            catCommand.AddCommand(getCommand);

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

            #region launch_command
            var launchCommand = new Command("launch", "Start running an experience from the catalog")
            {
                experienceOption,
                adminUsernameOption,
                adminPasswordOption,
                locationOption
            };
            catCommand.AddCommand(launchCommand);

            launchCommand.SetHandler(async (storageConnectionString, cosmosConnectionString, expID, adminUsername, adminPassword, location) =>
            {
                var result = await LaunchExperience.RunLocal(logger, location, storageConnectionString, cosmosConnectionString, expID, adminUsername, adminPassword);
                logger.LogInformation(result.ToString());
            },
            storageConnectionStringOption, cosmosConnectionStringOption, experienceOption, adminUsernameOption, adminPasswordOption, locationOption);
            #endregion

            return await rootCommand.InvokeAsync(args);
        }
    }
}