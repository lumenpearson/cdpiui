using System;
using System.Collections.Generic;
using System.Text;

namespace CDPIUI.Shared.Extensions
{
    public static class EnumExtension
    {
        public static T ToEnum<T>(this string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }

        public static T ToEnum<T>(this string? value, T defaultValue) where T : struct 
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return Enum.TryParse(value, true, out T result) ? result : defaultValue;
        }
    }
}
