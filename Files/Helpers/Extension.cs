using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppExtensions;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

public class Extension : INotifyPropertyChanged
{
    #region Member Vars

    private PropertySet properties;
    private string serviceName;
    private readonly object sync = new object();

    public event PropertyChangedEventHandler PropertyChanged;

    public List<string> FileExtensions { get; internal set; } = new List<string>();

    #endregion Member Vars

    public Extension(AppExtension ext, PropertySet properties, BitmapImage logo)
    {
        AppExtension = ext;
        this.properties = properties;
        Enabled = false;
        Loaded = false;
        Offline = false;
        Logo = logo;
        Visible = Visibility.Collapsed;

        #region Properties

        serviceName = null;
        if (this.properties != null)
        {
            if (this.properties.ContainsKey("Service"))
            {
                PropertySet serviceProperty = this.properties["Service"] as PropertySet;
                serviceName = serviceProperty["#text"].ToString();
            }
        }

        #endregion Properties

        UniqueId = $"{ext.AppInfo.AppUserModelId}!{ext.Id}"; 
    }

    #region Properties

    public BitmapImage Logo { get; private set; }

    /// <summary>
    /// Gets or sets the unique id of this extension which will be AppUserModel Id + Extension ID.
    /// </summary>
    public string UniqueId { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has enabled the extension or not.
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the package containing the extension is offline.
    /// </summary>
    public bool Offline { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the package has been loaded or not.
    /// </summary>
    public bool Loaded { get; private set; }

    public string PublicFolderPath { get; private set; }

    public AppExtension AppExtension { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the extension should be visible in the list of extensions.
    /// </summary>
    public Visibility Visible { get; private set; }

    #endregion Properties

    public async Task<ValueSet> Invoke(ValueSet message)
    {
        if (Loaded)
        {
            try
            {
                using (var connection = new AppServiceConnection())
                {
                    connection.AppServiceName = serviceName;
                    connection.PackageFamilyName = AppExtension.Package.Id.FamilyName;

                    AppServiceConnectionStatus status = await connection.OpenAsync();
                    if (status != AppServiceConnectionStatus.Success)
                    {
                        Debug.WriteLine("Failed App Service Connection");
                    }
                    else
                    {
                        AppServiceResponse response = await connection.SendMessageAsync(message);
                        if (response.Status == AppServiceResponseStatus.Success)
                        {
                            return response.Message;
                        }
                    }
                }
            }
            catch (Exception)
            {
                Debug.WriteLine("Calling the App Service failed");
            }
        }
        return new ValueSet(); 
    }

    public async Task Update(AppExtension ext)
    {
        string identifier = ext.AppInfo.AppUserModelId + "!" + ext.Id;
        if (identifier != this.UniqueId)
        {
            return;
        }

        var properties = await ext.GetExtensionPropertiesAsync() as PropertySet;

        var filestream = await (ext.AppInfo.DisplayInfo.GetLogo(new Windows.Foundation.Size(1, 1))).OpenReadAsync();
        BitmapImage logo = new BitmapImage();
        logo.SetSource(filestream);

        this.AppExtension = ext;
        this.properties = properties;
        Logo = logo;

        #region Update Properties

        // update app service information
        serviceName = null;
        if (this.properties != null)
        {
            if (this.properties.ContainsKey("Service"))
            {
                PropertySet serviceProperty = this.properties["Service"] as PropertySet;
                this.serviceName = serviceProperty["#text"].ToString();
            }
        }

        #endregion Update Properties

        await MarkAsLoaded();
    }

    public async Task MarkAsLoaded()
    {
        if (!AppExtension.Package.Status.VerifyIsOK())
        {
            return;
        }

        Enabled = true;

        if (Loaded)
        {
            return;
        }

        StorageFolder folder = await AppExtension.GetPublicFolderAsync();
        PublicFolderPath = folder.Path;
        try
        {
            var file = await folder.GetFileAsync("FileExtensions.json");
            var text = await FileIO.ReadTextAsync(file);
            FileExtensions = JsonConvert.DeserializeObject<List<string>>(text);
        }
        catch
        {
            Debug.WriteLine("Unable to get extensions");
        }

        Loaded = true;
        Visible = Visibility.Visible;
        RaisePropertyChanged(nameof(Visible));
        Offline = false;
    }

    public async Task Enable()
    {
        Enabled = true;
        await MarkAsLoaded();
    }

    public void Unload()
    {
        lock (sync) 
        {
            if (Loaded)
            {
                if (!AppExtension.Package.Status.VerifyIsOK() && !AppExtension.Package.Status.PackageOffline)
                {
                    Offline = true;
                }

                Loaded = false;
                Visible = Visibility.Collapsed;
                RaisePropertyChanged(nameof(Visible));
            }
        }
    }

    public void Disable()
    {
        if (Enabled)
        {
            Enabled = false;
            Unload();
        }
    }

    #region PropertyChanged

    /// <summary>
    /// Typical property changed handler so that the UI will update
    /// </summary>
    /// <param name="name"></param>
    private void RaisePropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion PropertyChanged
}