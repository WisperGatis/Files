using Files.Enums;
using Files.Filesystem.FilesystemHistory;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Files.Filesystem
{
    public interface IFilesystemOperations : IDisposable
    {
        Task<(IStorageHistory, IStorageItem)> CreateAsync(IStorageItemWithPath source, IProgress<FileSystemStatusCode> errorCode, CancellationToken cancellationToken);

        Task<IStorageHistory> CopyAsync(IStorageItem source,
                                        string destination,
                                        NameCollisionOption collision,
                                        IProgress<float> progress,
                                        IProgress<FileSystemStatusCode> errorCode,
                                        CancellationToken cancellationToken);

        Task<IStorageHistory> CopyAsync(IStorageItemWithPath source,
                                        string destination,
                                        NameCollisionOption collision,
                                        IProgress<float> progress,
                                        IProgress<FileSystemStatusCode> errorCode,
                                        CancellationToken cancellationToken);

        Task<IStorageHistory> MoveAsync(IStorageItem source,
                                        string destination,
                                        NameCollisionOption collision,
                                        IProgress<float> progress,
                                        IProgress<FileSystemStatusCode> errorCode,
                                        CancellationToken cancellationToken);

        Task<IStorageHistory> MoveAsync(IStorageItemWithPath source,
                                        string destination,
                                        NameCollisionOption collision,
                                        IProgress<float> progress,
                                        IProgress<FileSystemStatusCode> errorCode,
                                        CancellationToken cancellationToken);

        Task<IStorageHistory> DeleteAsync(IStorageItem source,
                                          IProgress<float> progress,
                                          IProgress<FileSystemStatusCode> errorCode,
                                          bool permanently,
                                          CancellationToken cancellationToken);

        Task<IStorageHistory> DeleteAsync(IStorageItemWithPath source,
                                          IProgress<float> progress,
                                          IProgress<FileSystemStatusCode> errorCode,
                                          bool permanently,
                                          CancellationToken cancellationToken);

        Task<IStorageHistory> RenameAsync(IStorageItem source,
                                          string newName,
                                          NameCollisionOption collision,
                                          IProgress<FileSystemStatusCode> errorCode,
                                          CancellationToken cancellationToken);

        Task<IStorageHistory> RenameAsync(IStorageItemWithPath source,
                                          string newName,
                                          NameCollisionOption collision,
                                          IProgress<FileSystemStatusCode> errorCode,
                                          CancellationToken cancellationToken);

        Task<IStorageHistory> RestoreFromTrashAsync(IStorageItemWithPath source,
                                                    string destination,
                                                    IProgress<float> progress,
                                                    IProgress<FileSystemStatusCode> errorCode,
                                                    CancellationToken cancellationToken);
    }
}