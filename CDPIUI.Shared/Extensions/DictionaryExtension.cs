using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CDPIUI.Shared.Extensions;

public static class DictionaryExtension
{
    /// <summary>
    /// Get dictionary key by value. <br></br>
    /// If value is <see cref="string"/>,
    /// uses <see cref="string.Equals(string, string)"/>.<br></br>
    /// Otherwise uses default object comparier.
    /// </summary>
    /// <summary>
    /// The key of the first entry with this value, or false when no entry has it. Unlike
    /// <see cref="GetKeyByValue{KeyType, ValueType}"/>, a missing value does not give default(KeyType).
    /// </summary>
    public static bool TryGetKeyByValue<KeyType, ValueType>(this Dictionary<KeyType, ValueType> dictionary, ValueType value, out KeyType key)
        where KeyType : notnull
    {
        foreach (var kvp in dictionary)
        {
            if (EqualityComparer<ValueType>.Default.Equals(kvp.Value, value))
            {
                key = kvp.Key;
                return true;
            }
        }
        key = default!;
        return false;
    }

    public static KeyType GetKeyByValue<KeyType, ValueType>(this Dictionary<KeyType, ValueType> dictionary, ValueType value) where ValueType : class
    {
        if (value is string stval)
        {
            return dictionary.FirstOrDefault(kvp => string.Equals(kvp.Value as string, value as string, StringComparison.Ordinal)).Key;
        }

        return dictionary.FirstOrDefault(kvp => kvp.Value == value).Key;
    }

    public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> source)
    {
        foreach (var element in source)
            if (!target.Contains(element))
                target.Add(element);
    }
}
