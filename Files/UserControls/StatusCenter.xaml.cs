using Files.Enums;
using Files.Helpers;
using Files.Interacts;
using Files.ViewModels;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Uwp;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;


namespace Files.UserControls
{
    public sealed partial class StatusCenter : UserControl
    {
        public StatusCenterViewModel StatusCenterViewModel { get; set; }
        public StatusCenter()
        {
            this.InitializeComponent();
        }

        private void DismissBanner(object sender, RoutedEventArgs e)
        {
            StatusBanner itemToDismiss = (sender as Button).DataContext as StatusBanner;
            StatusCenterViewModel.CloseBanner(itemToDismiss);
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            StatusBanner itemToDismiss = (sender as Button).DataContext as StatusBanner;
            await Task.Run(itemToDismiss.PrimaryButtonClick);
            StatusCenterViewModel.CloseBanner(itemToDismiss);
        }
    }
}