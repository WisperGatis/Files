namespace Files
{
    public static class Constants
    {
        public static class ImageRes
        {
            public const int QuickAccess = 1024;
            public const int Desktop = 183;
            public const int Downloads = 184;
            public const int Documents = 112;
            public const int Pictures = 113;
            public const int Music = 108;
            public const int Videos = 189;
            public const int GenericDiskDrive = 35;
            public const int WindowsDrive = 36;
            public const int ThisPC = 109;
            public const int NetworkDrives = 25;
            public const int RecycleBin = 55;
            public const int CloudDrives = 1040;
            public const int OneDrive = 1043;
            public const int Libraries = 1023;
            public const int Folder = 3;
        }

        public static class UI
        {
            public const float DimItemOpacity = 0.4f;
        }

        public static class Browser
        {
            public static class GridViewBrowser
            {
                public const int GridViewIncrement = 20;

                public const int GridViewSizeMax = 300;

                public const int GridViewSizeLarge = 220;

                public const int GridViewSizeMedium = 160;

                public const int GridViewSizeSmall = 100;

                public const int TilesView = 260;
            }

            public static class DetailsLayoutBrowser
            {
                public const int DetailsViewSize = 28;
            }

            public static class ColumnViewBrowser
            {
                public const int ColumnViewSize = 28;
            }
        }

        public static class Widgets
        {
            public static class Bundles
            {
                public const int MaxAmountOfItemsPerBundle = 8;
            }

            public static class Drives
            {
                public const float LowStorageSpacePercentageThreshold = 90.0f;
            }
        }

        public static class LocalSettings
        {
            public const string DateTimeFormat = "datetimeformat";

            public const string Theme = "theme";

            public const string SettingsFolderName = "settings";

            public const string BundlesSettingsFileName = "bundles.json";
        }

        public static class PreviewPane
        {
            public const int TextCharacterLimit = 50000;

            public const int PDFPageLimit = 10;

            public const long TryLoadAsTextSizeLimit = 1000000;

            public const int FolderPreviewThumbnailCount = 10;
        }

        public static class ResourceFilePaths
        {
            public const string DetailsPagePropertiesJsonPath = @"ms-appx:///Resources/PropertiesInformation.json";

            public const string PreviewPaneDetailsPropertiesJsonPath = @"ms-appx:///Resources/PreviewPanePropertiesInformation.json";
        }

        public static class OptionalPackages
        {
            public const string ThemesOptionalPackagesName = "49306atecsolution.ThemesforFiles";
        }
    }
}