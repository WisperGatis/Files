﻿using Files.DataModels;
using Files.DataModels.NavigationControlItems;
using Files.Filesystem;
using Files.Helpers;
using Files.UserControls.MultitaskingControl;
using Files.ViewModels;
using Microsoft.Toolkit.Uwp;
using Microsoft.Toolkit.Uwp.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Files.UserControls
{
    public sealed partial class SidebarControl : Microsoft.UI.Xaml.Controls.NavigationView, INotifyPropertyChanged
    {
        public static SemaphoreSlim SideBarItemsSemaphore = new SemaphoreSlim(1, 1);

        public static BulkConcurrentObservableCollection<INavigationControlItem> SideBarItems { get; private set; } = new BulkConcurrentObservableCollection<INavigationControlItem>();

        public SettingsViewModel AppSettings => App.AppSettings;

        public delegate void SidebarItemInvokedEventHandler(object sender, SidebarItemInvokedEventArgs e);

        public event SidebarItemInvokedEventHandler SidebarItemInvoked;

        public delegate void SidebarItemNewPaneInvokedEventHandler(object sender, SidebarItemNewPaneInvokedEventArgs e);

        public event SidebarItemNewPaneInvokedEventHandler SidebarItemNewPaneInvoked;

        public delegate void SidebarItemPropertiesInvokedEventHandler(object sender, SidebarItemPropertiesInvokedEventArgs e);

        public event SidebarItemPropertiesInvokedEventHandler SidebarItemPropertiesInvoked;

        public delegate void SidebarItemDroppedEventHandler(object sender, SidebarItemDroppedEventArgs e);

        public event SidebarItemDroppedEventHandler SidebarItemDropped;

        public event EventHandler RecycleBinItemRightTapped;

        public SidebarPinnedModel SidebarPinnedModel => App.SidebarPinnedController.Model;

        public static readonly DependencyProperty EmptyRecycleBinCommandProperty = DependencyProperty.Register(nameof(EmptyRecycleBinCommand), typeof(ICommand), typeof(SidebarControl), new PropertyMetadata(null));

        public ICommand EmptyRecycleBinCommand
        {
            get => (ICommand)GetValue(EmptyRecycleBinCommandProperty);
            set => SetValue(EmptyRecycleBinCommandProperty, value);
        }

        private bool IsInPointerPressed = false;

        private DispatcherQueueTimer dragOverSectionTimer, dragOverItemTimer;

        public SidebarControl()
        {
            this.InitializeComponent();
            this.Loaded += SidebarNavView_Loaded;

            dragOverSectionTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            dragOverItemTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        }

        public static readonly DependencyProperty SelectedSidebarItemProperty = DependencyProperty.Register(nameof(SelectedSidebarItem), typeof(INavigationControlItem), typeof(SidebarControl), new PropertyMetadata(null));

        public INavigationControlItem SelectedSidebarItem
        {
            get => (INavigationControlItem)GetValue(SelectedSidebarItemProperty);
            set
            {
                if (this.IsLoaded)
                {
                    SetValue(SelectedSidebarItemProperty, value);
                }
            }
        }
        
        public static readonly DependencyProperty TabContentProperty = DependencyProperty.Register(nameof(TabContent), typeof(UIElement), typeof(SidebarControl), new PropertyMetadata(null));

        public UIElement TabContent
        {
            get => (UIElement)GetValue(TabContentProperty);
            set => SetValue(TabContentProperty, value);
        }

        private bool canOpenInNewPane;

        public bool CanOpenInNewPane
        {
            get => canOpenInNewPane;
            set
            {
                if (value != canOpenInNewPane)
                {
                    canOpenInNewPane = value;
                    NotifyPropertyChanged(nameof(CanOpenInNewPane));
                }
            }
        }

        private bool showUnpinItem;

        public bool ShowUnpinItem
        {
            get => showUnpinItem;
            set
            {
                if (value != showUnpinItem)
                {
                    showUnpinItem = value;
                    NotifyPropertyChanged(nameof(ShowUnpinItem));
                }
            }
        }

        private bool showProperties;

        public bool ShowProperties
        {
            get => showProperties;
            set
            {
                if (value != showProperties)
                {
                    showProperties = value;
                    NotifyPropertyChanged(nameof(ShowProperties));
                }
            }
        }

        private bool showEmptyRecycleBin;

        public bool ShowEmptyRecycleBin
        {
            get => showEmptyRecycleBin;
            set
            {
                if (value != showEmptyRecycleBin)
                {
                    showEmptyRecycleBin = value;
                    NotifyPropertyChanged(nameof(ShowEmptyRecycleBin));
                }
            }
        }

        private bool showEjectDevice;

        public bool ShowEjectDevice
        {
            get => showEjectDevice;
            set
            {
                if (value != showEjectDevice)
                {
                    showEjectDevice = value;
                    NotifyPropertyChanged(nameof(ShowEjectDevice));
                }
            }
        }

        private bool recycleBinHasItems;

        public bool RecycleBinHasItems
        {
            get => recycleBinHasItems;
            set
            {
                if (value != recycleBinHasItems)
                {
                    recycleBinHasItems = value;
                    NotifyPropertyChanged(nameof(RecycleBinHasItems));
                }
            }
        }

        public INavigationControlItem RightClickedItem;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void UnpinItem_Click(object sender, RoutedEventArgs e)
        {
            if (RightClickedItem.Path.Equals(AppSettings.RecycleBinPath, StringComparison.OrdinalIgnoreCase))
            {
                AppSettings.PinRecycleBinToSideBar = false;
            }
            else if (RightClickedItem.Section == SectionType.Favorites)
            {
                App.SidebarPinnedController.Model.RemoveItem(RightClickedItem.Path.ToString());
            }
        }

        public static GridLength GetSidebarCompactSize()
        {
            if (App.Current.Resources.TryGetValue("NavigationViewCompactPaneLength", out object paneLength))
            {
                if (paneLength is double paneLengthDouble)
                {
                    return new GridLength(paneLengthDouble);
                }
            }
            return new GridLength(200);
        }

        private async void Sidebar_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            if (IsInPointerPressed || args.InvokedItem == null || args.InvokedItemContainer == null)
            {
                IsInPointerPressed = false;
                return;
            }

            var ctrlPressed = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            if (ctrlPressed && !(args.InvokedItemContainer.Tag is null))
            {
                string navigationPath = args.InvokedItemContainer.Tag.ToString();
                await NavigationHelpers.OpenPathInNewTab(navigationPath);
                return;
            }

            SidebarItemInvoked?.Invoke(this, new SidebarItemInvokedEventArgs(args.InvokedItemContainer));
        }

        private async void Sidebar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var properties = e.GetCurrentPoint(null).Properties;
            var context = (sender as Microsoft.UI.Xaml.Controls.NavigationViewItem).DataContext;
            if (properties.IsMiddleButtonPressed && context is INavigationControlItem item)
            {
                IsInPointerPressed = true;
                await NavigationHelpers.OpenPathInNewTab(item.Path);
                e.Handled = true;
            }
        }

        private void NavigationViewLocationItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var sidebarItem = sender as Microsoft.UI.Xaml.Controls.NavigationViewItem;
            var item = sidebarItem.DataContext as LocationItem;

            if (!item.Text.Equals("SidebarDrives".GetLocalized()) &&
                !item.Text.Equals("SidebarNetworkDrives".GetLocalized()) &&
                !item.Text.Equals("SidebarCloudDrives".GetLocalized()) &&
                !item.Text.Equals("SidebarLibraries".GetLocalized()) &&
                !item.Text.Equals("WSL") &&
                !item.Text.Equals("SidebarFavorites".GetLocalized()))
            {
                bool isLibrary = item.Section == SectionType.Library;
                bool isFavorites = item.Section == SectionType.Favorites;
                ShowUnpinItem = isLibrary && !item.IsDefaultLocation || isFavorites && !item.IsDefaultLocation;
                ShowProperties = isLibrary && !item.IsDefaultLocation || isFavorites && !item.IsDefaultLocation;
                ShowEjectDevice = false;

                if (string.Equals(item.Path, App.AppSettings.RecycleBinPath, StringComparison.OrdinalIgnoreCase))
                {
                    RecycleBinItemRightTapped?.Invoke(this, EventArgs.Empty);
                    ShowEmptyRecycleBin = true;
                    ShowUnpinItem = true;
                }
                else
                {
                    ShowEmptyRecycleBin = false;
                }

                RightClickedItem = item;
                SideBarItemContextFlyout.ShowAt(sidebarItem, e.GetPosition(sidebarItem));
            }

            e.Handled = true;
        }

        private void NavigationViewDriveItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var sidebarItem = sender as Microsoft.UI.Xaml.Controls.NavigationViewItem;
            var item = sidebarItem.DataContext as DriveItem;

            ShowEjectDevice = item.IsRemovable;
            ShowUnpinItem = false;
            ShowEmptyRecycleBin = false;
            ShowProperties = true;

            SideBarItemContextFlyout.ShowAt(sidebarItem, e.GetPosition(sidebarItem));

            RightClickedItem = item;

            e.Handled = true;
        }

        private void NavigationViewWSLItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var sidebarItem = sender as Microsoft.UI.Xaml.Controls.NavigationViewItem;
            var item = sidebarItem.DataContext as WslDistroItem;

            ShowEjectDevice = false;
            ShowUnpinItem = false;
            ShowEmptyRecycleBin = false;
            ShowProperties = true;

            SideBarItemContextFlyout.ShowAt(sidebarItem, e.GetPosition(sidebarItem));

            RightClickedItem = item;

            e.Handled = true;
        }

        private async void OpenInNewTab_Click(object sender, RoutedEventArgs e)
        {
            await NavigationHelpers.OpenPathInNewTab(RightClickedItem.Path);
        }

        private async void OpenInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            await NavigationHelpers.OpenPathInNewWindowAsync(RightClickedItem.Path);
        }

        private void NavigationViewItem_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            if (!((sender as Microsoft.UI.Xaml.Controls.NavigationViewItem).DataContext is LocationItem locationItem))
            {
                return;
            }

            var navItem = (sender as Microsoft.UI.Xaml.Controls.NavigationViewItem);
            args.Data.Properties.Add("sourceLocationItem", navItem);
        }

        private object dragOverSection, dragOverItem = null;

        private void NavigationViewItem_DragEnter(object sender, DragEventArgs e)
        {
            VisualStateManager.GoToState(sender as Microsoft.UI.Xaml.Controls.NavigationViewItem, "DragEnter", false);

            if ((sender as Microsoft.UI.Xaml.Controls.NavigationViewItem).DataContext is INavigationControlItem iNavItem)
            {
                if (string.IsNullOrEmpty(iNavItem.Path))
                {
                    dragOverSection = sender;
                    dragOverSectionTimer.Stop();
                    dragOverSectionTimer.Debounce(() =>
                    {
                        if (dragOverSection != null)
                        {
                            dragOverSectionTimer.Stop();
                            if ((dragOverSection as Microsoft.UI.Xaml.Controls.NavigationViewItem).DataContext is LocationItem section)
                            {
                                section.IsExpanded = true;
                            }
                            dragOverSection = null;
                        }
                    }, TimeSpan.FromMilliseconds(1000), false);
                }
                else
                {
                    dragOverItem = sender;
                    dragOverItemTimer.Stop();
                    dragOverItemTimer.Debounce(() =>
                    {
                        if (dragOverItem != null)
                        {
                            dragOverItemTimer.Stop();
                            SidebarItemInvoked?.Invoke(this, new SidebarItemInvokedEventArgs(dragOverItem as Microsoft.UI.Xaml.Controls.NavigationViewItemBase));
                            dragOverItem = null;
                        }
                    }, TimeSpan.FromMilliseconds(1000), false);
                }
            }
        }

        private void NavigationViewItem_DragLeave(object sender, DragEventArgs e)
        {
            VisualStateManager.GoToState(sender as Microsoft.UI.Xaml.Controls.NavigationViewItem, "DragLeave", false);

            if ((sender as Microsoft.UI.Xaml.Controls.NavigationViewItem).DataContext is INavigationControlItem)
            {
                if (sender == dragOverItem)
                {
                    dragOverItem = null;
                }
                if (sender == dragOverSection)
                {
                    dragOverSection = null;
                }
            }
        }

        private async void NavigationViewLocationItem_DragOver(object sender, DragEventArgs e)
        {
            if (!((sender as Microsoft.UI.Xaml.Controls.NavigationViewItem)?.DataContext is LocationItem locationItem))
            {
                return;
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var deferral = e.GetDeferral();
                e.Handled = true;
                IReadOnlyList<IStorageItem> storageItems;
                try
                {
                    storageItems = await e.DataView.GetStorageItemsAsync();
                }
                catch (Exception ex) when ((uint)ex.HResult == 0x80040064)
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                    deferral.Complete();
                    return;
                }
                catch (Exception ex)
                {
                    App.Logger.Warn(ex, ex.Message);
                    e.AcceptedOperation = DataPackageOperation.None;
                    deferral.Complete();
                    return;
                }

                if (storageItems.Count == 0 ||
                    string.IsNullOrEmpty(locationItem.Path) ||
                    locationItem.Path.Equals(App.AppSettings.RecycleBinPath, StringComparison.OrdinalIgnoreCase) ||
                    storageItems.AreItemsAlreadyInFolder(locationItem.Path))
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                }
                else
                {
                    e.DragUIOverride.IsCaptionVisible = true;
                    if (storageItems.AreItemsInSameDrive(locationItem.Path) || locationItem.IsDefaultLocation)
                    {
                        e.AcceptedOperation = DataPackageOperation.Move;
                        e.DragUIOverride.Caption = string.Format("MoveToFolderCaptionText".GetLocalized(), locationItem.Text);
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        e.DragUIOverride.Caption = string.Format("CopyToFolderCaptionText".GetLocalized(), locationItem.Text);
                    }
                }

                deferral.Complete();
            }
            else if ((e.DataView.Properties["sourceLocationItem"] as Microsoft.UI.Xaml.Controls.NavigationViewItem)?.DataContext is LocationItem sourceLocationItem)
            {

                NavigationViewLocationItem_DragOver_SetCaptions(locationItem, sourceLocationItem, e);
            }
        }

        private void NavigationViewLocationItem_DragOver_SetCaptions(LocationItem senderLocationItem, LocationItem sourceLocationItem, DragEventArgs e)
        {
            if (sourceLocationItem.Equals(senderLocationItem))
            {
                e.AcceptedOperation = DataPackageOperation.None;
                e.DragUIOverride.IsCaptionVisible = false;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.IsCaptionVisible = true;
                e.DragUIOverride.Caption = "PinToSidebarByDraggingCaptionText".GetLocalized();
            }
        }

        private void NavigationViewLocationItem_Drop(object sender, DragEventArgs e)
        {
            dragOverItem = null; 
            dragOverSection = null; 

            if (!((sender as Microsoft.UI.Xaml.Controls.NavigationViewItem).DataContext is LocationItem locationItem))
            {
                return;
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                VisualStateManager.GoToState(sender as Microsoft.UI.Xaml.Controls.NavigationViewItem, "Drop", false);

                var deferral = e.GetDeferral();
                SidebarItemDropped?.Invoke(this, new SidebarItemDroppedEventArgs()
                {
                    Package = e.DataView,
                    ItemPath = locationItem.Path,
                    AcceptedOperation = e.AcceptedOperation
                });
                deferral.Complete();
            }
            else if ((e.DataView.Properties["sourceLocationItem"] as Microsoft.UI.Xaml.Controls.NavigationViewItem).DataContext is LocationItem sourceLocationItem)
            {
                SidebarPinnedModel.SwapItems(sourceLocationItem, locationItem);
            }
        }

        private async void NavigationViewDriveItem_DragOver(object sender, DragEventArgs e)
        {
            if (!((sender as Microsoft.UI.Xaml.Controls.NavigationViewItem).DataContext is DriveItem driveItem) ||
                !e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                return;
            }

            var deferral = e.GetDeferral();
            e.Handled = true;
            IReadOnlyList<IStorageItem> storageItems;
            try
            {
                storageItems = await e.DataView.GetStorageItemsAsync();
            }
            catch (Exception ex) when ((uint)ex.HResult == 0x80040064)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                deferral.Complete();
                return;
            }
            catch (Exception ex)
            {
                App.Logger.Warn(ex, ex.Message);
                e.AcceptedOperation = DataPackageOperation.None;
                deferral.Complete();
                return;
            }

            if (storageItems.Count == 0 ||
                "DriveCapacityUnknown".GetLocalized().Equals(driveItem.SpaceText, StringComparison.OrdinalIgnoreCase) ||
                storageItems.AreItemsAlreadyInFolder(driveItem.Path))
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
            else
            {
                e.DragUIOverride.IsCaptionVisible = true;
                if (storageItems.AreItemsInSameDrive(driveItem.Path))
                {
                    e.AcceptedOperation = DataPackageOperation.Move;
                    e.DragUIOverride.Caption = string.Format("MoveToFolderCaptionText".GetLocalized(), driveItem.Text);
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                    e.DragUIOverride.Caption = string.Format("CopyToFolderCaptionText".GetLocalized(), driveItem.Text);
                }
            }

            deferral.Complete();
        }

        private void NavigationViewDriveItem_Drop(object sender, DragEventArgs e)
        {
            dragOverItem = null; 
            dragOverSection = null; 

            if (!((sender as Microsoft.UI.Xaml.Controls.NavigationViewItem).DataContext is DriveItem driveItem))
            {
                return;
            }

            VisualStateManager.GoToState(sender as Microsoft.UI.Xaml.Controls.NavigationViewItem, "Drop", false);

            var deferral = e.GetDeferral();
            SidebarItemDropped?.Invoke(this, new SidebarItemDroppedEventArgs()
            {
                Package = e.DataView,
                ItemPath = driveItem.Path,
                AcceptedOperation = e.AcceptedOperation
            });
            deferral.Complete();
        }

        private void Properties_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuFlyoutItem).DataContext;
            SidebarItemPropertiesInvoked?.Invoke(this, new SidebarItemPropertiesInvokedEventArgs(item));
        }

        private async void EjectDevice_Click(object sender, RoutedEventArgs e)
        {
            await DriveHelpers.EjectDeviceAsync(RightClickedItem.Path);
        }

        private void SidebarNavView_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = (Microsoft.UI.Xaml.Controls.NavigationViewItem)this.SettingsItem;
            settings.SelectsOnInvoked = false;

            (this.FindDescendant("TabContentBorder") as Border).Child = TabContent;
        }

        private void Border_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var step = 1;
            var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
            if (ctrl.HasFlag(CoreVirtualKeyStates.Down))
            {
                step = 5;
            }

            if (e.Key == VirtualKey.Left)
            {
                IncrementSize(-step);
                e.Handled = true;
            } else if(e.Key == VirtualKey.Right)
            {
                IncrementSize(step);
                e.Handled = true;
            }
        }

        private void Border_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if(IsPaneOpen)
            {
                IncrementSize(e.Delta.Translation.X);
            }
        }

        private void Border_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
        }

        private void IncrementSize(double val)
        {
            var newSize = AppSettings.SidebarWidth.Value + val;
            AppSettings.SidebarWidth = new GridLength(newSize >= 0 ? newSize : 0);
        }

        private void Border_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (IsPaneOpen)
            {
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.SizeWestEast, 0);
            }
        }

        private void OpenInNewPane_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuFlyoutItem).DataContext;
            SidebarItemNewPaneInvoked?.Invoke(this, new SidebarItemNewPaneInvokedEventArgs(item));
        }

        private void DragArea_Loaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SetTitleBar(sender as Grid);
        }
    }

    public class SidebarItemDroppedEventArgs : EventArgs
    {
        public DataPackageView Package { get; set; }
        public string ItemPath { get; set; }
        public DataPackageOperation AcceptedOperation { get; set; }
    }

    public class SidebarItemInvokedEventArgs : EventArgs
    {
        public Microsoft.UI.Xaml.Controls.NavigationViewItemBase InvokedItemContainer { get; set; }

        public SidebarItemInvokedEventArgs(Microsoft.UI.Xaml.Controls.NavigationViewItemBase ItemContainer)
        {
            InvokedItemContainer = ItemContainer;
        }
    }

    public class SidebarItemPropertiesInvokedEventArgs : EventArgs
    {
        public object InvokedItemDataContext { get; set; }

        public SidebarItemPropertiesInvokedEventArgs(object invokedItemDataContext)
        {
            InvokedItemDataContext = invokedItemDataContext;
        }
    }

    public class SidebarItemNewPaneInvokedEventArgs : EventArgs
    {
        public object InvokedItemDataContext { get; set; }

        public SidebarItemNewPaneInvokedEventArgs(object invokedItemDataContext)
        {
            InvokedItemDataContext = invokedItemDataContext;
        }
    }

    public class NavItemDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate LocationNavItemTemplate { get; set; }
        public DataTemplate DriveNavItemTemplate { get; set; }
        public DataTemplate LinuxNavItemTemplate { get; set; }
        public DataTemplate HeaderNavItemTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item != null && item is INavigationControlItem)
            {
                INavigationControlItem navControlItem = item as INavigationControlItem;
                switch (navControlItem.ItemType)
                {
                    case NavigationControlItemType.Location:
                        return LocationNavItemTemplate;

                    case NavigationControlItemType.Drive:
                        return DriveNavItemTemplate;

                    case NavigationControlItemType.CloudDrive:
                        return DriveNavItemTemplate;

                    case NavigationControlItemType.LinuxDistro:
                        return LinuxNavItemTemplate;

                    case NavigationControlItemType.Header:
                        return HeaderNavItemTemplate;
                }
            }
            return null;
        }
    }
}