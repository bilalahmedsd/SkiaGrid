
using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Model;

namespace SkiaSharpControlV2.Managers
{
    /// <summary>
    /// Flattens grouped/hierarchical data into flat lists for rendering.
    /// Extracted from SkiaGridViewV2 FlattenGroupedItems/FlattenRowItems (Step 12 refactoring).
    /// </summary>
    internal class DataFlatteningService
    {
        private readonly ReflectionHelper _reflectionHelper;

        public DataFlatteningService(ReflectionHelper reflectionHelper)
        {
            _reflectionHelper = reflectionHelper;
        }

        /// <summary>
        /// Flatten grouped data into a list of GroupModel wrappers.
        /// Includes group headers, subtotals, and data rows with expansion state.
        /// </summary>
        public List<GroupModel> FlattenGroupedItems(
            ICustomCollectionView collectionView,
            Dictionary<string, (bool IsExpended, float x, float y, float height, float width)> groupToggleDetails,
            SKGroupDefinition? groupSettings,
            SkGridColumnCollection? columns)
        {
            using var _metrics = GridMetrics.Measure(GridMetrics.DataFlattenGrouped);
            var result = new List<GroupModel>();

            if (collectionView == null || collectionView.GroupList == null)
                return result;

            var sortColumn = columns?.FirstOrDefault(x => x.GridViewColumnSort != SkGridViewColumnSort.None);
            bool isDistinctColumn = groupSettings?.HeaderFields?.Any(x => x.Aggregation == SkAggregation.Distinct) ?? false;
            bool emitDistinctSubtotals = sortColumn != null && sortColumn.ShowSubTotalOnSort && isDistinctColumn;

            // Grand-total (header) subtotal rows — emitted ONCE at the very top, aggregated across
            // ALL groups' data (RenderHeaderSubTotal / GridExportService do the grand aggregation).
            // BUGFIX: this block was previously inside the per-group loop below, so it was re-emitted
            // at the top of every group; for the 2nd+ group the extra rows landed between the prior
            // group's data and the next header, rendering as a duplicated subtotal at the bottom of
            // one group / top of the next. Now it runs exactly once over the union of all items.
            if (emitDistinctSubtotals)
            {
                var grandDistinctValues = collectionView.GroupList
                    .SelectMany(g => g.Items)
                    .Select(item =>
                    {
                        var (val, _) = _reflectionHelper.ReadCurrentItemWithTypes(item, sortColumn!.BindingPath);
                        return val;
                    }).Where(v => v != null).Distinct().OrderBy(v => v).ToList();

                foreach (var dv in grandDistinctValues)
                {
                    result.Add(new GroupModel { IsHeaderSubTotal = true, GroupName = dv, BindingPath = sortColumn!.BindingPath, IsExpanded = true });
                }
            }

            foreach (var group in collectionView.GroupList)
            {
                string groupName = group.Key?.ToString() ?? "";

                // Group header
                bool isExpanded = true;
                if (groupToggleDetails.ContainsKey(groupName))
                {
                    isExpanded = groupToggleDetails[groupName].IsExpended;
                }
                else
                {
                    groupToggleDetails[groupName] = (true, 0, 0, 0, 0);
                }

                result.Add(new GroupModel { IsGroupHeader = true, GroupName = groupName, IsExpanded = true });

                // Per-group distinct subtotals — one row per distinct value WITHIN this group,
                // immediately after the group header (unchanged behavior).
                if (emitDistinctSubtotals)
                {
                    var distinctValues = group.Items.Select(item =>
                    {
                        var (val, _) = _reflectionHelper.ReadCurrentItemWithTypes(item, sortColumn!.BindingPath);
                        return val;
                    }).Where(v => v != null).Distinct().OrderBy(v => v).ToList();

                    foreach (var dv in distinctValues)
                    {
                        // Opt-in ShowSubtotalsWhenCollapsed keeps the per-group subtotal visible even
                        // when the group is collapsed (IsExpanded=true routes it through the single
                        // "IsGroupHeader || IsExpanded" visibility+index predicate as visible). Default
                        // false → IsExpanded = isExpanded, i.e. behavior byte-for-byte unchanged.
                        result.Add(new GroupModel { IsGroupSubTotal = true, GroupName = groupName, SubTotalGroupName = dv, BindingPath = sortColumn!.BindingPath, IsExpanded = (groupSettings?.ShowSubtotalsWhenCollapsed == true) ? true : isExpanded });
                    }
                }

                // Data rows
                foreach (var item in group.Items)
                {
                    result.Add(new GroupModel { Item = item, GroupName = groupName, IsExpanded = isExpanded });
                }
            }

            return result;
        }

        // ── RowModel reuse pool (M2 perf fix) ────────────────────────────────────
        //
        // FlattenRowItems runs on EVERY source CollectionChanged — including the app-side
        // drain's 4×/sec ReplaceAll Reset at flood rates. Allocating N fresh RowModels per
        // rebuild produced an allocation storm at 70k+ rows (measured: 247 MB/s mean
        // allocation rate, GC pauses ~343 ms per wall-clock second — the "unusable at
        // 85k rows" symptom). The pool reuses the previous flatten's RowModel for the
        // same source item, so a steady-state rebuild is O(N) pointer work with ~zero
        // allocations. Two persistent dictionaries are swapped + cleared per flatten so
        // the pool itself allocates nothing at steady state.
        //
        // Behavior parity: a reused RowModel carries identical (Item, IsChildRow) and its
        // HasChild is re-evaluated on every flatten. Nothing in the codebase keys on
        // RowModel identity (selection / toggles / hit-tests all key on the data item),
        // so reuse is observationally identical to fresh allocation. Reuse only happens
        // when IsChildRow matches; an item appearing with a different role allocates fresh.
        // Keys use reference equality — "same source object" — immune to Equals overrides.
        private Dictionary<object, RowModel> _rowModelPool = new(ReferenceEqualityComparer.Instance);
        private Dictionary<object, RowModel> _rowModelPoolNext = new(ReferenceEqualityComparer.Instance);

        private RowModel GetOrCreateRowModel(object item, bool isChildRow, bool hasChild)
        {
            if (_rowModelPool.TryGetValue(item, out var pooled) && pooled.IsChildRow == isChildRow)
            {
                pooled.HasChild = hasChild;
                _rowModelPoolNext[item] = pooled;
                return pooled;
            }
            var fresh = new RowModel { Item = item, IsChildRow = isChildRow, HasChild = hasChild };
            _rowModelPoolNext[item] = fresh;
            return fresh;
        }

        /// <summary>
        /// Flatten hierarchical data into a list of RowModel wrappers.
        /// Uses ChildProperty (reflection) to find children — no ITreeItem dependency.
        /// RowModel instances are reused across flattens via the pool above.
        /// </summary>
        public List<RowModel> FlattenRowItems(
            ICustomCollectionView collectionView,
            Dictionary<object, (float x, float y, float height, float width)> rowToggleDetails,
            string? childProperty = null,
            Dictionary<object, bool>? expandedItems = null)
        {
            using var _metrics = GridMetrics.Measure(GridMetrics.DataFlattenRows);

            if (collectionView == null || collectionView.ViewList == null)
                return new List<RowModel>();

            // Pre-size: avoids repeated List growth re-allocations at large N.
            var result = new List<RowModel>(collectionView.ViewList.Count);

            foreach (var row in collectionView.ViewList)
            {
                // Check for children via ChildProperty reflection (not ITreeItem)
                System.Collections.IEnumerable? children = null;
                if (!string.IsNullOrEmpty(childProperty))
                {
                    try
                    {
                        // Use GetPropValue directly to get the raw collection object.
                        // ReadCurrentItemWithTypes returns a string representation, not the actual collection.
                        var rawChildren = _reflectionHelper.GetPropValue(row, childProperty);
                        if (rawChildren is System.Collections.IEnumerable enumerable)
                            children = enumerable;
                    }
                    catch { /* Property not found on this item type — skip */ }
                }

                bool hasChildren = children != null && children.Cast<object>().Any();

                if (hasChildren)
                {
                    result.Add(GetOrCreateRowModel(row, isChildRow: false, hasChild: true));

                    bool isExpanded = expandedItems?.GetValueOrDefault(row, false) ?? false;
                    if (isExpanded)
                    {
                        foreach (var child in children!)
                        {
                            result.Add(GetOrCreateRowModel(child, isChildRow: true, hasChild: false));
                        }
                    }
                    if (!rowToggleDetails.ContainsKey(row))
                        rowToggleDetails.Add(row, (0, 0, 0, 0));
                }
                else
                {
                    result.Add(GetOrCreateRowModel(row, isChildRow: false, hasChild: false));
                }
            }

            // Swap: this flatten's models become the reuse pool for the next one.
            // Clear() keeps capacity — zero allocations at steady state.
            (_rowModelPool, _rowModelPoolNext) = (_rowModelPoolNext, _rowModelPool);
            _rowModelPoolNext.Clear();

            return result;
        }

        /// <summary>
        /// Child rows of <paramref name="parent"/> via ChildProperty reflection — the same lookup
        /// FlattenRowItems uses, reused so expand/collapse notifications can report exactly the rows
        /// that became visible/hidden. Empty list when there is no ChildProperty or no children.
        /// Called on user toggles / API calls only — never per frame.
        /// </summary>
        public IReadOnlyList<object> GetChildItems(object? parent, string? childProperty)
        {
            if (parent == null || string.IsNullOrEmpty(childProperty)) return Array.Empty<object>();
            try
            {
                if (_reflectionHelper.GetPropValue(parent, childProperty) is not System.Collections.IEnumerable children)
                    return Array.Empty<object>();

                var list = new List<object>();
                foreach (var child in children)
                    if (child != null) list.Add(child);
                return list;
            }
            catch { return Array.Empty<object>(); } // Property not found on this item type
        }
    }
}
