using JetBrains.Annotations;
using Microsoft.Toolkit.Uwp;
using System;
using System.Collections.Generic;
using System.IO;

namespace Files.Extensions
{
    public static class StringExtensions
    {
        public static bool IsSubPathOf(this string path, string baseDirPath)
        {
            string normalizedPath = Path.GetFullPath(path.Replace('/', '\\')
                .WithEnding("\\"));

            string normalizedBaseDirPath = Path.GetFullPath(baseDirPath.Replace('/', '\\')
                .WithEnding("\\"));

            return normalizedPath.StartsWith(normalizedBaseDirPath, StringComparison.OrdinalIgnoreCase);
        }

        public static string WithEnding([CanBeNull] this string str, string ending)
        {
            if (str == null)
            {
                return ending;
            }

            string result = str;

            for (int i = 0; i <= ending.Length; i++)
            {
                string tmp = result + ending.Right(i);
                if (tmp.EndsWith(ending))
                {
                    return tmp;
                }
            }

            return result;
        }

        public static string Right([NotNull] this string value, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", length, "Length is less than zero");
            }

            return (length < value.Length) ? value.Substring(value.Length - length) : value;
        }

        private static readonly Dictionary<string, string> abbreviations = new Dictionary<string, string>()
        {
            { "KiB", "KiloByteSymbol".GetLocalized() },
            { "MiB", "MegaByteSymbol".GetLocalized() },
            { "GiB", "GigaByteSymbol".GetLocalized() },
            { "TiB", "TeraByteSymbol".GetLocalized() },
            { "PiB", "PetaByteSymbol".GetLocalized() },
            { "B", "ByteSymbol".GetLocalized() },
            { "b", "ByteSymbol".GetLocalized() }
        };

        public static string ConvertSizeAbbreviation(this string value)
        {
            foreach (var item in abbreviations)
            {
                value = value.Replace(item.Key, item.Value);
            }
            return value;
        }
    }
}