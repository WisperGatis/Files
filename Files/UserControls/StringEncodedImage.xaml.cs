﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;


namespace Files.UserControls
{
    public sealed partial class StringEncodedImage : UserControl
    {
        public StringEncodedImage()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty EncodedImageProperty =
            DependencyProperty.Register("EncodedImage", typeof(string), typeof(StringEncodedImage), null);

        public string EncodedImage
        {
            get => (string)GetValue(EncodedImageProperty);
            set
            {
                SetValue(EncodedImageProperty, value);
                SetImageFromString(value);
            }
        }

        private async void SetImageFromString(string encodedImage)
        {
            try
            {
                var array = Convert.FromBase64String(encodedImage);
                var buffer = array.AsBuffer();
                var source = new BitmapImage();
                var stream = buffer.AsStream();
                var rastream = stream.AsRandomAccessStream();
                await source.SetSourceAsync(rastream);
                MainImage.Source = source;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Loading image from string failed with the following exception: {e}");
            }
        }
    }
}