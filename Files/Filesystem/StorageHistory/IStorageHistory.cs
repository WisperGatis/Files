using Files.Enums;
using System;
using System.Collections.Generic;

namespace Files.Filesystem.FilesystemHistory
{
    public interface IStorageHistory : IDisposable
    {
        FileOperationType OperationType { get; }

        IEnumerable<IStorageItemWithPath> Source { get; }

        IEnumerable<IStorageItemWithPath> Destination { get; }

        #region Modify

        void Modify(IStorageHistory newHistory);

        void Modify(FileOperationType operationType, IEnumerable<IStorageItemWithPath> source, IEnumerable<IStorageItemWithPath> destination);

        void Modify(FileOperationType operationType, IStorageItemWithPath source, IStorageItemWithPath destination);

        #endregion Modify
    }
}