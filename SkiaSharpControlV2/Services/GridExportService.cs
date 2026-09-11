
using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Model;
using System.Collections;
using System.Text;

namespace SkiaSharpControlV2.Services
{
    /// <summary>
    /// Exports grid data to tab-separated values (TSV) format.
    /// Extracted from SkiaRenderer.ExportData() — pure read-only consumer with zero reverse dependencies.
    /// </summary>
    internal class GridExportService
    {
        /// <summary>
        /// Export grid data as TSV string.
        /// </summary>
        /// <param name="exportType">All or Selected rows</param>
        /// <param name="columns">Visible columns (will be filtered and ordered internally)</param>
        /// <param name="items">Flat row items (RowModel list) — used when not grouped</param>
        /// <param name="groupItemSource">Grouped row items (GroupModel list) — used when grouped</param>
        /// <param name="group">Group definition (for target column, header fields, toggle symbol)</param>
        /// <param name="selectedItems">Currently selected items</param>
        /// <param name="reflectionHelper">Reflection helper for property reading</param>
        /// <returns>TSV-formatted string</returns>
        public string Export(
            SKExportType exportType,
            IEnumerable<SKGridViewColumn>? columns,
            List<RowModel>? items,
            List<GroupModel>? groupItemSource,
            SKGroupDefinition? group,
            IEnumerable? selectedItems,
            ReflectionHelper reflectionHelper)
        {
            using var _metrics = GridMetrics.Measure(GridMetrics.ExportData);
            GridLogger.Log(GridLogger.Export, $"Export called, type={exportType}");

            if (columns == null || (items == null && groupItemSource == null))
                return "";

            var visibleColumns = columns
                .Where(c => c.IsVisible)
                .OrderBy(c => c.DisplayIndex)
                .ToList();

            if (visibleColumns.Count == 0)
                return "";

            StringBuilder sb = new();

            // Header Row
            sb.AppendLine(string.Join("\t", visibleColumns.Select(c => c.Header)));

            // Decide source: grouped or flat
            IEnumerable<object> exportSource;
            if (groupItemSource != null && groupItemSource.Count > 0)
            {
                if (exportType == SKExportType.Selected)
                {
                    var selectedSet = new HashSet<object>(selectedItems!.Cast<object>());
                    // Match by Item (data rows) OR by GroupModel itself (group headers where Item is null)
                    exportSource = groupItemSource.Where(x => selectedSet.Contains(x.Item ?? (object)x));
                }
                else
                    exportSource = groupItemSource;
            }
            else
            {
                exportSource = exportType == SKExportType.Selected
                     ? selectedItems!.Cast<object>()
                     : items!.Select(x => x.Item).ToList();
            }

            foreach (var row in exportSource)
            {
                // If this is a grouped row wrapper (GroupModel)
                if (row is GroupModel gi)
                {
                    List<string> rowData = new();

                    // 1) Header SubTotal (IsHeaderSubTotal)
                    if (gi.IsHeaderSubTotal)
                    {
                        foreach (var col in visibleColumns)
                        {
                            if (group?.Target != null && group?.Target == col.Name)
                            {
                                rowData.Add("Total " + (gi.GroupName ?? ""));
                            }
                            else if (group?.HeaderFields?.Any(x => x.TargetColumns != null && x.TargetColumns == col.Name) == true)
                            {
                                var header = group.HeaderFields.First(x => x.TargetColumns == col.Name);

                                var valuesForTotal = groupItemSource!
                                    .Where(x => x.IsGroupHeader == false && x.IsGroupSubTotal == false && x.IsHeaderSubTotal == false)
                                    .Where(x => reflectionHelper.ReadCurrentItemWithTypes(x.Item, gi.BindingPath).Value == gi.GroupName);

                                object? agg = CalculateGroupAggregation(valuesForTotal, reflectionHelper, header.BindingPath, header.Aggregation);
                                var formatted = Helper.ApplyFormat(typeof(double), agg?.ToString(), col.Format!, col.ShowBracketOnNegative, col.FormatWithAcronym);
                                rowData.Add(formatted);
                            }
                            else
                                rowData.Add("");
                        }
                    }
                    // 2) Group Header (IsGroupHeader)
                    else if (gi.IsGroupHeader)
                    {
                        foreach (var col in visibleColumns)
                        {
                            if (group?.Target != null && group?.Target == col.Name)
                            {
                                rowData.Add(gi.GroupName ?? "");
                            }
                            else if (group?.ToggleSymbol?.TargetColumns == col.Name)
                            {
                                rowData.Add("");
                            }
                            else if (group?.HeaderFields != null && group?.HeaderFields.Count > 0 && group.HeaderFields.Any(x => x.TargetColumns != null && x.TargetColumns == col.Name))
                            {
                                var groupHeader = group.HeaderFields.First(x => x.TargetColumns == col.Name);
                                var valuesForTotal = groupItemSource!
                                    .Where(x => x.GroupName == gi.GroupName && x.IsGroupHeader == false && x.IsGroupSubTotal == false && x.IsHeaderSubTotal == false);

                                object? agg = CalculateGroupAggregation(valuesForTotal, reflectionHelper, groupHeader.BindingPath, groupHeader.Aggregation);
                                var val = Helper.ApplyFormat(typeof(double), agg?.ToString(), col.Format!, col.ShowBracketOnNegative, col.FormatWithAcronym);
                                rowData.Add(val);
                            }
                            else
                                rowData.Add("");
                        }
                    }
                    // 3) Group SubTotal (IsGroupSubTotal)
                    else if (gi.IsGroupSubTotal)
                    {
                        foreach (var col in visibleColumns)
                        {
                            if (group?.Target != null && group?.Target == col.Name)
                            {
                                rowData.Add("Total " + (gi.SubTotalGroupName ?? ""));
                            }
                            else if (group?.HeaderFields?.Any(x => x.TargetColumns != null && x.TargetColumns == col.Name) == true)
                            {
                                var header = group.HeaderFields.First(x => x.TargetColumns == col.Name);

                                var valuesForTotal = groupItemSource!
                                    .Where(x => x.IsGroupHeader == false && x.IsGroupSubTotal == false && x.IsHeaderSubTotal == false)
                                    .Where(x =>
                                        reflectionHelper.ReadCurrentItemWithTypes(x.Item, gi.BindingPath).Value == gi.SubTotalGroupName &&
                                        (group?.GroupBy != null ? reflectionHelper.ReadCurrentItemWithTypes(x.Item, group.GroupBy).Value == gi.GroupName : true)
                                    );

                                object? agg = CalculateGroupAggregation(valuesForTotal, reflectionHelper, header.BindingPath, header.Aggregation);
                                var formatted = Helper.ApplyFormat(typeof(double), agg?.ToString(), col.Format!, col.ShowBracketOnNegative, col.FormatWithAcronym);
                                rowData.Add(formatted);
                            }
                            else
                                rowData.Add("");
                        }
                    }
                    // 4) Normal item row (actual data item)
                    else
                    {
                        foreach (var col in visibleColumns)
                        {
                            var val = reflectionHelper.ReadCurrentItemWithTypes(gi.Item, col.BindingPath);
                            var formatted = Helper.ApplyFormat(val.Type!, val.Value, col.Format!, col.ShowBracketOnNegative, col.FormatWithAcronym);
                            rowData.Add(col.DataVisible ? formatted : "");
                        }
                    }

                    sb.AppendLine(string.Join("\t", rowData));
                }
                else
                {
                    // Flat (non-grouped) objects
                    List<string> rowData = new();
                    foreach (var col in visibleColumns)
                    {
                        var val = reflectionHelper.ReadCurrentItemWithTypes(row, col.BindingPath);
                        var formatted = Helper.ApplyFormat(val.Type!, val.Value, col.Format!, col.ShowBracketOnNegative, col.FormatWithAcronym);
                        rowData.Add(col.DataVisible ? formatted : ""); // B33 fix: respect DataVisible for flat rows
                    }
                    sb.AppendLine(string.Join("\t", rowData));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Calculate aggregation over group items for a specific binding path.
        /// Duplicated from SkiaRenderer — will be consolidated in Step 4 (SetterResolver extraction).
        /// </summary>
        internal static object? CalculateGroupAggregation(
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
