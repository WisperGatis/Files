using Files.Enums;
using Files.Filesystem.FilesystemHistory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Files.Filesystem
{
    public interface IFilesystemHelpers : IDisposable
    {
        Task<(ReturnResult, IStorageItem)> CreateAsync(IStorageItemWithPath source, bool registerHistory);

        #region Delete

        Task<ReturnResult> DeleteItemsAsync(IEnumerable<IStorageItem> source, bool showDialog, bool permanently, bool registerHistory);

        Task<ReturnResult> DeleteItemAsync(IStorageItem source, bool showDialog, bool permanently, bool registerHistory);

        Task<ReturnResult> DeleteItemsAsync(IEnumerable<IStorageItemWithPath> source, bool showDialog, bool permanently, bool registerHistory);

        Task<ReturnResult> DeleteItemAsync(IStorageItemWithPath source, bool showDialog, bool permanently, bool registerHistory);

        #endregion Delete

        Task<ReturnResult> RestoreFromTrashAsync(IStorageItemWithPath source, string destination, bool registerHistory);

        Task<ReturnResult> PerformOperationTypeAsync(DataPackageOperation operation, DataPackageView packageView, string destination, bool showDialog, bool registerHistory, bool isDestinationExecutable = false);

        #region Copy

        Task<ReturnResult> CopyItemsAsync(IEnumerable<IStorageItem> source, IEnumerable<string> destination, bool showDialog, bool registerHistory);

        Task<ReturnResult> CopyItemAsync(IStorageItem source, string destination, bool showDialog, bool registerHistory);

        Task<ReturnResult> CopyItemsAsync(IEnumerable<IStorageItemWithPath> source, IEnumerable<string> destination, bool showDialog, bool registerHistory);

        Task<ReturnResult> CopyItemAsync(IStorageItemWithPath source, string destination, bool showDialog, bool registerHistory);

        Task<ReturnResult> CopyItemsFromClipboard(DataPackageView packageView, string destination, bool showDialog, bool registerHistory);

        #endregion Copy

        #region Move

        Task<ReturnResult> MoveItemsAsync(IEnumerable<IStorageItem> source, IEnumerable<string> destination, bool showDialog, bool registerHistory);

        Task<ReturnResult> MoveItemAsync(IStorageItem source, string destination, bool showDialog, bool registerHistory);

        Task<ReturnResult> MoveItemsAsync(IEnumerable<IStorageItemWithPath> source, IEnumerable<string> destination, bool showDialog, bool registerHistory);

        Task<ReturnResult> MoveItemAsync(IStorageItemWithPath source, string destination, bool showDialog, bool registerHistory);

        Task<ReturnResult> MoveItemsFromClipboard(DataPackageView packageView, string destination, bool showDialog, bool registerHistory);

        #endregion Move

        Task<ReturnResult> RenameAsync(IStorageItem source, string newName, NameCollisionOption collision, bool registerHistory);

        Task<ReturnResult> RenameAsync(IStorageItemWithPath source, string newName, NameCollisionOption collision, bool registerHistory);
    }
}