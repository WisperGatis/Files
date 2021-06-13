using Files.Enums;
using Files.Filesystem;
using System;
using Windows.Storage;
using Windows.System.UserProfile;

namespace Files.Helpers
{
    public static class WallpaperHelpers
    {
        public static async void SetAsBackground(WallpaperType type, string filePath, IShellPage associatedInstance)
        {
            if (UserProfilePersonalizationSettings.IsSupported())
            {
                StorageFile sourceFile = await StorageItemHelpers.ToStorageItem<StorageFile>(filePath, associatedInstance);
                if (sourceFile == null)
                {
                    return;
                }

                StorageFolder localFolder = ApplicationData.Current.LocalFolder;

                StorageFile file = await FilesystemTasks.Wrap(() => sourceFile.CopyAsync(localFolder, sourceFile.Name, NameCollisionOption.GenerateUniqueName).AsTask());
                if (file == null)
                {
                    return;
                }

                UserProfilePersonalizationSettings profileSettings = UserProfilePersonalizationSettings.Current;
                if (type == WallpaperType.Desktop)
                {
                    await profileSettings.TrySetWallpaperImageAsync(file);
                }
                else if (type == WallpaperType.LockScreen)
                {
                    await profileSettings.TrySetLockScreenImageAsync(file);
                }
            }
        }
    }
}