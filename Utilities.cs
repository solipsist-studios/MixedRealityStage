using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solipsist.ExperienceCatalog
{
    internal class Utilities
    {
        // TODO: Make this work for both local settings and Key Storage
        // TODO2: Replace this with Identity based auth
        public static string GetBlobStorageConnectionString(string name)
        {
            var conStrSetting = ConfigurationManager.ConnectionStrings[$"ConnectionStrings:{name}"];
            string conStr = conStrSetting != null ?
                conStrSetting.ConnectionString :
                // Azure Functions App Service naming convention
                Environment.GetEnvironmentVariable($"BlobStorageConnectionString");

            return conStr;
        }
    }
}
