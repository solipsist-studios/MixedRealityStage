using Azure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Newtonsoft.Json;
using Solipsist.Common;
using Solipsist.ExperienceCatalog;
using System.CommandLine;
using System.IdentityModel.Tokens.Jwt;

namespace Solipsist.CLI
{
    internal class Program
    {
        // The MSAL Public client app
        private static IPublicClientApplication? application;
        private static PublicClientApplicationOptions? appConfiguration = null;
        private static IConfiguration? configuration;

        private static async Task<JwtSecurityToken> SignInUserAndGetTokenUsingMSALAsync(string[] scopes)
        {
            if (configuration == null || appConfiguration == null)
            {
                return null;
            }

            string authority = configuration.GetValue<string>("Authority");

            // Initialize the MSAL library by building a public client application
            application = PublicClientApplicationBuilder.Create(appConfiguration.ClientId)
                                                        .WithB2CAuthority(authority)
                                                        .WithRedirectUri(appConfiguration.RedirectUri)
                                                        .Build();

            // Building StorageCreationProperties
            var storageProperties =
                 new StorageCreationPropertiesBuilder(CacheSettings.CacheFileName, CacheSettings.CacheDir)
                 .WithLinuxKeyring(
                     CacheSettings.LinuxKeyRingSchema,
                     CacheSettings.LinuxKeyRingCollection,
                     CacheSettings.LinuxKeyRingLabel,
                     CacheSettings.LinuxKeyRingAttr1,
                     CacheSettings.LinuxKeyRingAttr2)
                 .WithMacKeyChain(
                     CacheSettings.KeyChainServiceName,
                     CacheSettings.KeyChainAccountName)
                 .Build();

            // This hooks up the cross-platform cache into MSAL
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(application.UserTokenCache);

            AuthenticationResult result;
            try
            {
                var accounts = await application.GetAccountsAsync();
                result = await application.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                result = await application.AcquireTokenInteractive(scopes)
                    .WithClaims(ex.Claims)
                    .ExecuteAsync();
            }

            return Utilities.GetJwtFromString(result.AccessToken);
        }

        static async Task LogoutAndClearCacheAsync()
        {
            if (application == null)
            {
                return;
            }

            var accounts = await application.GetAccountsAsync().ConfigureAwait(false);
            foreach (var acc in accounts)
            {
                await application.RemoveAsync(acc).ConfigureAwait(false);
            }
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

            configuration = builder.Build();

            // Loading PublicClientApplicationOptions from the values set on appsettings.json
            appConfiguration = configuration
                .Get<PublicClientApplicationOptions>();

            #region user_auth
            string resourceId = "https://solipsiststudios.onmicrosoft.com/experience-catalog";
            string[] scopes = new string[]
            {
                $"{resourceId}/experiences.read",
                $"{resourceId}/experiences.write"
            };

            var jsonToken = await SignInUserAndGetTokenUsingMSALAsync(scopes);
            string ownerID = Utilities.GetUserIdentityFromToken(logger, jsonToken);
            logger.LogInformation("Authenticated as user: {0}", ownerID);

            var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
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

            var logoutCommand = new Command("logout", "Log out of current session");
            rootCommand.AddCommand(logoutCommand);
            logoutCommand.SetHandler(async () =>
            {
                await LogoutAndClearCacheAsync();
            });

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
                if (result != null && result is CreatedResult)
                {
                    logger.LogInformation("Successfully created experience with ID {0}", ((CreatedResult)result).Location);
                }
                
            },
            nameOption, fileOption);
            #endregion

            #region list_command
            var listCommand = new Command("list", "Retrieves available experiences by owner");
            catCommand.AddCommand(listCommand);

            listCommand.SetHandler(async (owner) =>
            {
                var result = await ListExperiences.RunLocal(logger, credential, ownerID);
                if (result != null && result is OkObjectResult)
                {
                    JsonResult? experiences = ((OkObjectResult)result).Value as JsonResult;
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
                if (result == null)
                {
                    logger.LogCritical("LaunchExperience failed with no error message!");
                }
                else if (result is OkObjectResult)
                {
                    JsonResult? vmInfo = ((OkObjectResult)result).Value as JsonResult;
                    if (vmInfo != null)
                    {
                        string output = JsonConvert.SerializeObject(vmInfo.Value);
                        logger.LogInformation(output);
                    }
                }
                else
                {
                    logger.LogError(result.ToString());
                }
            },
            experienceOption, adminUsernameOption, adminPasswordOption, locationOption);
            #endregion

            return await rootCommand.InvokeAsync(args);
        }
    }
}