using Microsoft.Identity.Client.Extensions.Msal;

namespace Solipsist.CLI
{
    public static class CacheSettings
    {
        // computing the root directory is not very simple on Linux and Mac, so a helper is provided
        private static readonly string s_cacheFilePath =
                   Path.Combine(MsalCacheHelper.UserRootDirectory, "msal.solx.cache");

        public static readonly string CacheFileName = Path.GetFileName(s_cacheFilePath);
        public static readonly string CacheDir = Path.GetDirectoryName(s_cacheFilePath);


        public static readonly string KeyChainServiceName = "Solipsist.solx";
        public static readonly string KeyChainAccountName = "MSALCache";

        public static readonly string LinuxKeyRingSchema = "com.solipsist.solx.tokencache";
        public static readonly string LinuxKeyRingCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
        public static readonly string LinuxKeyRingLabel = "MSAL token cache for all Solipsist developer apps.";
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new KeyValuePair<string, string>("Version", "1");
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new KeyValuePair<string, string>("ProductGroup", "MyApps");
    }
}
