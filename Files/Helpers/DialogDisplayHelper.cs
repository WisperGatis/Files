using Files.Dialogs;
using Files.Enums;
using Files.ViewModels.Dialogs;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Files.Helpers
{
    internal class DialogDisplayHelper
    {
        public static async Task<bool> ShowDialogAsync(string title, string message, string primaryText = "OK", string secondaryText = null)
        {
            bool result = false;

            try
            {
                if (Window.Current.Content is Frame rootFrame)
                {
                    DynamicDialog dialog = new DynamicDialog(new DynamicDialogViewModel()
                    {
                        TitleText = title,
                        SubtitleText = message, 
                        PrimaryButtonText = primaryText,
                        SecondaryButtonText = secondaryText,
                        DynamicButtons = DynamicDialogButtons.Primary | DynamicDialogButtons.Secondary
                    });

                    await dialog.ShowAsync();

                    result = dialog.DynamicResult == DynamicDialogResult.Primary;
                }
            }
            catch (Exception)
            {
            }

            return result;
        }
    }
}