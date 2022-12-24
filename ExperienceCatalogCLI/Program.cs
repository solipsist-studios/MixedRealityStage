using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
//using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Newtonsoft.Json;
using Solipsist.ExperienceCatalog;
using System.CommandLine;
using System.IdentityModel.Tokens.Jwt;
using System.Net;

namespace Solipsist.CLI
{
    internal class Program
    {
        // The MSAL Public client app
        private static IPublicClientApplication? application;

        //private static async Task<string> SignInUserAndGetTokenUsingMSAL(PublicClientApplicationOptions configuration, string[] scopes)
        //{
        //    string authority = string.Concat(configuration.Instance, configuration.TenantId);

        //    // Initialize the MSAL library by building a public client application
        //    application = PublicClientApplicationBuilder.Create(configuration.ClientId)
        //                                            .WithAuthority(authority)
        //                                            .WithDefaultRedirectUri()
        //                                            .Build();

        //    AuthenticationResult result;
        //    try
        //    {
        //        var accounts = await application.GetAccountsAsync();
        //        result = await application.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
        //         .ExecuteAsync();
        //    }
        //    catch (MsalUiRequiredException ex)
        //    {
        //        result = await application.AcquireTokenInteractive(scopes)
        //         .WithClaims(ex.Claims)
        //         .ExecuteAsync();
        //    }

        //    return result.AccessToken;
        //}

        public static async Task<string> GetCurrentUserIdentityAsync(ILogger log, TokenCredential credential)
        {
            string clientId = "bf644f2b-7148-4fde-bec1-79d5d58da4c5";
            string resourceId = "https://solipsiststudios.onmicrosoft.com/experience-catalog";
            //string[] scopes = new string[] { $"{resourceId}/.default" };
            string[] scopes = new string[]
            {
                $"{resourceId}/experiences.read",
                $"{resourceId}/experiences.write"
            };

            string token = "";
            try
            {
                token = credential.GetToken(new Azure.Core.TokenRequestContext(scopes), new System.Threading.CancellationToken()).Token;
            }
            catch (AuthenticationFailedException ex)
            {
                log.LogInformation("Failed to retrieve token from Default Credentials. Trying token cache...");

                application = PublicClientApplicationBuilder.Create(clientId)
                    .WithRedirectUri("http://localhost")
                    .Build();

                AuthenticationResult result;
                try
                {
                    var accounts = await application.GetAccountsAsync();
                    result = await application.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                        .ExecuteAsync();
                    token = result.AccessToken;
                }
                catch (MsalUiRequiredException ex2)
                {
                    log.LogInformation("Failed to retrieve token from token cache.  Please log in.");

                    result = await application.AcquireTokenInteractive(scopes)
                        .WithClaims(ex2.Claims)
                        .ExecuteAsync();
                    token = result.AccessToken;
                }
            }

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

            log.LogInformation("Full Token:\n{0}", jsonToken != null ? jsonToken.ToString() : "NOT FOUND");

            // TODO: Validate "aud" claim matches client ID
            return jsonToken.Claims.First(c => c.Type == "oid").Value;
        }

        static async Task<int> Main(string[] args)
        {
            // Set up logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", Microsoft.Extensions.Logging.LogLevel.Warning)
                    .AddFilter("System", Microsoft.Extensions.Logging.LogLevel.Warning)
                    .AddFilter("Solipsist.CLI.Program", Microsoft.Extensions.Logging.LogLevel.Debug)
                    .AddConsole();
            });

            ILogger logger = loggerFactory.CreateLogger<Program>();

            // Using appsettings.json for our configuration settings
            var builder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            #region user_auth
            var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);

            string ownerID = await GetCurrentUserIdentityAsync(logger, credential);
            logger.LogInformation("Authenticated as user: {0}", ownerID);
            #endregion

            #region options
            var nameOption = new Option<string?>(
                name: "--name",
                description: "The name of the experience")
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
            rootCommand.AddCommand(catCommand);

            #region add_command
            var addCommand = new Command("add", "Creates a new experience in the catalog")
            {
                nameOption,
                fileOption
            };
            catCommand.AddCommand(addCommand);

            addCommand.SetHandler(async (name, file) =>
            {
                var fileStream = file != null ? file.OpenRead() : null;
                var result = await AddExperience.RunLocal(logger, credential, name, ownerID, fileStream);
                logger.LogInformation(result.ToString());
            },
            nameOption, fileOption);
            #endregion

            #region list_command
            var listCommand = new Command("list", "Retrieves available experiences by owner");
            catCommand.AddCommand(listCommand);

            listCommand.SetHandler(async (owner) =>
            {
                OkObjectResult? result = await ListExperiences.RunLocal(logger, credential, ownerID) as OkObjectResult;
                if (result != null)
                {
                    JsonResult? experiences = result.Value as JsonResult;
                    if (experiences != null)
                    {
                        string output = JsonConvert.SerializeObject(experiences.Value);
                        logger.LogInformation(output);
                    }
                }

            });
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

            launchCommand.SetHandler(async (expID, adminUsername, adminPassword, location) =>
            {
                var result = await LaunchExperience.RunLocal(logger, credential, location, expID, adminUsername, adminPassword);
                logger.LogInformation(result.ToString());
            },
            experienceOption, adminUsernameOption, adminPasswordOption, locationOption);
            #endregion

            return await rootCommand.InvokeAsync(args);
        }
    }
}