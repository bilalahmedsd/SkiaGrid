using SkiaSharp;

using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Model;

namespace SkiaSharpControlV2.Renderer
{
    /// <summary>
    /// Resolves SKSetter values to SKPaint objects and calculates group aggregations.
    /// Extracted from SkiaRenderer (Step 4 refactoring).
    /// Performance-critical: called per-cell on every paint frame.
    /// </summary>
    internal class SetterResolver
    {
        // ── Setter Resolution ───────────────────────────────────────────

        /// <summary>
        /// Resolve a collection of SKSetters to paint values.
        /// Supports both literal Value and dynamic ValuePath (reads from item property).
        /// </summary>
        public static (SKPaint BackgroundColor, SKPaint Foregroundcolor, SKPaint BorderColor) GetSetterValues(
            ReflectionHelper reflection, IEnumerable<SKSetter>? setters, object Item)
        {
            SKPaint backgroundColor = null;
            SKPaint foregroundcolor = null;
            SKPaint borderColor = null;
            if (setters != null)
            {
                foreach (var item1 in setters)
                {
                    var value = "";
                    if (string.IsNullOrEmpty(item1.ValuePath) || Item == null)
                        value = item1.Value?.ToString() ?? "";
                    else
                    {
                        var (strVal, _) = reflection.ReadCurrentItemWithTypes(Item, item1.ValuePath);
                        value = strVal ?? "";
                    }

                    // Use cached SKPaint instead of creating new per cell per frame (memory leak fix + B10)
                    if (!string.IsNullOrEmpty(value))
                    {
                        var paint = SKPaintCache.Get(value);
                        switch (item1.Property)
                        {
                            case SkStyleProperty.Background: backgroundColor = paint; break;
                            case SkStyleProperty.Foreground: foregroundcolor = paint; break;
                            case SkStyleProperty.BorderColor: borderColor = paint; break;
                        }
                    }
                }
            }
            return (backgroundColor, foregroundcolor, borderColor);
        }

        // ── Group Aggregation ───────────────────────────────────────────

        /// <summary>
        /// Calculate aggregation over group items for a specific binding path.
        /// Used by both renderer (group header display) and export (group header/subtotal values).
        /// </summary>
        public object? CalculateGroupAggregation(
            IEnumerable<GroupModel> groupItems,
            ReflectionHelper reflectionHelper,
            string bindingPath,
            SkAggregation aggregation)
        {
            var rawValues = groupItems
                .Select(x => x.Item)
                .Select(item =>
                {
                    var (strVal, _) = reflectionHelper.ReadCurrentItemWithTypes(item, bindingPath);
                    return strVal;
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (!rawValues.Any())
                return "";

            switch (aggregation)
            {
                case SkAggregation.Sum:
                case SkAggregation.Count:
                case SkAggregation.Avg:
                case SkAggregation.Min:
                case SkAggregation.Max:
                    var numbers = rawValues
                        .Select(v => double.TryParse(v, out var num) ? (double?)num : null)
                        .Where(x => x.HasValue)
                        .Select(x => x.Value)
                        .ToList();

                    if (!numbers.Any())
                        return null;

                    return aggregation switch
                    {
                        SkAggregation.Sum => numbers.Sum(),
                        SkAggregation.Count => numbers.Count,
                        SkAggregation.Avg => numbers.Average(),
                        SkAggregation.Min => numbers.Min(),
                        SkAggregation.Max => numbers.Max(),
                        _ => null
                    };

                case SkAggregation.Distinct:
                    return string.Join('/', (rawValues != null && rawValues.Count > 0) ? rawValues
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x).ToArray() : []);

                default:
                    return null;
            }
        }
    }
}
