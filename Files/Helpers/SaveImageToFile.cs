using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Files.Helpers
{
    internal static class SaveImageToFile
    {
        public static async Task SaveSoftwareBitmapToFile(SoftwareBitmap softwareBitmap, StorageFile outputFile, Guid encoderId)
        {
            using IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);

            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);

            encoder.SetSoftwareBitmap(softwareBitmap);

            try
            {
                await encoder.FlushAsync();
            }
            catch (Exception err)
            {
                const int WINCODEC_ERR_UNSUPPORTEDOPERATION = unchecked((int)0x88982F81);
                switch (err.HResult)
                {
                    case WINCODEC_ERR_UNSUPPORTEDOPERATION:
                        encoder.IsThumbnailGenerated = false;
                        break;

                    default:
                        throw;
                }
            }

            if (encoder.IsThumbnailGenerated == false)
            {
                await encoder.FlushAsync();
            }
        }
    }
}