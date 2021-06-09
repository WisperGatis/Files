using Files.Common;
using Files.Filesystem;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace Files.Helpers
{
    internal class LibraryHelper
    {

        public static bool IsDefaultLibrary(string libraryFilePath)
        {
            switch (Path.GetFileNameWithoutExtension(libraryFilePath))
            {
                case "CameraRoll":
                case "Documents":
                case "Music":
                case "Pictures":
                case "SavedPictures":
                case "Videos":
                    return true;

                default:
                    return false;
            }
        }

        public static async Task<List<LibraryLocationItem>> ListUserLibraries()
        {
            List<LibraryLocationItem> libraries = null;
            var connection = await AppServiceConnectionHelper.Instance;
            if (connection == null)
            {
                return null;
            }
            var (status, response) = await connection.SendMessageForResponseAsync(new ValueSet
            {
                { "Arguments", "ShellLibrary" },
                { "action", "Enumerate" }
            });
            if (status == AppServiceResponseStatus.Success && response.ContainsKey("Enumerate"))
            {
                libraries = JsonConvert.DeserializeObject<List<ShellLibraryItem>>((string)response["Enumerate"]).Select(lib => new LibraryLocationItem(lib)).ToList();
            }
            return libraries;
        }

        public static async Task<LibraryLocationItem> CreateLibrary(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            var connection = await AppServiceConnectionHelper.Instance;
            if (connection == null)
            {
                return null;
            }
            var (status, response) = await connection.SendMessageForResponseAsync(new ValueSet
            {
                { "Arguments", "ShellLibrary" },
                { "action", "Create" },
                { "library", name }
            });
            LibraryLocationItem library = null;
            if (status == AppServiceResponseStatus.Success && response.ContainsKey("Create"))
            {
                library = new LibraryLocationItem(JsonConvert.DeserializeObject<ShellLibraryItem>((string)response["Create"]));
            }
            return library;
        }

        public static async Task<LibraryLocationItem> UpdateLibrary(string libraryFilePath, string defaultSaveFolder = null, string[] folders = null, bool? isPinned = null)
        {
            if (string.IsNullOrWhiteSpace(libraryFilePath) || (defaultSaveFolder == null && folders == null && isPinned == null))
            {
                return null;
            }
            var connection = await AppServiceConnectionHelper.Instance;
            if (connection == null)
            {
                return null;
            }
            var request = new ValueSet
            {
                { "Arguments", "ShellLibrary" },
                { "action", "Update" },
                { "library", libraryFilePath }
            };
            if (!string.IsNullOrEmpty(defaultSaveFolder))
            {
                request.Add("defaultSaveFolder", defaultSaveFolder);
            }
            if (folders != null)
            {
                request.Add("folders", JsonConvert.SerializeObject(folders));
            }
            if (isPinned != null)
            {
                request.Add("isPinned", isPinned);
            }
            var (status, response) = await connection.SendMessageForResponseAsync(request);
            LibraryLocationItem library = null;
            if (status == AppServiceResponseStatus.Success && response.ContainsKey("Update"))
            {
                library = new LibraryLocationItem(JsonConvert.DeserializeObject<ShellLibraryItem>((string)response["Update"]));
            }
            return library;
        }
    }
}