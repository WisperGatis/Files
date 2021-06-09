using System;

namespace Files.Filesystem
{
    public enum FilesystemItemType : byte
    {
        Directory = 0,

        File = 1,

        [Obsolete("The symlink has no use for now here.")]
        Symlink = 2,

        Library = 3,
    }
}