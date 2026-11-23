using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Input;

namespace SourceFork.Converters
{
    public static class StringConverters
    {
        public class ToLocaleConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Models.Locale.Supported.Find(x => x.Key == value as string);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return (value as Models.Locale)?.Key;
            }
        }

        public static readonly ToLocaleConverter ToLocale = new ToLocaleConverter();

        public class ThemeToIndexConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return (value as string) switch
                {
                    "Light" => 1,
                    "Dark" => 2,
                    _ => 0,
                };
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var index = value is int i ? i : 0;
                return index switch
                {
                    1 => "Light",
                    2 => "Dark",
                    _ => "System",
                };
            }
        }

        public static readonly ThemeToIndexConverter ThemeToIndex = new ThemeToIndexConverter();

        public class FormatByResourceKeyConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var key = parameter as string;
                return App.Text(key, value);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        public static readonly FormatByResourceKeyConverter FormatByResourceKey = new FormatByResourceKeyConverter();

        public static readonly FuncValueConverter<string, string> ToShortSHA =
            new FuncValueConverter<string, string>(v => v == null ? string.Empty : (v.Length > 10 ? v.Substring(0, 10) : v));

        public static readonly FuncValueConverter<string, string> TrimRefsPrefix =
            new FuncValueConverter<string, string>(v =>
            {
                if (v == null)
                    return string.Empty;
                if (v.StartsWith("refs/heads/", StringComparison.Ordinal))
                    return v.Substring(11);
                if (v.StartsWith("refs/remotes/", StringComparison.Ordinal))
                    return v.Substring(13);
                return v;
            });

        public static readonly FuncValueConverter<string, bool> ContainsSpaces =
            new FuncValueConverter<string, bool>(v => v != null && v.Contains(' '));

        public static readonly FuncValueConverter<string, bool> IsNotNullOrWhitespace =
            new FuncValueConverter<string, bool>(v => v != null && v.Trim().Length > 0);

        public static readonly FuncValueConverter<string, string> ToFriendlyUpstream =
            new FuncValueConverter<string, string>(v => v is { Length: > 13 } ? v.Substring(13) : string.Empty);

        public static readonly FuncValueConverter<KeyGesture, string> FromKeyGesture =
            new FuncValueConverter<KeyGesture, string>(v => v?.ToString("p", null) ?? string.Empty);

        public class ToWindowTitleConverter : IMultiValueConverter
        {
            public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
            {
                if (values is not { Count: 1 })
                    return "SourceFork";

                var page = values[0] as string;
                return string.IsNullOrEmpty(page) ? "SourceFork" : $"{page} - SourceFork";
            }

            public IList<object> ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        public static readonly ToWindowTitleConverter ToWindowTitle = new ToWindowTitleConverter();
    }
}
