
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Text;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SkiaSharpControlV2.Helpers
{
    internal static class Helper
    {
        public static SolidColorBrush GetColorBrush(string Color)
        {
            string bgcolorString = Color;
            var bgcolor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bgcolorString);
            return new SolidColorBrush(bgcolor);
        }
        public static float GetSystemDpi()
        {
            using Graphics g = Graphics.FromHwnd(IntPtr.Zero);
            return g.DpiX / 96.0f; // 96 DPI is the default (100% scaling)
        }
        public static ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent is ScrollViewer)
                return (ScrollViewer)parent;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }
        // B9: Duplicate of ReflectionHelper.ReadCurrentItemWithTypes (which is more efficient, uses compiled expressions).
        // Kept for backward compatibility with tests. Use ReflectionHelper version in new code.
        [System.Obsolete("Use ReflectionHelper.ReadCurrentItemWithTypes instead")]
        public static (string? Value, Type Type) ReadCurrentItemWithTypes(object currentItem, string propertyName)
        {
            if (currentItem == null || string.IsNullOrWhiteSpace(propertyName))
                return (null, typeof(void));

            var type = currentItem.GetType();
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop == null)
                return (null, typeof(void)); // Property not found

            object? val = prop.GetValue(currentItem);
            return (val?.ToString(), prop.PropertyType);
        }
        public static string ApplyFormat(Type type, string? value, string format, bool showBracketIfNegative = false, bool showAsAcronym = false)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            Type baseType = Nullable.GetUnderlyingType(type) ?? type;

            try
            {
                switch (Type.GetTypeCode(baseType))
                {
                    case TypeCode.Double:
                        return double.TryParse(value, out var dVal)
                            ? FormatWithAcronym(dVal, format, showBracketIfNegative, showAsAcronym)
                            : $"{value}";

                    case TypeCode.Decimal:
                        return decimal.TryParse(value, out var decVal)
                            ? FormatWithAcronym(decVal, format, showBracketIfNegative, showAsAcronym)
                            : $"{value}";

                    case TypeCode.Single:
                        return float.TryParse(value, out var fVal)
                            ? FormatWithAcronym(fVal, format, showBracketIfNegative, showAsAcronym)
                            : $"{value}";

                    case TypeCode.Int32:
                        return int.TryParse(value, out var iVal)
                            ? FormatWithAcronym(iVal, format, showBracketIfNegative, showAsAcronym)
                            : $"{value}";

                    case TypeCode.Int64:
                        return long.TryParse(value, out var lVal)
                            ? FormatWithAcronym(lVal, format, showBracketIfNegative, showAsAcronym)
                            : $"{value}";

                    case TypeCode.DateTime:
                        return DateTime.TryParse(value, out var dtVal)
                            ? dtVal.ToString(format)
                            : $"{value}";

                    case TypeCode.String:
                    case TypeCode.Char:
                        return value;
                }

                if (baseType == typeof(TimeSpan))
                {
                    return TimeSpan.TryParse(value, out var tsVal)
                        ? tsVal.ToString(format)
                        : $"{value}";
                }

                return value;
            }
            catch
            {
                return $"{value}";
            }
        }
        private static string FormatWithAcronym<T>(T number, string format, bool showBracket, bool showAsAcronym) where T : struct, IComparable
        {
            decimal val = Convert.ToDecimal(number);

            if (showAsAcronym)
            {
                string suffix;
                decimal shortVal;

                if (Math.Abs(val) >= 1_000_000_000)
                {
                    shortVal = val / 1_000_000_000;
                    suffix = "B";
                }
                else if (Math.Abs(val) >= 1_000_000)
                {
                    shortVal = val / 1_000_000;
                    suffix = "M";
                }
                else if (Math.Abs(val) >= 1_000)
                {
                    shortVal = val / 1_000;
                    suffix = "K";
                }
                else
                {
                    return FormatWithBracket(val, format, showBracket);
                }

                string formatted = string.IsNullOrWhiteSpace(format) ? shortVal.ToString("0.#") : shortVal.ToString(format);
                string result = $"{formatted}{suffix}";

                return showBracket && val < 0 ? $"({result.TrimStart('-')})" : result;
            }

            return FormatWithBracket(val, format, showBracket);
        }

        private static string FormatWithBracket<T>(T number, string format, bool showBracket) where T : struct, IComparable
        {
            decimal val = Convert.ToDecimal(number);
            string formatted = string.IsNullOrWhiteSpace(format) ? val.ToString() : val.ToString(format);

            return showBracket && val < 0 ? $"({formatted.TrimStart('-')})" : formatted;
        }
        public static bool IsFontInstalled(string fontName)
        {
            using (InstalledFontCollection fontsCollection = new InstalledFontCollection())
            {
                return fontsCollection.Families.Any(f => string.Equals(f.Name, fontName, StringComparison.OrdinalIgnoreCase));
            }
        }

    }

}
