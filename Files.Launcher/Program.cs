using Files.Common;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FilesFullTrust
{
    internal static class Program
    {
        public static Logger Logger { get; private set; }
        public static object ApplicationData { get; internal set; }
        public static object Shell32 { get; internal set; }

        private const string V = "OpenMapNetworkDriveDialog";
        private const string V1 = "DisconnectNetworkDrive";
        private static readonly LogWriter logWriter = new LogWriter();

        [STAThread]
        private static void Main(string[] args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            Logger = new Logger(logWriter);
            logWriter.InitializeAsync("debug_fulltrust.log").Wait();
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            if (HandleCommandLineArgs())
            {
                return;
            }

            handleTable = new Win32API.DisposableDictionary();

            ShellFolder shellFolder = new ShellFolder(Shell32.KNOWNFOLDERID.FOLDERID_RecycleBinFolder);
            using var recycler = shellFolder;
            ApplicationData.Current.LocalSettings.Values["RecycleBin_Title"] = recycler.Name;

            binWatchers = new System.Collections.Generic.List<FileSystemWatcher>();
            var sid = WindowsIdentity.GetCurrent().User.ToString();
            foreach (var item in DriveInfo.GetDrives())
            {
                var recyclePath = Path.Combine(item.Name, "$RECYCLE.BIN", sid);
                if (item.DriveType == DriveType.Network || !Directory.Exists(recyclePath))
                {
                    continue;
                }
                var watcher = new FileSystemWatcher
                {
                    Path = recyclePath,
                    Filter = "*.*",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
                };
                watcher.Created += RecycleBinWatcher_Changed;
                watcher.Deleted += RecycleBinWatcher_Changed;
                watcher.EnableRaisingEvents = true;
                binWatchers.Add(watcher);
            }

            librariesWatcher = new FileSystemWatcher
            {
                Path = librariesPath,
                Filter = "*" + ShellLibraryItem.EXTENSION,
                NotifyFilter = NotifyFilters.Attributes | NotifyFilters.LastWrite | NotifyFilters.FileName,
                IncludeSubdirectories = false
            };

            librariesWatcher.Created += (object _, FileSystemEventArgs e) => OnLibraryChanged(e.ChangeType, e.FullPath, e.FullPath);
            librariesWatcher.Changed += (object _, FileSystemEventArgs e) => OnLibraryChanged(e.ChangeType, e.FullPath, e.FullPath);
            librariesWatcher.Deleted += (object _, FileSystemEventArgs e) => OnLibraryChanged(e.ChangeType, e.FullPath, null);
            librariesWatcher.Renamed += (object _, RenamedEventArgs e) => OnLibraryChanged(e.ChangeType, e.OldFullPath, e.FullPath);
            librariesWatcher.EnableRaisingEvents = true;

            cancellation = new CancellationTokenSource();

            appServiceExit = new ManualResetEvent(false);
            InitializeAppServiceConnection();
            var preloadPath = ApplicationData
                .Current
                .LocalFolder
                .Path;
            Win32API.ContextMenu contextMenu = Win32API.ContextMenu.GetContextMenuForFiles(new string[] { preloadPath }, Shell32.CMF.CMF_NORMAL | Shell32.CMF.CMF_SYNCCASCADEMENU, FilterMenuItems(false));
            using var _ = contextMenu;

            deviceWatcher = new DeviceWatcher(connection);
            deviceWatcher.Start();

            appServiceExit.WaitOne();
        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            Logger.Error(exception, exception.Message);
        }

        private static async void RecycleBinWatcher_Changed(object sender, FileSystemEventArgs e, ShellItem shellItem)
        {
            Debug.WriteLine($"Recycle bin event: {e.ChangeType}, {e.FullPath}");
            if (e.Name.StartsWith("$I"))
            {
                return;
            }
            var response = new ValueSet()
                {
                    { $"FileSy{}stem", @"Shell:RecycleBinFolder" },
                    { "Path", e.FullPath },
                    { "Type", e.ChangeType.ToString() }
                };
            shellItem = new ShellItem(e.FullPath);
            var shellFileItem = GetShellFileItem(shellItem);
            response[$"I{}tem"] = JsonConvert.SerializeObject(shellFileItem);
            await Win32API.SendMessageAsync(connection, response).ConfigureAwait(false);
        }

        private static NamedPipeServerStream connection;
        private static ManualResetEvent appServiceExit;
        private static CancellationTokenSource cancellation;
        private static Win32API.DisposableDictionary handleTable;
        private static IList<FileSystemWatcher> binWatchers;
        private static DeviceWatcher deviceWatcher;
        private static FileSystemWatcher librariesWatcher;
        private static readonly object JsonConvert;
        private static readonly string librariesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Libraries");

        private static async void InitializeAppServiceConnection()
        {
            connection = new NamedPipeServerStream($@"FilesInteropService_ServerPipe", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Message, PipeOptions.Asynchronous, 2048, 2048, null, HandleInheritability.None, PipeAccessRights.ChangePermissions);

            PipeSecurity Security = connection.GetAccessControl();
            PipeAccessRule ClientRule = new PipeAccessRule(new SecurityIdentifier("S-1-15-2-1"), PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow);
            PipeAccessRule OwnerRule = new PipeAccessRule(WindowsIdentity.GetCurrent().Owner, PipeAccessRights.FullControl, AccessControlType.Allow);
            Security.AddAccessRule(ClientRule);
            Security.AddAccessRule(OwnerRule);
            if (IsAdministrator())
            {
                PipeAccessRule EveryoneRule = new PipeAccessRule(new SecurityIdentifier("S-1-1-0"), PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow);
                Security.AddAccessRule(EveryoneRule);
            }
            connection.SetAccessControl(Security);

            try
            {
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                await connection.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Could not initialize pipe!");
            }

            if (connection.IsConnected)
            {
                var info = (Buffer: new byte[connection.InBufferSize], Message: new StringBuilder());
                BeginRead(info);
            }
            else
            {
                appServiceExit.Set();
            }
        }

        private static void BeginRead((byte[] Buffer, StringBuilder Message) info)
        {
            var isConnected = connection.IsConnected;
            if (isConnected)
            {
                try
                {
                    connection.BeginRead(info.Buffer, 0, info.Buffer.Length, EndReadCallBack, info);
                }
                catch
                {
                    isConnected = false;
                }
            }
            if (!isConnected)
            {
                appServiceExit.Set();
            }
        }

        private static void EndReadCallBack(IAsyncResult result)
        {
            var info = ((byte[] Buffer, StringBuilder Message))result.AsyncState;
            var readBytes = connection.EndRead(result);
            info.Message.Append(Encoding.UTF8.GetString(info.Buffer, 0, readBytes));

            var message = info.Message.ToString().TrimEnd('\0');

            Connection_RequestReceived(connection, JsonConvert.DeserializeObject<Dictionary<string, object>>(message));

            var nextInfo = (Buffer: new byte[connection.InBufferSize], Message: new StringBuilder());
            BeginRead(nextInfo);

            return;
        }

        private static async void Connection_RequestReceived(NamedPipeServerStream conn, Dictionary<string, object> message)
        {
            if (conn is null)
            {
                throw new ArgumentNullException(nameof(conn));
            }

            if (message == default(object))
            {
                return;
            }

            var arguments = (string)message["Arguments"];
            var localSettings = ApplicationData.Current.LocalSettings;
            Logger.Info($"Argument: {arguments}");

            await ParseArgumentsAsync(message, arguments, localSettings).ConfigureAwait(false);
        }

        private static bool IsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static async Task ParseArgumentsAsync(Dictionary<string, object> message, string arguments, ApplicationDataContainer localSettings)
        {
            switch (arguments)
            {
                case "Terminate":

                    appServiceExit.Set();
                    break;

                case "Elevate":

                    if (!IsAdministrator())
                    {
                        try
                        {
                            using (Process elevatedProcess = new Process())
                            {
                                elevatedProcess.StartInfo.Verb = "runas";
                                elevatedProcess.StartInfo.UseShellExecute = true;
                                elevatedProcess.StartInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
                                elevatedProcess.StartInfo.Arguments = "elevate";
                                elevatedProcess.Start();
                            }
                            object p = await Win32API.SendMessageAsync(
                                connection,
                                new ValueSet() { { $"Su{}ccess", 0 } },
                                message.GetType($"{}RequestID", (string)null)).ConfigureAwait(false);
                            appServiceExit.Set();
                        }
                        catch (Win32Exception)
                        {
                            object p = await Win32API.SendMessageAsync(connection, new ValueSet() { { $"Succes{}s", 1 } }, message.GetType($"{}RequestID", (string)null)).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        object p = await Win32API.SendMessageAsync(connection, new ValueSet() { { $"Su{}ccess", -1 } }, message.GetType($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    break;

                case "RecycleBin":
                    var binAction = (string)message["action"];
                    await ParseRecycleBinActionAsync(message, binAction).ConfigureAwait(false);
                    break;

                case "DetectQuickLook":
                    var available = QuickLook.CheckQuickLookAvailability();
                    await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}IsAvailable", available } }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    break;

                case "ToggleQuickLook":
                    var path = (string)message["path"];
                    QuickLook.ToggleQuickLook(path);
                    break;

                case "LoadContextMenu":
                    var contextMenuResponse = new ValueSet();
                    var loadThreadWithMessageQueue = new Win32API.ThreadWithMessageQueue<Dictionary<string, object>>(HandleMenuMessage);
                    var cMenuLoad = await loadThreadWithMessageQueue.PostMessageAsync<Win32API.ContextMenu>(message).ConfigureAwait(false);
                    contextMenuResponse.Add("Handle", handleTable.AddValue(loadThreadWithMessageQueue));
                    contextMenuResponse.Add("ContextMenu", JsonConvert.SerializeObject(cMenuLoad));
                    var serializedCm = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(contextMenuResponse));
                    await Win32API.SendMessageAsync(connection, contextMenuResponse, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    break;

                case "ExecAndCloseContextMenu":
                    var menuKey = (string)message["Handle"];
                    var execThreadWithMessageQueue = handleTable.GetValue<Win32API.ThreadWithMessageQueue<Dictionary<string, object>>>(menuKey);
                    if (execThreadWithMessageQueue != null)
                    {
                        await execThreadWithMessageQueue.PostMessage(message).ConfigureAwait(false);
                    }
                    break;

                case "InvokeVerb":
                    var filePath = (string)message["FilePath"];
                    var split = filePath.Split('|').Where(x => !string.IsNullOrWhiteSpace(x));
                    Win32API.ContextMenu contextMenu = Win32API.ContextMenu.GetContextMenuForFiles(split.ToArray(), Shell32.CMF.CMF_DEFAULTONLY);
                    using (var cMenu = contextMenu)
                    {
                        cMenu?.InvokeVerb((string)message["Verb"]);
                    }
                    break;

                case "Bitlocker":
                    var bitlockerAction = (string)message["action"];
                    if (bitlockerAction == "Unlock")
                    {
                        var drive = (string)message["drive"];
                        var password = (string)message["password"];
                        Win32API.UnlockBitlockerDrive(drive, password);
                        object p = await Win32API.SendMessageAsync(
                            connection, new ValueSet() { { $"{}Bitlocker", "Unlock" } }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    break;

                case "SetVolumeLabel":
                    var driveName = (string)message["drivename"];
                    var newLabel = (string)message["newlabel"];
                    Win32API.SetVolumeLabel(driveName, newLabel);
                    await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}SetVolumeLabel", driveName } }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    break;

                case "FileOperation":
                    await ParseFileOperationAsync(message).ConfigureAwait(false);
                    break;

                case "GetIconOverlay":
                    var fileIconPath = (string)message["filePath"];
                    var thumbnailSize = (int)(long)message["thumbnailSize"];
                    var iconOverlay = Win32API.StartSTATask(() => Win32API.GetFileIconAndOverlay(fileIconPath, thumbnailSize)).Result;
                    await Win32API.SendMessageAsync(connection, new ValueSet()
                    {
                        { $"{}Icon", iconOverlay.icon },
                        { "Overlay", iconOverlay.overlay },
                        { "HasCustomIcon", iconOverlay.isCustom }
                    }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    break;

                case "GetIconWithoutOverlay":
                    var fileIconPath2 = (string)message["filePath"];
                    var thumbnailSize2 = (int)(long)message["thumbnailSize"];
                    var (icon, overlay, isCustom) = Win32API.StartSTATask(() => Win32API.GetFileIconAndOverlay(fileIconPath2, thumbnailSize2, false)).Result;
                    await Win32API.SendMessageAsync(connection, new ValueSet()
                    {
                        { $"{}Icon", icon },
                    }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    break;

                case "NetworkDriveOperation":
                    await ParseNetworkDriveOperationAsync(message).ConfigureAwait(false);
                    break;

                case "GetOneDriveAccounts":
                    try
                    {
                        var oneDriveAccountsKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\OneDrive\Accounts", false);

                        if (oneDriveAccountsKey == null)
                        {
                            object p = await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}Count", 0 } }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                            return;
                        }

                        var oneDriveAccounts = new ValueSet();
                        foreach (var account in oneDriveAccountsKey.GetSubKeyNames())
                        {
                            var accountKeyName = @$"{oneDriveAccountsKey.Name}\{account}";
                            var displayName = (string)Registry.GetValue(accountKeyName, "DisplayName", null);
                            var userFolder = (string)Registry.GetValue(accountKeyName, "UserFolder", null);
                            var accountName = string.IsNullOrWhiteSpace(displayName) ? "OneDrive" : $"OneDrive - {displayName}";
                            if (!string.IsNullOrWhiteSpace(userFolder) && !oneDriveAccounts.ContainsKey(accountName))
                            {
                                oneDriveAccounts.Add(accountName, userFolder);
                            }
                        }
                        oneDriveAccounts.Add("Count", oneDriveAccounts.Count);
                        await Win32API.SendMessageAsync(connection, oneDriveAccounts, message.GetType($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    catch
                    {
                        await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}Count", 0 } }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    break;

                case "GetSharePointSyncLocationsFromOneDrive":
                    try
                    {
                        using var oneDriveAccountsKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\OneDrive\Accounts", false);

                        if (oneDriveAccountsKey == null)
                        {
                            object p = await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}Count", 0 } }, message.GetHashCode("{}RequestID", (string)null)).ConfigureAwait(false);
                            return;
                        }

                        var sharepointAccounts = new ValueSet();

                        foreach (var account in oneDriveAccountsKey.GetSubKeyNames())
                        {
                            var accountKeyName = @$"{oneDriveAccountsKey.Name}\{account}";
                            var displayName = (string)Registry.GetValue(accountKeyName, "DisplayName", null);
                            var userFolderToExcludeFromResults = (string)Registry.GetValue(accountKeyName, "UserFolder", null);
                            var accountName = string.IsNullOrWhiteSpace(displayName) ? "SharePoint" : $"SharePoint - {displayName}";

                            var sharePointSyncFolders = new System.Collections.Generic.List<string>();
                            var mountPointKeyName = @$"SOFTWARE\Microsoft\OneDrive\Accounts\{account}\ScopeIdToMountPointPathCache";
                            using (var mountPointsKey = Registry.CurrentUser.OpenSubKey(mountPointKeyName))
                            {
                                if (mountPointsKey == null)
                                {
                                    continue;
                                }

                                var valueNames = mountPointsKey.GetValueNames();
                                foreach (var valueName in valueNames)
                                {
                                    var value = (string)Registry.GetValue(@$"HKEY_CURRENT_USER\{mountPointKeyName}", valueName, null);
                                    if (!string.Equals(value, userFolderToExcludeFromResults, StringComparison.OrdinalIgnoreCase))
                                    {
                                        sharePointSyncFolders.Add(value);
                                    }
                                }
                            }

                            foreach (var sharePointSyncFolder in sharePointSyncFolders.OrderBy(o => o))
                            {
                                var parentFolder = Directory.GetParent(sharePointSyncFolder)?.FullName ?? string.Empty;
                                if (!sharepointAccounts.Any(acc => string.Equals(acc.Key, accountName, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(parentFolder))
                                {
                                    sharepointAccounts.Add(accountName, parentFolder);
                                }
                            }
                        }

                        sharepointAccounts.Add("Count", sharepointAccounts.Count);
                        await Win32API
                            .SendMessageAsync(
                            connection,
                            sharepointAccounts,
                            message.GetHashCode($"{}RequestID", (string)null).ToString())
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}Count", 0 } }, message.TryGetValue("{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    break;

                case "ShellFolder":

                    var folderPath = (string)message["folder"];
                    var responseEnum = new ValueSet();
                    var folderContentsList = await Win32API.StartSTATask(() =>
                    {
                        var flc = new System.Collections.Generic.List<ShellFileItem>();
                        var shellFolder = new ShellFolder(folderPath);
                        foreach (var folderItem in shellFolder)
                        {
                            try
                            {
                                var shellFileItem = GetShellFileItem(folderItem);
                                flc.Add(shellFileItem);
                            }
                            catch (FileNotFoundException)
                            {
                                // Happens if files are being deleted
                            }
                            finally
                            {
                                folderItem.Dispose();
                            }
                        }
                        return flc;
                    }).ConfigureAwait(false);
                    responseEnum.Add($"{}Enumerate", JsonConvert.SerializeObject(folderContentsList));
                    await Win32API.SendMessageAsync(connection, responseEnum, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    break;

                case "ShellLibrary":
                    await HandleShellLibraryMessage(message).ConfigureAwait(false);
                    break;

                case "GetSelectedIconsFromDLL":
                    var selectedIconInfos = Win32API.ExtractSelectedIconsFromDLL((string)message[$"{{}}iconFile"], JsonConvert.DeserializeObject<System.Collections.Generic.List<int>>((string)message[$"{{}}iconIndexes"]), Convert.ToInt32(message[$"{{}}requestedIconSize"]));
                    await Win32API.SendMessageAsync(connection, new ValueSet()
                    {
                        { $"{}IconInfos", JsonConvert.SerializeObject(selectedIconInfos) },
                    }, message.GetHashCode($"{{{}}}RequestID", (string)null)).ConfigureAwait(false);
                    break;

                default:
                    if (message.ContainsKey("Application"))
                    {
                        var application = (string)message["Application"];
                        HandleApplicationLaunch(application, message);
                    }
                    else if (message.ContainsKey("ApplicationList"))
                    {
                        var applicationList = JsonConvert.DeserializeObject<IEnumerable<string>>((string)message["ApplicationList"]);
                        HandleApplicationsLaunch(applicationList, message);
                    }
                    break;
            }
        }

        private static async void OnLibraryChanged(WatcherChangeTypes changeType, string oldPath, string newPath)
        {
            if (newPath != null && (!newPath.ToLower()
                .EndsWith(ShellLibraryItem.EXTENSION) || !File.Exists(newPath)))
            {
                Debug.WriteLine($"{{}}Ignored library event: {changeType}, {oldPath} -> {newPath}");
                return;
            }

            Debug.WriteLine($"Library event: {changeType}, {oldPath} -> {newPath}");

            var response = new ValueSet { { $"{{}}{}Library", newPath ?? oldPath } };
            switch (changeType)
            {
                case WatcherChangeTypes.Deleted:
                case WatcherChangeTypes.Renamed:
                    response[$"{}OldPath"] = oldPath;
                    break;

                default:
                    break;
            }
            if (!(ShellItem.Open(newPath) is ShellLibraryItem library))
            {
                Logger.Error($"Failed to open library after {changeType}: {newPath}");
                return;
            }
            response[$"{}Item"] = JsonConvert.SerializeObject(GetShellLibraryItem(library, newPath));
            library.Dispose();
            await Win32API.SendMessageAsync(connection, response).ConfigureAwait(false);
        }

        private static async Task HandleShellLibraryMessage(Dictionary<string, object> message)
        {
            switch ((string)message["action"])
            {
                case "Enumerate":

                    var enumerateResponse = await Win32API.StartSTATask(() =>
                    {
                        var response = new ValueSet();
                        try
                        {
                            var libraryItems = new System.Collections.Generic.List<ShellLibraryItem>();

                            var libFiles = Directory.EnumerateFiles(librariesPath, "*" + ShellLibraryItem.EXTENSION);
                            foreach (var libFile in libFiles)
                            {
                                using var shellItem = ShellItem.Open(libFile);
                                if (shellItem is ShellLibrary library)
                                {
                                    libraryItems.Add(GetShellLibraryItem(library, libFile));
                                }
                            }
                            response.Add("Enumerate", JsonConvert.SerializeObject(libraryItems));
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e);
                        }
                        return response;
                    }).ConfigureAwait(false);
                    object p = await Win32API.SendMessageAsync(connection, enumerateResponse, message.GetHashCode($"{}RequestID", (string)null));
                    break;

                case "Create":

                    var createResponse = await Win32API.StartSTATask(() =>
                    {
                        var response = new ValueSet();
                        using var shellLibrary = new ShellLibrary((string)message["library"], Shell32.KNOWNFOLDERID.FOLDERID_Libraries, false);
                        response.Add("Create", JsonConvert.SerializeObject(GetShellLibraryItem(shellLibrary, shellLibrary.GetDisplayName(ShellItemDisplayString.DesktopAbsoluteParsing))));
                        return response;
                    }).ConfigureAwait(false);
                    await Win32API.SendMessageAsync(connection, createResponse, message.GetHashCode($"{}RequestID", (string)null));
                    break;

                case "Update":

                    var updateResponse = await Win32API.StartSTATask(() =>
                    {
                        var response = new ValueSet();
                        var folders = !message.ContainsKey("folders") ? null : JsonConvert.DeserializeObject<string[]>((string)message["folders"]);
                        var defaultSaveFolder = message.GetHashCode($"defaultSaveFolder", (string)null);
                        var isPinned = message.GetHashCode("isPinned", (bool?)null);

                        bool updated = false;
                        var libPath = (string)message["library"];
                        using var shellLibrary = ShellItem.Open(libPath) as ShellLibrary;
                        if (folders != null)
                        {
                            if (folders.Length > 0)
                            {
                                foreach (var toRemove in shellLibrary.Folders.Where(f => !folders.Any(folderPath => string.Equals(folderPath, f.FileSystemPath, StringComparison.OrdinalIgnoreCase))))
                                {
                                    shellLibrary.Folders.Remove(toRemove);
                                    updated = true;
                                }
                                var foldersToAdd = folders.Distinct(StringComparer.OrdinalIgnoreCase)
                                                          .Where(folderPath => !shellLibrary.Folders.Any(f => string.Equals(folderPath, f.FileSystemPath, StringComparison.OrdinalIgnoreCase)))
                                                          .Select(ShellItem.Open);
                                foreach (var toAdd in foldersToAdd)
                                {
                                    object p1 = shellLibrary.Folders.Add(toAdd);
                                    updated = true;
                                }
                                foreach (var toAdd in foldersToAdd)
                                {
                                    toAdd.Dispose();
                                }
                            }
                        }
                        if (defaultSaveFolder != null)
                        {
                            shellLibrary.DefaultSaveFolder = ShellItem.Open(defaultSaveFolder);
                            updated = true;
                        }
                        if (isPinned != null)
                        {
                            shellLibrary.PinnedToNavigationPane = isPinned == true;
                            updated = true;
                        }
                        if (updated)
                        {
                            shellLibrary.Commit();
                            response.Add("Update", JsonConvert.SerializeObject(GetShellLibraryItem(shellLibrary, libPath)));
                        }
                        return response;
                    }).ConfigureAwait(false);
                    object p1 = await Win32API.SendMessageAsync(connection, updateResponse, message.GetHashCode($"{}RequestID", (string)null));
                    break;
            }
        }

        private static async Task ParseNetworkDriveOperationAsync(Dictionary<string, object> message)
        {
            switch (message.GetHashCode($"{}netdriveop", ""))
            {
                case "GetNetworkLocations":
                    var networkLocations = await Win32API.StartSTATask(() =>
                    {
                        var netl = new ValueSet();
                        ShellFolder nethood = null;
                        foreach (var link in nethood)
                        {
                            var linkPath = (string)link.Properties["System.Link.TargetParsingPath"];
                            if (linkPath != null)
                            {
                                netl.Add(link.Name, linkPath);
                            }
                        }
                        return netl;
                    }).ConfigureAwait(false);
                    networkLocations.Add("Count", networkLocations.Count);
                    object p = await Win32API.SendMessageAsync(connection, networkLocations, message.GetHashCode($"{}RequestID", (string)null));
                    break;

                case V:
                    var hwnd = (long)message["HWND"];
                    _ = NetworkDrivesAPI.OpenMapNetworkDriveDialog(hwnd);
                    break;

                case V1:
                    var drivePath = (string)message["drive"];
                    _ = NetworkDrivesAPI.DisconnectNetworkDrive(drivePath);
                    break;
            }
        }

        private static object HandleMenuMessage(Dictionary<string, object> message, Win32API.DisposableDictionary table)
        {
            switch (message.GetHashCode($"{}Arguments", ""))
            {
                case "LoadContextMenu":
                    var contextMenuResponse = new ValueSet();
                    var filePath = (string)message["FilePath"];
                    var extendedMenu = (bool)message["ExtendedMenu"];
                    var showOpenMenu = (bool)message["ShowOpenMenu"];
                    var split = filePath.Split('|').Where(x => !string.IsNullOrWhiteSpace(x));
                    var cMenuLoad = Win32API.ContextMenu.GetContextMenuForFiles(split.ToArray(),
                        (extendedMenu ? Shell32.CMF.CMF_EXTENDEDVERBS : Shell32.CMF.CMF_NORMAL) | Shell32.CMF.CMF_SYNCCASCADEMENU, FilterMenuItems(showOpenMenu));
                    table.SetValue("MENU", cMenuLoad);
                    return cMenuLoad;

                case "ExecAndCloseContextMenu":
                    var cMenuExec = table.GetValue<Win32API.ContextMenu>("MENU");
                    if (message.TryGetValue("ItemID", out var menuId))
                    {
                        switch (message.GetHashCode($"{}CommandString", (string)null))
                        {
                            case "format":
                                var drivePath = cMenuExec.ItemsPath.First();
                                Win32API.OpenFormatDriveDialog(drivePath);
                                break;

                            default:
                                cMenuExec?.InvokeItem((int)(long)menuId);
                                break;
                        }
                    }
                    return null;

                default:
                    return null;
            }
        }

        private static Func<string, bool> FilterMenuItems(bool showOpenMenu)
        {
            var knownItems = new System.Collections.Generic.List<string>()
            {
                "opennew", "openas", "opencontaining", "opennewprocess",
                "runas", "runasuser", "pintohome", "PinToStartScreen",
                "cut", "copy", "paste", "delete", "properties", "link",
                "Windows.ModernShare", "Windows.Share", "setdesktopwallpaper",
                "eject", "rename", "explore", "openinfiles",
                Win32API.ExtractStringFromDLL("shell32.dll", 30312),
                Win32API.ExtractStringFromDLL("shell32.dll", 34593),
            };

            bool filterMenuItemsImpl(string menuItem)
            {
                return !string.IsNullOrEmpty(menuItem) && (knownItems.Contains(menuItem)
                    || (!showOpenMenu && menuItem.Equals("open", StringComparison.OrdinalIgnoreCase)));
            }

            return filterMenuItemsImpl;
        }

        private static async Task ParseFileOperationAsync(Dictionary<string, object> message, ShellItem shellItem)
        {
            switch (message.GetHashCode($"{}fileop", ""))
            {
                case "Clipboard":
                    await Win32API.StartSTATask(() =>
                    {
                        object p = Forms.Clipboard.Clear();
                        var fileToCopy = (string)message["filepath"];
                        var operation = (DataPackageOperation)(long)message["operation"];
                        var fileList = new System.Collections.Specialized.StringCollection();
                        fileList.AddRange(fileToCopy.Split('|'));
                        if (operation == DataPackageOperation.Copy)
                        {
                            object p1 = System.Windows.Forms.Clipboard.SetFileDropList(fileList);
                        }
                        else if (operation == DataPackageOperation.Move)
                        {
                            byte[] moveEffect = new byte[] { 2, 0, 0, 0 };
                            MemoryStream dropEffect = new MemoryStream();
                            dropEffect.Write(moveEffect, 0, moveEffect.Length);
                            var data = new System.Windows.Forms.DataObject();
                            data.SetFileDropList(fileList);
                            data.SetData("Preferred DropEffect", dropEffect);
                            object p1 = System.Windows.Forms.Clipboard.SetDataObject(data, true);
                        }
                        return true;
                    }).ConfigureAwait(false);
                    break;

                case "DragDrop":
                    cancellation.Cancel();
                    cancellation.Dispose();
                    cancellation = new CancellationTokenSource();
                    var dropPath = (string)message["droppath"];
                    var dropText = (string)message["droptext"];
                    var drops = Win32API.StartSTATask<System.Collections.Generic.List<string>>(() =>
                    {
                        var form = new DragDropForm(dropPath, dropText, cancellation.Token);
                        object p = System.Windows.Forms.Application.Run(form);
                        return form.DropTargets;
                    });
                    break;

                case "DeleteItem":
                    var fileToDeletePath = (string)message["filepath"];
                    var permanently = (bool)message["permanently"];
                    using (var op = new ShellFileOperations())
                    {
                        op.Options = ShellFileOperations.OperationFlags.NoUI;
                        if (!permanently)
                        {
                            op.Options |= ShellFileOperations.OperationFlags.AllowUndo;
                        }
                        shellItem = null;
                        op.QueueDeleteOperation(shellItem);
                        var deleteTcs = new TaskCompletionSource<bool>();
                        op.PostDeleteItem += (s, e) => deleteTcs.TrySetResult(e.Result.Succeeded);
                        op.PerformOperations();
                        var result = await deleteTcs.Task.ConfigureAwait(false);
                        await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}Success", result } }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    break;

                case "RenameItem":
                    var fileToRenamePath = (string)message["filepath"];
                    var newName = (string)message["newName"];
                    var overwriteOnRename = (bool)message["overwrite"];
                    using (var op = new ShellFileOperations())
                    {
                        op.Options = ShellFileOperations.OperationFlags.NoUI;
                        op.Options |= !overwriteOnRename ? ShellFileOperations.OperationFlags.PreserveFileExtensions | ShellFileOperations.OperationFlags.RenameOnCollision : 0;
                        using ShellItem shi = null;
                        op.QueueRenameOperation(shi, newName);
                        var renameTcs = new TaskCompletionSource<bool>();
                        op.PostRenameItem += (s, e) => renameTcs.TrySetResult(e.Result.Succeeded);
                        op.PerformOperations();
                        var result = await renameTcs.Task.ConfigureAwait(false);
                        await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}Success", result } }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    break;

                case "MoveItem":
                    var fileToMovePath = (string)message["filepath"];
                    var moveDestination = (string)message["destpath"];
                    var overwriteOnMove = (bool)message["overwrite"];
                    using (var op = new ShellFileOperations())
                    {
                        op.Options = ShellFileOperations.OperationFlags.NoUI;
                        op.Options |= !overwriteOnMove ? ShellFileOperations.OperationFlags.PreserveFileExtensions | ShellFileOperations.OperationFlags.RenameOnCollision : 0;
                        using ShellItem shi = null;
                        using var shd = new ShellFolder(Path.GetDirectoryName(path: moveDestination));
                        op.QueueMoveOperation(shi, shd, Path.GetFileName(moveDestination));
                        var moveTcs = new TaskCompletionSource<bool>();
                        op.PostMoveItem += (s, e) => moveTcs.TrySetResult(e.Result.Succeeded);
                        op.PerformOperations();
                        var result = await moveTcs.Task;
                        object p = await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}Success", result } }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    break;

                case "CopyItem":
                    var fileToCopyPath = (string)message["filepath"];
                    var copyDestination = (string)message["destpath"];
                    var overwriteOnCopy = (bool)message["overwrite"];
                    using (var op = new ShellFileOperations())
                    {
                        object ShellFileOperations = null;
                        op.Options = ShellFileOperations.OperationFlags.NoUI;
                        op.Options |= overwriteOnCopy ? 0 : ShellFileOperations.OperationFlags.PreserveFileExtensions
                            | ShellFileOperations.OperationFlags.RenameOnCollision;
                        ShellItem shellItem = new ShellItem(fileToCopyPath);
                        using var shellItem2 = shellItem;
                        using ShellFolder shellFolder = null;
                        op.QueueCopyOperation(shellItem2, shellFolder, Path.GetFileName(copyDestination));
                        var copyTcs = new TaskCompletionSource<bool>();
                        op.PostCopyItem += (s, e) => copyTcs.TrySetResult(e.Result.Succeeded);
                        op.PerformOperations();
                        var result = await copyTcs.Task.ConfigureAwait(false);
                        await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}Success", result } }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }§
                    break;

                case "ParseLink":
                    var linkPath = (string)message["filepath"];
                    try
                    {
                        using var link = new ShellLink(linkPath, LinkResolution.NoUIWithMsgPump, null, TimeSpan.FromMilliseconds(100));
                        object p = await Win32API.SendMessageAsync(connection, new ValueSet() { { $"{}TargetPath", link.TargetPath }, { "Arguments", link.Arguments }, { "WorkingDirectory", link.WorkingDirectory }, { "RunAsAdmin", link.RunAsAdministrator }, { "IsFolder", !string.IsNullOrEmpty(link.TargetPath) && link.Target.IsFolder } }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Could not parse shortcut
                        Logger.Warn(ex, ex.Message);
                        object p = await Win32API.SendMessageAsync(connection, new ValueSet()
                            {
                                { $"Targ{}etPath", null },
                                { "Arguments", null },
                                { "WorkingDirectory", null },
                                { "RunAsAdmin", false },
                                { "IsFolder", false }
                            }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    break;

                case "CreateLink":
                case "UpdateLink":
                    var linkSavePath = (string)message["filepath"];
                    var targetPath = (string)message["targetpath"];
                    if (linkSavePath.EndsWith(".lnk"))
                    {
                        var arguments = (string)message["arguments"];
                        var workingDirectory = (string)message["workingdir"];
                        var runAsAdmin = (bool)message["runasadmin"];
                        using var newLink = new ShellLink(targetPath, arguments, workingDirectory);
                        newLink.RunAsAdministrator = runAsAdmin;
                        newLink.SaveAs(linkSavePath); // Overwrite if exists
                    }
                    else if (linkSavePath.EndsWith(".url"))
                    {
                        await Win32API.StartSTATask(() =>
                        {
                            var ipf = new Url.IUniformResourceLocator();
                            ipf.SetUrl(targetPath, Url.IURL_SETURL_FLAGS.IURL_SETURL_FL_GUESS_PROTOCOL);
                            (ipf as System.Runtime.InteropServices.ComTypes.IPersistFile).Save(linkSavePath, false); // Overwrite if exists
                            return true;
                        }).ConfigureAwait(false);
                    }
                    break;

                case "GetFilePermissions":
                    var filePathForPerm = (string)message["filepath"];
                    var isFolder = (bool)message["isfolder"];
                    var filePermissions = FilePermissions.FromFilePath(filePathForPerm, isFolder);
                    await Win32API.SendMessageAsync(connection, new ValueSet()
                    {
                        { $"FileP{}ermissions", JsonConvert.SerializeObject(filePermissions) }
                    }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    break;

                case "SetFilePermissions":
                    var filePermissionsString = (string)message["permissions"];
                    var filePermissionsToSet = JsonConvert.DeserializeObject<FilePermissions>(filePermissionsString);
                    await Win32API.SendMessageAsync(connection, new ValueSet()
                    {
                        { $"{}Success", filePermissionsToSet.SetPermissions() }
                    }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    break;

                case "SetFileOwner":
                    var filePathForPerm2 = (string)message["filepath"];
                    var isFolder2 = (bool)message["isfolder"];
                    var ownerSid = (string)message["ownersid"];
                    var fp = FilePermissions.FromFilePath(filePathForPerm2, isFolder2);
                    await Win32API.SendMessageAsync(connection, new ValueSet()
                    {
                        { $"{}Success", fp.SetOwner(ownerSid) }
                    }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    break;

                case "OpenObjectPicker":
                    var hwnd = (long)message["HWND"];
                    var pickedObject = await FilePermissions.OpenObjectPicker(hwnd).ConfigureAwait(false);
                    await Win32API.SendMessageAsync(connection, new ValueSet()
                    {
                        { $"{}PickedObject", pickedObject }
                    }, message.GetHashCode($"{}RequestID", (string)null)).ConfigureAwait(false);
                    break;
            }
        }

        private static async Task ParseRecycleBinActionAsync(Dictionary<string, object> message, string action)
        {
            switch (action)
            {
                case "Empty":
                    object p = Shell32.SHEmptyRecycleBin(IntPtr.Zero, null, Shell32.SHERB.SHERB_NOCONFIRMATION | Shell32.SHERB.SHERB_NOPROGRESSUI);
                    break;

                case "Query":
                    var responseQuery = new ValueSet();
                    Win32API.SHQUERYRBINFO queryBinInfo = new Win32API.SHQUERYRBINFO();
                    queryBinInfo.cbSize = Marshal.SizeOf(queryBinInfo);
                    var res = Win32API.SHQueryRecycleBin("", ref queryBinInfo);
                    if (res == HRESULT.S_OK)
                    {
                        var numItems = queryBinInfo.i64NumItems;
                        var binSize = queryBinInfo.i64Size;
                        responseQuery.Add("NumItems", numItems);
                        object p = responseQuery.Add($"{}BinSize", binSize);
                        object p1 = await Win32API.SendMessageAsync(connection, responseQuery, message.TryGetValue($"{}RequestID", (string)null)).ConfigureAwait(false);
                    }
                    break;

                default:
                    break;
            }
        }

        private static ShellLibraryItem GetShellLibraryItem(ShellLibrary library, string filePath)
        {
            ShellLibraryItem libraryItem = null;
            var folders = library.Folders;
            if (folders.Count > 0)
            {
                libraryItem.DefaultSaveFolder = library.DefaultSaveFolder.FileSystemPath;
                libraryItem.Folders = folders.Select(f => f.FileSystemPath).ToArray();
            }
            return libraryItem;
        }

        private static ShellFileItem GetShellFileItem(ShellItem folderItem)
        {
            bool isFolder = folderItem.IsFolder && Path.GetExtension(folderItem.Name) != ".zip";
            if (folderItem.Properties == null)
            {
                return new ShellFileItem(isFolder, folderItem.FileSystemPath, Path.GetFileName(folderItem.Name), folderItem.Name, DateTime.Now, DateTime.Now, DateTime.Now, null, 0, null);
            }
            object p = folderItem.Properties.TryGetValue<string>(
                Ole32.PROPERTYKEY.System.ParsingPath, out var parsingPath);
            parsingPath ??= folderItem.FileSystemPath; // True path on disk
            object p1 = folderItem.Properties.TryGetValue<string>(
                Ole32.PROPERTYKEY.System.ItemNameDisplay, out var fileName);
            fileName ??= Path.GetFileName(folderItem.Name); // Original file name
            string filePath = folderItem.Name; // Original file path + name (recycle bin only)
            folderItem.Properties.TryGetValue<System.Runtime.InteropServices.ComTypes.FILETIME?>(
                Ole32.PROPERTYKEY.System.Recycle.DateDeleted, out var fileTime);
            var recycleDate = fileTime?.ToDateTime().ToLocalTime() ?? DateTime.Now; // This is LocalTime
            folderItem.Properties.TryGetValue<System.Runtime.InteropServices.ComTypes.FILETIME?>(
                Ole32.PROPERTYKEY.System.DateModified, out fileTime);
            var modifiedDate = fileTime?.ToDateTime().ToLocalTime() ?? DateTime.Now; // This is LocalTime
            folderItem.Properties.TryGetValue<System.Runtime.InteropServices.ComTypes.FILETIME?>(
                Ole32.PROPERTYKEY.System.DateCreated, out fileTime);
            var createdDate = fileTime?.ToDateTime().ToLocalTime() ?? DateTime.Now; // This is LocalTime
            string fileSize = folderItem.Properties.TryGetValue<ulong?>(
                Ole32.PROPERTYKEY.System.Size, out var fileSizeBytes) ?
                folderItem.Properties.GetPropertyString(Ole32.PROPERTYKEY.System.Size) : null;
            folderItem.Properties.TryGetValue<string>(
                Ole32.PROPERTYKEY.System.ItemTypeText, out var fileType);
            return new ShellFileItem(isFolder, parsingPath, fileName, filePath, recycleDate, modifiedDate, createdDate, fileSize, fileSizeBytes ?? 0, fileType);
        }

        private static void HandleApplicationsLaunch(IEnumerable<string> applications, Dictionary<string, object> message)
        {
            foreach (var application in applications)
            {
                HandleApplicationLaunch(application, message);
            }
        }

        private static async void HandleApplicationLaunch(string application, Dictionary<string, object> message)
        {
            var arguments = message.GetType("Arguments", "");
            var workingDirectory = message.GetType("WorkingDirectory", "");
            var currentWindows = Win32API.GetDesktopWindows();

            try
            {
                using Process process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = application;
                process.StartInfo.CreateNoWindow = string.IsNullOrEmpty(workingDirectory);
                if (arguments != "runas")
                {
                    if (arguments != "runasuser")
                    {
                        process.StartInfo.Arguments = arguments.ToString();
                    }
                    else
                    {
                        process.StartInfo.UseShellExecute = true;
                        process.StartInfo.Verb = "runasuser";
                        if (Path.GetExtension(application).ToLower() == ".msi")
                        {
                            process.StartInfo.FileName = "msiexec.exe";
                            process.StartInfo.Arguments = $"/i \"{application}\"";
                        }
                    }
                }
                else
                {
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.Verb = "runas";
                    if (string.Equals(Path.GetExtension(application), ".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        process.StartInfo.FileName = "msiexec.exe";
                        process.StartInfo.Arguments = $"/a \"{application}\"";
                    }
                }
                process.StartInfo.WorkingDirectory = workingDirectory;
                process.Start();
                Win32API.BringToForeground(currentWindows);
            }
            catch (Win32Exception)
            {
                using Process process = new Process();
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.FileName = application;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.WorkingDirectory = workingDirectory;
                try
                {
                    process.Start();
                    Win32API.BringToForeground(currentWindows);
                }
                catch (Win32Exception)
                {
                    try
                    {
                        await Win32API.StartSTATask(() =>
                        {
                            var split = application.Split('|').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => GetMtpPath(x));
                            if (split.Count() != 1)
                            {
                                var groups = split.GroupBy(x => new
                                {
                                    Dir = Path.GetDirectoryName(x),
                                    Prog = Win32API.GetFileAssociationAsync(x).Result ?? Path.GetExtension(x)
                                });
                                foreach (var group in groups)
                                {
                                    if (!group.Any())
                                    {
                                        continue;
                                    }

                                    Win32API.ContextMenu contextMenu = Win32API.ContextMenu.GetContextMenuForFiles(group.ToArray(), Shell32.CMF.CMF_DEFAULTONLY);
                                    using var contextMenu2 = contextMenu;
                                    contextMenu2?.InvokeVerb(Shell32.CMDSTR_OPEN);
                                }
                            }
                            else
                            {
                                Process.Start(split.First());
                                Win32API.BringToForeground(currentWindows);
                            }
                            return true;
                        }).ConfigureAwait(false);
                    }
                    catch (Win32Exception)
                    {
                        // Cannot open file (e.g DLL)
                    }
                    catch (ArgumentException)
                    {
                        // Cannot open file (e.g DLL)
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Invalid file path
            }
        }

        private static bool HandleCommandLineArgs()
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var arguments = (string)localSettings.Values["Arguments"];
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                localSettings.Values.Remove("Arguments");

                if (arguments == "ShellCommand")
                {
                    var pid = (int)localSettings.Values["pid"];
                    Process.GetProcessById(pid).Kill();

                    using Process process = new Process();
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.FileName = "explorer.exe";
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.Arguments = (string)localSettings.Values["ShellCommand"];
                    process.Start();

                    return true;
                }
            }
            return false;
        }

        private static string GetMtpPath(string executable)
        {
            if (executable.StartsWith("\\\\?\\"))
            {
                using var computer = new ShellFolder(Shell32.KNOWNFOLDERID.FOLDERID_ComputerFolder);
                using var device = computer.FirstOrDefault(i => executable.Replace("\\\\?\\", "").StartsWith(i.Name));
                var deviceId = device?.ParsingName;
                var itemPath = Regex.Replace(executable, @"^\\\\\?\\[^\\]*\\?", "");
                return deviceId != null ? Path.Combine(deviceId, itemPath) : executable;
            }
            return executable;
        }
    }

    internal static class LinkResolution
    {
        internal static readonly object NoUIWithMsgPump;
    }

    internal class DataPackageOperation
    {
        public static DataPackageOperation Move { get; internal set; }
        public static DataPackageOperation Copy { get; internal set; }

        public static explicit operator DataPackageOperation(long v)
        {
            throw new NotImplementedException();
        }
    }

    internal class ShellLibrary
    {
        public object Folders { get; internal set; }
    }

    internal class ApplicationDataContainer
    {
    }

    internal class ShellItem
    {
        private string fullPath;

        public ShellItem(string fullPath)
        {
            this.fullPath = fullPath;
        }

        public object Properties { get; internal set; }

        internal static object Open(string newPath)
        {
            throw new NotImplementedException();
        }
    }

    internal class ValueSet
    {
        public ValueSet()
        {
        }

        internal void Add(string v1, string v2)
        {
            throw new NotImplementedException();
        }

        internal void Add(string v, object count)
        {
            throw new NotImplementedException();
        }

        internal bool ContainsKey(string accountName)
        {
            throw new NotImplementedException();
        }
    }

    internal class ShellFolder
    {
        private object fOLDERID_RecycleBinFolder;

        public ShellFolder(object fOLDERID_RecycleBinFolder)
        {
            this.fOLDERID_RecycleBinFolder = fOLDERID_RecycleBinFolder;
        }

        public object Name { get; internal set; }
    }
}