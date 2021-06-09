using System;

namespace Files.Enums
{
    [Flags]
    public enum ReturnResult : byte
    {
        InProgress = 0,

        Success = 1,

        Failed = 2,

        IntegrityCheckFailed = 3,

        UnknownException = 4,

        BadArgumentException = 5,

        NullException = 6,

        AccessUnauthorized = 7,

        Cancelled = 8,
    }
}