using Files.Enums;
using System;
using System.Threading.Tasks;

namespace Files.Filesystem.FilesystemHistory
{
    public interface IStorageHistoryOperations : IDisposable
    {
        Task<ReturnResult> Undo(IStorageHistory history);

        Task<ReturnResult> Redo(IStorageHistory history);
    }
}