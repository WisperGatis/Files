using Files.Enums;
using Files.Helpers;
using Files.ViewModels.Dialogs;
using Microsoft.Toolkit.Uwp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace Files.DataModels
{
    public struct FilesystemItemsOperationItemModel
    {
        public FilesystemOperationType OperationType;

        public string SourcePath;

        public string DestinationPath;

        public FilesystemItemsOperationItemModel(FilesystemOperationType operationType, string sourcePath, string destinationPath)
        {
            this.OperationType = operationType;
            this.SourcePath = sourcePath;
            this.DestinationPath = destinationPath;
        }
    }

    public struct FilesystemItemsOperationDataModel
    {
        public FilesystemOperationType OperationType;

        public bool MustResolveConflicts;

        public bool PermanentlyDelete;

        public bool PermanentlyDeleteEnabled;

        public List<FilesystemItemsOperationItemModel> IncomingItems;

        public List<FilesystemItemsOperationItemModel> ConflictingItems;

        public FilesystemItemsOperationDataModel(FilesystemOperationType operationType, bool mustResolveConflicts, bool permanentlyDelete, bool permanentlyDeleteEnabled, List<FilesystemItemsOperationItemModel> incomingItems, List<FilesystemItemsOperationItemModel> conflictingItems)
        {
            this.OperationType = operationType;
            this.MustResolveConflicts = mustResolveConflicts;
            this.PermanentlyDelete = permanentlyDelete;
            this.PermanentlyDeleteEnabled = permanentlyDeleteEnabled;
            this.IncomingItems = incomingItems;
            this.ConflictingItems = conflictingItems;
        }

        public async Task<List<FilesystemOperationItemViewModel>> ToItems(Action updatePrimaryButtonEnabled, Action optionGenerateNewName, Action optionReplaceExisting, Action optionSkip)
        {
            List<FilesystemOperationItemViewModel> items = new List<FilesystemOperationItemViewModel>();

            List<FilesystemItemsOperationItemModel> nonConflictingItems = IncomingItems.Except(ConflictingItems).ToList();

            foreach (var item in ConflictingItems)
            {
                var iconData = await FileThumbnailHelper.LoadIconWithoutOverlayAsync(item.SourcePath, 64u);

                items.Add(new FilesystemOperationItemViewModel(updatePrimaryButtonEnabled, optionGenerateNewName, optionReplaceExisting, optionSkip)
                {
                    IsConflict = true,
                    ItemIcon = iconData != null ? await iconData.ToBitmapAsync() : null,
                    SourcePath = item.SourcePath,
                    DestinationPath = item.DestinationPath,
                    ConflictResolveOption = FileNameConflictResolveOptionType.GenerateNewName,
                    ItemOperation = item.OperationType,
                    ActionTaken = false
                });
            }

            foreach (var item in nonConflictingItems)
            {
                var iconData = await FileThumbnailHelper.LoadIconWithoutOverlayAsync(item.SourcePath, 64u);

                items.Add(new FilesystemOperationItemViewModel(updatePrimaryButtonEnabled, optionGenerateNewName, optionReplaceExisting, optionSkip)
                {
                    IsConflict = false,
                    ItemIcon = iconData != null ? await iconData.ToBitmapAsync() : null,
                    SourcePath = item.SourcePath,
                    DestinationPath = item.DestinationPath,
                    ConflictResolveOption = FileNameConflictResolveOptionType.NotAConflict,
                    ItemOperation = item.OperationType,
                    ActionTaken = true
                });
            }

            return items;
        }

        private string GetOperationIconGlyph(FilesystemOperationType operationType)
        {
            switch (operationType)
            {
                case FilesystemOperationType.Copy:
                    return "\uE8C8";

                case FilesystemOperationType.Move:
                    return "\uE8C6";

                case FilesystemOperationType.Delete:
                    return "\uE74D";

                default:
                    return "\uE8FB";
            }
        }
    }
}