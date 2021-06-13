using Files.Common;
using Files.Filesystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.StartScreen;

namespace Files.Helpers
{
    public sealed class JumpListManager
    {
        private JumpList instance = null;
        private List<string> JumpListItemPaths { get; set; }

        public JumpListManager()
        {
            Initialize();
        }

        private async void Initialize()
        {
            try
            {
                if (JumpList.IsSupported())
                {
                    instance = await JumpList.LoadCurrentAsync();

                    instance.SystemGroupKind = JumpListSystemGroupKind.None;
                    JumpListItemPaths = instance.Items.Select(item => item.Arguments).ToList();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Warn(ex, ex.Message);
                instance = null;
            }
        }

        public async void AddFolderToJumpList(string path)
        {
            try
            {
                AddFolder(path);
                await instance?.SaveAsync();
            }
            catch { }
        }

        private void AddFolder(string path)
        {
            if (instance != null && !JumpListItemPaths.Contains(path))
            {
                string displayName;
                if (path.Equals(App.AppSettings.DesktopPath, StringComparison.OrdinalIgnoreCase))
                {
                    displayName = "ms-resource:///Resources/SidebarDesktop";
                }
                else if (path.Equals(App.AppSettings.DownloadsPath, StringComparison.OrdinalIgnoreCase))
                {
                    displayName = "ms-resource:///Resources/SidebarDownloads";
                }
                else if (path.Equals(App.AppSettings.RecycleBinPath, StringComparison.OrdinalIgnoreCase))
                {
                    var localSettings = ApplicationData.Current.LocalSettings;
                    displayName = localSettings.Values.Get("RecycleBin_Title", "Recycle Bin");
                }
                else if (App.LibraryManager.TryGetLibrary(path, out LibraryLocationItem library))
                {
                    var libName = Path.GetFileNameWithoutExtension(library.Path);
                    switch (libName)
                    {
                        case "Documents":
                        case "Pictures":
                        case "Music":
                        case "Videos":
                            displayName = $"ms-resource:///Resources/Sidebar{libName}";
                            break;

                        default:
                            displayName = library.Text;
                            break;
                    }
                }
                else
                {
                    displayName = Path.GetFileName(path);
                }

                var jumplistItem = JumpListItem.CreateWithArguments(path, displayName);
                jumplistItem.Description = jumplistItem.Arguments;
                jumplistItem.GroupName = "ms-resource:///Resources/JumpListRecentGroupHeader";
                jumplistItem.Logo = new Uri("ms-appx:///Assets/FolderIcon.png");
                instance.Items.Add(jumplistItem);
                JumpListItemPaths.Add(path);
            }
        }

        public async void RemoveFolder(string path)
        {
            try
            {
                if (JumpListItemPaths.Contains(path))
                {
                    JumpListItemPaths.Remove(path);
                    await UpdateAsync();
                }
            }
            catch { }
        }

        private async Task UpdateAsync()
        {
            if (instance != null)
            {
                instance?.Items.Clear();

                foreach (string path in JumpListItemPaths)
                {
                    AddFolder(path);
                }

                await instance.SaveAsync();
            }
        }
    }
}