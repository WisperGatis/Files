using System;

namespace Files.Enums
{
    [Flags]
    public enum FileOperationType : byte
    {
        CreateNew = 0,

        Rename = 1,

        Copy = 3,

        Move = 4,

        Extract = 5,

        Recycle = 6,

        Restore = 7,

        Delete = 8
    }
}