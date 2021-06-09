using Files.ViewModels;
using Windows.UI.Xaml.Controls;


namespace Files.Dialogs
{
    public sealed partial class PropertiesDialog : ContentDialog
    {
        public SettingsViewModel AppSettings => App.AppSettings;

        public PropertiesDialog()
        {
            this.InitializeComponent();
        }
    }
}