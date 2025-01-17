﻿using Files.Common;
using Microsoft.Toolkit.Uwp;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Collections;
using Windows.UI.Xaml.Media.Imaging;

namespace Files.Helpers
{
    public static class FileThumbnailHelper
    {
        public static async Task<(byte[] IconData, byte[] OverlayData, bool IsCustom)> LoadIconOverlayAsync(string filePath, uint thumbnailSize)
        {
            var connection = await AppServiceConnectionHelper.Instance;
            if (connection != null)
            {
                var value = new ValueSet
                {
                    { "Arguments", "GetIconOverlay" },
                    { "filePath", filePath },
                    { "thumbnailSize", (int)thumbnailSize }
                };
                var (status, response) = await connection.SendMessageForResponseAsync(value);
                if (status == AppServiceResponseStatus.Success)
                {
                    var hasCustomIcon = response.Get("HasCustomIcon", false);
                    var icon = response.Get("Icon", (string)null);
                    var overlay = response.Get("Overlay", (string)null);

                    return (icon == null ? null : Convert.FromBase64String(icon),
                        overlay == null ? null : Convert.FromBase64String(overlay),
                        hasCustomIcon);
                }
            }
            return (null, null, false);
        }

        public static async Task<byte[]> LoadIconWithoutOverlayAsync(string filePath, uint thumbnailSize)
        {
            var Connection = await AppServiceConnectionHelper.Instance;
            if (Connection != null)
            {
                var value = new ValueSet();
                value.Add("Arguments", "GetIconWithoutOverlay");
                value.Add("filePath", filePath);
                value.Add("thumbnailSize", (int)thumbnailSize);
                var (status, response) = await Connection.SendMessageForResponseAsync(value);
                if (status == AppServiceResponseStatus.Success)
                {
                    var icon = response.Get("Icon", (string)null);

                    return (icon == null ? null : Convert.FromBase64String(icon));
                }
            }
            return null;
        }
    }
}