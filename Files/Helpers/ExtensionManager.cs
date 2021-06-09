using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation.Collections;
using Windows.UI.Xaml.Media.Imaging;

namespace Files.Helpers
{
    internal class ExtensionManager
    {
        private AppExtensionCatalog catalog; 

        public ExtensionManager(string extensionContractName)
        {
            ExtensionContractName = extensionContractName;
            catalog = AppExtensionCatalog.Open(ExtensionContractName);
        }

        public ObservableCollection<Extension> Extensions { get; } = new ObservableCollection<Extension>();

        public Extension GetExtension(string id)
        {
            return Extensions.Where(e => e.UniqueId == id).FirstOrDefault();
        }

        public string ExtensionContractName { get; private set; }

        public void Initialize()
        {
            #region Error Checking & Dispatcher Setup

            // verify that we haven't already been initialized
            //if (_dispatcher != null)
            //{
            //    throw new ExtensionManagerException("Extension Manager for " + this.ExtensionContractName + " is already initialized.");
            //}

            //_dispatcher = dispatcher;

            #endregion Error Checking & Dispatcher Setup


            FindAndLoadExtensions();
        }

        public async void FindAndLoadExtensions()
        {
            #region Error Checking

            // Run on the UI thread because the Extensions Tab UI updates as extensions are added or removed
            //if (_dispatcher == null)
            //{
            //    throw new ExtensionManagerException("Extension Manager for " + this.ExtensionContractName + " is not initialized.");
            //}

            #endregion Error Checking

            IReadOnlyList<AppExtension> extensions = await catalog.FindAllAsync();
            foreach (AppExtension ext in extensions)
            {
                await LoadExtension(ext);
            }
        }

        private async void Catalog_PackageInstalled(AppExtensionCatalog sender, AppExtensionPackageInstalledEventArgs args)
        {
            foreach (AppExtension ext in args.Extensions)
            {
                await LoadExtension(ext);
            }
        }

        private async void Catalog_PackageUpdated(AppExtensionCatalog sender, AppExtensionPackageUpdatedEventArgs args)
        {
            foreach (AppExtension ext in args.Extensions)
            {
                await LoadExtension(ext);
            }
        }

        private void Catalog_PackageUpdating(AppExtensionCatalog sender, AppExtensionPackageUpdatingEventArgs args)
        {
            UnloadExtensions(args.Package);
        }

        private void Catalog_PackageUninstalling(AppExtensionCatalog sender, AppExtensionPackageUninstallingEventArgs args)
        {
            RemoveExtensions(args.Package);
        }

        private void Catalog_PackageStatusChanged(AppExtensionCatalog sender, AppExtensionPackageStatusChangedEventArgs args)
        {
            if (!args.Package.Status.VerifyIsOK()) 
            {
                if (args.Package.Status.PackageOffline)
                {
                    UnloadExtensions(args.Package);
                }
                else if (args.Package.Status.Servicing || args.Package.Status.DeploymentInProgress)
                {
                    // if the package is being serviced or deployed, ignore the status events
                }
                else
                {
                    RemoveExtensions(args.Package);
                }
            }
            else 
            {
                LoadExtensions(args.Package);
            }
        }

        public async Task LoadExtension(AppExtension ext)
        {
            string identifier = $"{ext.AppInfo.AppUserModelId}!{ext.Id}";

            if (!ext.Package.Status.VerifyIsOK()
                )
            {
                return; 
            }

            Extension existingExt = Extensions.Where(e => e.UniqueId == identifier).FirstOrDefault();

            if (existingExt == null)
            {
                var properties = await ext.GetExtensionPropertiesAsync() as PropertySet;
                var filestream = await ext.AppInfo.DisplayInfo.GetLogo(new Windows.Foundation.Size(1, 1)).OpenReadAsync();
                BitmapImage logo = new BitmapImage();
                logo.SetSource(filestream);

                Extension newExtension = new Extension(ext, properties, logo);
                Extensions.Add(newExtension);

                await newExtension.MarkAsLoaded();
            }
            else 
            {
                existingExt.Unload();

                await existingExt.Update(ext);
            }
        }

        public void LoadExtensions(Package package)
        {
            Extensions.Where(ext => ext.AppExtension.Package.Id.FamilyName == package.Id.FamilyName).ToList().ForEach(async e => { await e.MarkAsLoaded(); });
        }

        public void UnloadExtensions(Package package)
        {
            Extensions.Where(ext => ext.AppExtension.Package.Id.FamilyName == package.Id.FamilyName).ToList().ForEach(e => { e.Unload(); });
        }

        public void RemoveExtensions(Package package)
        {
            Extensions.Where(ext => ext.AppExtension.Package.Id.FamilyName == package.Id.FamilyName).ToList().ForEach(e => { e.Unload(); Extensions.Remove(e); });
        }

        public async void RemoveExtension(Extension ext)
        {
            await catalog.RequestRemovePackageAsync(ext.AppExtension.Package.Id.FullName);
        }

        #region Extra exceptions

        // For exceptions using the Extension Manager
        public class ExtensionManagerException : Exception
        {
            public ExtensionManagerException()
            {
            }

            public ExtensionManagerException(string message) : base(message)
            {
            }

            public ExtensionManagerException(string message, Exception inner) : base(message, inner)
            {
            }
        }

        #endregion Extra exceptions
    }
}