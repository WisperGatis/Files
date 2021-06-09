using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.System;

namespace Files.Helpers
{
    public static class PackageHelper
    {
        private static readonly Uri dummyUri = new Uri("mailto:dummy@dummy.com");

        public static async Task<bool> IsAppInstalledAsync(string packageName)
        {
            try
            {
                bool appInstalled;
                LaunchQuerySupportStatus result = await Launcher.QueryUriSupportAsync(dummyUri, LaunchQuerySupportType.Uri, packageName);
                switch (result)
                {
                    case LaunchQuerySupportStatus.Available:
                    case LaunchQuerySupportStatus.NotSupported:
                        appInstalled = true;
                        break;
                    default:
                        appInstalled = false;
                        break;
                }

                Debug.WriteLine($"App {packageName}, query status: {result}, installed: {appInstalled}");
                return appInstalled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if app {packageName} is installed. Error: {ex}");
                return false;
            }
        }
    }
}