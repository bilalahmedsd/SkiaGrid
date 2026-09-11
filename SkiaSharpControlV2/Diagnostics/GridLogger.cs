using System.Diagnostics;

namespace SkiaSharpControlV2.Diagnostics
{
    /// <summary>
    /// Lightweight debug logger for SkiaGridViewV2.
    /// All methods use [Conditional("DEBUG")] — calls are entirely stripped in Release builds (zero overhead).
    /// Output goes to System.Diagnostics.Debug.WriteLine (visible in VS Output window).
    /// </summary>
    internal static class GridLogger
    {
        // Standard categories for consistent log filtering
        public const string Render = "Render";
        public const string Data = "Data";
        public const string Sort = "Sort";
        public const string Filter = "Filter";
        public const string Group = "Group";
        public const string Selection = "Selection";
        public const string Scroll = "Scroll";
        public const string Column = "Column";
        public const string Lifecycle = "Lifecycle";
        public const string Export = "Export";
        public const string Input = "Input";

        [Conditional("DEBUG")]
        public static void Log(string category, string message)
        {
            Debug.WriteLine($"[SkiaGrid][{category}] {message}");
        }

        [Conditional("DEBUG")]
        public static void Log(string category, string message, params object[] args)
        {
            Debug.WriteLine($"[SkiaGrid][{category}] {string.Format(message, args)}");
        }

        [Conditional("DEBUG")]
        public static void LogWarning(string category, string message)
        {
            Debug.WriteLine($"[SkiaGrid][{category}][WARN] {message}");
        }

        [Conditional("DEBUG")]
        public static void LogError(string category, string message, Exception? ex = null)
        {
            var exInfo = ex != null ? $" | Exception: {ex.GetType().Name}: {ex.Message}" : "";
            Debug.WriteLine($"[SkiaGrid][{category}][ERROR] {message}{exInfo}");
        }
    }
}
