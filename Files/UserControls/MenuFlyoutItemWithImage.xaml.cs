﻿using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;


namespace Files.UserControls
{
    public sealed partial class MenuFlyoutItemWithImage : MenuFlyoutItem
    {
        public BitmapImage BitmapIcon
        {
            get { return (BitmapImage)GetValue(BitmapIconProperty); }
            set { SetValue(BitmapIconProperty, value); }
        }

        public static readonly DependencyProperty BitmapIconProperty =
            DependencyProperty.Register("BitmapIcon", typeof(BitmapImage), typeof(MenuFlyoutItemWithImage), new PropertyMetadata(null));

        public MenuFlyoutItemWithImage()
        {
            this.InitializeComponent();
        }
    }
}