using System;
using System.Collections.Generic;
using System.Linq;

namespace Files.Extensions
{
    internal static class LinqExtensions
    {
        internal static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            foreach (T value in collection)
                action(value);
        }

        internal static void AddSorted<T>(this IList<T> list, T item) where T : IComparable<T>
        {
            if (list.Count == 0)
            {
                list.Add(item);
                return;
            }
            if (list[list.Count - 1].CompareTo(item) <= 0)
            {
                list.Add(item);
                return;
            }
            if (list[0].CompareTo(item) >= 0)
            {
                list.Insert(0, item);
                return;
            }
            int index = list.ToList().BinarySearch(item);
            if (index < 0)
            {
                index = ~index;
            }
            list.Insert(index, item);
        }
    }
}