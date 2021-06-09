using Files.Extensions;
using System;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Files.Helpers
{
    public static class ThemeHelper
    {
        private const string selectedAppThemeKey = "theme";
        private static Window currentApplicationWindow;
        private static ApplicationViewTitleBar titleBar;

        public static UISettings UiSettings;

        public static ElementTheme RootTheme
        {
            get
            {
                var savedTheme = ApplicationData.Current.LocalSettings.Values[selectedAppThemeKey]?.ToString();

                if (!string.IsNullOrEmpty(savedTheme))
                {
                    return EnumExtensions.GetEnum<ElementTheme>(savedTheme);
                }
                else
                {
                    return ElementTheme.Default;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values[selectedAppThemeKey] = value.ToString();
                ApplyTheme();
            }
        }

        public static void Initialize()
        {
            currentApplicationWindow = Window.Current;

            titleBar = ApplicationView.GetForCurrentView().TitleBar;

            ApplyTheme();

            UiSettings = new UISettings();
            UiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
        }

        private static async void UiSettings_ColorValuesChanged(UISettings sender, object args)
        {
            if (currentApplicationWindow != null)
            {
                await currentApplicationWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
                {
                    ApplyTheme();
                });
            }
        }

        private static void ApplyTheme()
        {
            var rootTheme = RootTheme;

            if (Window.Current.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = rootTheme;
            }

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            switch (rootTheme)
            {
                case ElementTheme.Default:
                    App.AppSettings.AcrylicTheme.SetDefaultTheme();
                    titleBar.ButtonHoverBackgroundColor = (Color)Application.Current.Resources["SystemBaseLowColor"];
                    titleBar.ButtonForegroundColor = (Color)Application.Current.Resources["SystemBaseHighColor"];
                    break;

                case ElementTheme.Light:
                    App.AppSettings.AcrylicTheme.SetLightTheme();
                    titleBar.ButtonHoverBackgroundColor = Color.FromArgb(51, 0, 0, 0);
                    titleBar.ButtonForegroundColor = Colors.Black;
                    break;

                case ElementTheme.Dark:
                    App.AppSettings.AcrylicTheme.SetDarkTheme();
                    titleBar.ButtonHoverBackgroundColor = Color.FromArgb(51, 255, 255, 255);
                    titleBar.ButtonForegroundColor = Colors.White;
                    break;
            }
            App.AppSettings.UpdateThemeElements.Execute(null);
        }
    }
}