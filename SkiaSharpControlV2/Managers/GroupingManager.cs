
using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Model;
using SkiaSharpControlV2.Renderer;

namespace SkiaSharpControlV2.Managers
{
    /// <summary>
    /// Manages group toggle state, expand/collapse operations, and data flattening coordination.
    /// Owns GroupToggleDetails and RowToggleDetails dictionaries.
    /// Extracted from SkiaGridViewV2 (Step 12 refactoring).
    /// </summary>
    internal class GroupingManager
    {
        public Dictionary<string, (bool IsExpended, float x, float y, float height, float width)> GroupToggleDetails { get; } = new();
        public Dictionary<object, (float x, float y, float height, float width)> RowToggleDetails { get; } = new();

        private readonly DataFlatteningService _flatteningService;
        private readonly Func<ICustomCollectionView?> _getCollectionView;
        private readonly Func<SKGroupDefinition?> _getGroupSettings;
        private readonly Func<SkGridColumnCollection?> _getColumns;
        private readonly Func<SkiaRenderer?> _getRenderer;
        private readonly Func<string?> _getChildProperty;
        private readonly Action _updateTotalRows;
        private readonly Action _refresh;

        /// <summary>Tracks expand/collapse state per data item (keyed by item reference).</summary>
        public Dictionary<object, bool> ExpandedItems { get; } = new();

        // ── Expand/collapse notification (additive; no-op unless the control has a listener) ──
        //
        // Set by SkiaGridViewV2 at construction. The payload (AffectedItems) is only materialized
        // when ExpandListenersActive() says someone is actually subscribed, so a grid with no
        // ExpandChanged handler / ExpandChangedCommand pays nothing beyond one delegate call.
        // Never invoked from a render path — only from user toggles and explicit API calls.
        public Action<SkExpandChangedEventArgs>? ExpandChangedCallback { get; set; }
        public Func<bool>? ExpandListenersActive { get; set; }

        private bool WantsNotification =>
            ExpandChangedCallback != null && (ExpandListenersActive?.Invoke() ?? false);

        private void NotifyExpandChanged(bool isExpanded, SkExpandChangeReason reason, string? groupName,
            object? parentItem, Func<IReadOnlyList<object>> affectedItems)
        {
            if (!WantsNotification) return;
            ExpandChangedCallback!(new SkExpandChangedEventArgs(
                isExpanded, reason, groupName, parentItem, affectedItems()));
        }

        /// <summary>Data rows of one group (all groups when <paramref name="groupName"/> is null). Headers/subtotals excluded.</summary>
        private IReadOnlyList<object> GroupDataItems(string? groupName)
        {
            var src = _getRenderer()?.GroupItemSource;
            if (src == null) return Array.Empty<object>();

            var list = new List<object>();
            foreach (var g in src)
                if (g.Item != null && (groupName == null || g.GroupName == groupName))
                    list.Add(g.Item);
            return list;
        }

        /// <summary>Child rows of every top-level row in the view — payload for bulk tree expand/collapse.</summary>
        private IReadOnlyList<object> AllChildItems()
        {
            var cv = _getCollectionView();
            if (cv?.ViewList == null) return Array.Empty<object>();

            var childProperty = _getChildProperty();
            var list = new List<object>();
            foreach (var row in cv.ViewList)
                list.AddRange(_flatteningService.GetChildItems(row, childProperty));
            return list;
        }

        public GroupingManager(
            DataFlatteningService flatteningService,
            Func<ICustomCollectionView?> getCollectionView,
            Func<SKGroupDefinition?> getGroupSettings,
            Func<SkGridColumnCollection?> getColumns,
            Func<SkiaRenderer?> getRenderer,
            Func<string?> getChildProperty,
            Action updateTotalRows,
            Action refresh)
        {
            _flatteningService = flatteningService;
            _getCollectionView = getCollectionView;
            _getGroupSettings = getGroupSettings;
            _getColumns = getColumns;
            _getRenderer = getRenderer;
            _getChildProperty = getChildProperty;
            _updateTotalRows = updateTotalRows;
            _refresh = refresh;
        }

        // ── Update Collection (rebuild flattened data) ──────────────────

        public void UpdateCollection()
        {
            using var _metrics = GridMetrics.Measure(GridMetrics.DataUpdateCollection);
            // Removed per-update logging — too frequent, causes debug lag

            var cv = _getCollectionView();
            if (cv == null) return;

            var renderer = _getRenderer();
            if (renderer == null) return;

            if (_getGroupSettings() != null)
            {
                renderer.GroupItemSource = _flatteningService.FlattenGroupedItems(cv, GroupToggleDetails, _getGroupSettings(), _getColumns());
            }
            else
            {
                renderer.Items = _flatteningService.FlattenRowItems(cv, RowToggleDetails, _getChildProperty(), ExpandedItems);
            }

            _updateTotalRows();
        }

        // ── Toggle Operations ───────────────────────────────────────────

        public void UpdateGroupToggle(double x, double y, int totalRows)
        {
            var groupSettings = _getGroupSettings();
            if (groupSettings == null || totalRows <= 0) return;

            var values = GroupToggleDetails.Where(v =>
                (x >= v.Value.x && x <= v.Value.x + v.Value.width) &&
                (y >= v.Value.y && y <= v.Value.y + v.Value.height)).LastOrDefault();

            ResetGroupToggleValues();
            if (values.Key != null)
            {
                var res = GroupToggleDetails[values.Key];
                res.IsExpended = !res.IsExpended;
                GroupToggleDetails[values.Key] = res;

                var renderer = _getRenderer();
                if (renderer?.GroupItemSource != null)
                {
                    // Opt-in: keep this group's subtotal rows visible while collapsed (default false → no-op).
                    bool keepSubtotals = groupSettings.ShowSubtotalsWhenCollapsed;
                    foreach (var item in renderer.GroupItemSource.Where(g => g.GroupName == values.Key))
                        item.IsExpanded = (keepSubtotals && item.IsGroupSubTotal) ? true : res.IsExpended;
                }

                _updateTotalRows();
                _refresh();

                // A glyph click is always a real state change → notify unconditionally.
                NotifyExpandChanged(res.IsExpended, SkExpandChangeReason.UserToggle,
                    values.Key, null, () => GroupDataItems(values.Key));
            }
        }

        public void UpdateRowToggle(double x, double y, int totalRows)
        {
            var groupSettings = _getGroupSettings();
            if (groupSettings != null || totalRows <= 0) return;

            var values = RowToggleDetails.Where(v =>
                (x >= v.Value.x && x <= v.Value.x + v.Value.width) &&
                (y >= v.Value.y && y <= v.Value.y + v.Value.height)).LastOrDefault();

            ResetRowToggleValues();
            if (values.Key != null)
            {
                var res = RowToggleDetails[values.Key];
                RowToggleDetails[values.Key] = res;

                // Toggle expand state in ExpandedItems dictionary (no ITreeItem dependency)
                bool currentlyExpanded = ExpandedItems.GetValueOrDefault(values.Key, false);
                ExpandedItems[values.Key] = !currentlyExpanded;

                var cv = _getCollectionView();
                var renderer = _getRenderer();
                if (cv != null && renderer != null)
                    renderer.Items = _flatteningService.FlattenRowItems(cv, RowToggleDetails, _getChildProperty(), ExpandedItems);

                _updateTotalRows();
                _refresh();

                // A glyph click is always a real state change → notify unconditionally.
                NotifyExpandChanged(!currentlyExpanded, SkExpandChangeReason.UserToggle,
                    null, values.Key, () => _flatteningService.GetChildItems(values.Key, _getChildProperty()));
            }
        }

        // ── Tree-mode (ChildProperty) expand/collapse API ────────────────
        //
        // Grouped mode already had ToggleGroup/ExpandAll/CollapseAll; tree rows could previously
        // only be toggled by clicking the glyph. These are the programmatic equivalents.

        public bool IsRowExpanded(object? item) =>
            item != null && ExpandedItems.GetValueOrDefault(item, false);

        public void ToggleRow(object? item, bool isExpand)
        {
            if (item == null || _getGroupSettings() != null) return; // grouped mode → use ToggleGroup

            bool wasExpanded = ExpandedItems.GetValueOrDefault(item, false);
            if (wasExpanded == isExpand) return; // no change → no re-flatten, no notification

            ExpandedItems[item] = isExpand;
            ReflattenRows();

            NotifyExpandChanged(isExpand, SkExpandChangeReason.Api,
                null, item, () => _flatteningService.GetChildItems(item, _getChildProperty()));
        }

        public void SetAllRowsExpanded(bool isExpand)
        {
            if (_getGroupSettings() != null) return; // grouped mode → use ExpandAll/CollapseAll

            var cv = _getCollectionView();
            if (cv?.ViewList == null) return;

            // Only rows that actually HAVE children — marking a childless row "expanded" would make
            // GetExpandedRowItems lie and would flip the control's AnyRowExpanded splice gate on for
            // a grid with no expandable rows.
            var childProperty = _getChildProperty();
            foreach (var row in cv.ViewList)
                if (row != null && _flatteningService.GetChildItems(row, childProperty).Count > 0)
                    ExpandedItems[row] = isExpand;

            ReflattenRows();

            NotifyExpandChanged(isExpand,
                isExpand ? SkExpandChangeReason.ExpandAll : SkExpandChangeReason.CollapseAll,
                null, null, AllChildItems);
        }

        /// <summary>Re-flatten + settle after a tree-mode expand state change (mirrors UpdateRowToggle's tail).</summary>
        private void ReflattenRows()
        {
            var cv = _getCollectionView();
            var renderer = _getRenderer();
            if (cv != null && renderer != null)
                renderer.Items = _flatteningService.FlattenRowItems(cv, RowToggleDetails, _getChildProperty(), ExpandedItems);

            _updateTotalRows();
            _refresh();
        }

        /// <summary>Current expand state per group name. Empty until the first flatten seeds it.</summary>
        public IReadOnlyDictionary<string, bool> GetGroupExpandStates()
        {
            var result = new Dictionary<string, bool>(GroupToggleDetails.Count);
            foreach (var kvp in GroupToggleDetails)
                result[kvp.Key] = kvp.Value.IsExpended;
            return result;
        }

        /// <summary>True when the named group is expanded. Unknown group names report the seeded default (true).</summary>
        public bool IsGroupExpanded(string groupName) =>
            !GroupToggleDetails.TryGetValue(groupName ?? "", out var details) || details.IsExpended;

        /// <summary>Parent rows currently expanded (tree mode).</summary>
        public IReadOnlyList<object> GetExpandedRowItems()
        {
            var list = new List<object>();
            foreach (var kvp in ExpandedItems)
                if (kvp.Value) list.Add(kvp.Key);
            return list;
        }

        /// <summary>
        /// Data rows currently visible (rendered): grouped mode excludes rows of collapsed groups and
        /// all header/subtotal rows; tree mode includes expanded parents' children.
        /// </summary>
        public IReadOnlyList<object> GetVisibleDataItems()
        {
            var renderer = _getRenderer();
            if (renderer == null) return Array.Empty<object>();

            var list = new List<object>();
            if (_getGroupSettings() != null)
            {
                foreach (var g in renderer.GroupItemSource)
                    if (g.Item != null && g.IsExpanded) list.Add(g.Item);
            }
            else
            {
                foreach (var r in renderer.Items)
                    if (r.Item != null) list.Add(r.Item);
            }
            return list;
        }

        public void ToggleGroup(string groupName, bool isExpand)
        {
            var groupSettings = _getGroupSettings();
            if (groupSettings == null) return;

            ResetGroupToggleValues();
            var renderer = _getRenderer();
            if (renderer?.GroupItemSource == null) return;

            // Previous state for change-gating the notification only — the state mutation below is
            // unconditional, exactly as before. Unknown group name → seeded default (true).
            bool wasExpanded = IsGroupExpanded(groupName);

            // Opt-in: keep this group's subtotal rows visible while collapsed (default false → no-op).
            bool keepSubtotals = groupSettings.ShowSubtotalsWhenCollapsed;
            foreach (var item in renderer.GroupItemSource.Where(g => g.GroupName == groupName))
                item.IsExpanded = (keepSubtotals && item.IsGroupSubTotal) ? true : isExpand;

            // B23 fix: also update GroupToggleDetails dictionary
            if (GroupToggleDetails.ContainsKey(groupName))
            {
                var res = GroupToggleDetails[groupName];
                res.IsExpended = isExpand;
                GroupToggleDetails[groupName] = res;
            }

            _updateTotalRows();
            _refresh();

            if (wasExpanded != isExpand)
                NotifyExpandChanged(isExpand, SkExpandChangeReason.Api,
                    groupName, null, () => GroupDataItems(groupName));
        }

        public void CollapseAll()
        {
            var groupSettings = _getGroupSettings();
            if (groupSettings == null) return;

            ResetGroupToggleValues();
            var renderer = _getRenderer();
            if (renderer?.GroupItemSource == null) return;

            // Opt-in: keep both per-group subtotals (IsGroupSubTotal) AND the grand-total rows
            // (IsHeaderSubTotal) visible after a Collapse-All (default false → no-op).
            bool keepSubtotals = groupSettings.ShowSubtotalsWhenCollapsed;
            foreach (var item in renderer.GroupItemSource)
                item.IsExpanded = (keepSubtotals && (item.IsGroupSubTotal || item.IsHeaderSubTotal)) ? true : false;

            // B23 fix: also update GroupToggleDetails dictionary
            foreach (var key in GroupToggleDetails.Keys.ToList())
            {
                var res = GroupToggleDetails[key];
                res.IsExpended = false;
                GroupToggleDetails[key] = res;
            }

            _updateTotalRows();
            _refresh();

            // ONE bulk notification for every group (GroupName null), not one per group.
            NotifyExpandChanged(false, SkExpandChangeReason.CollapseAll,
                null, null, () => GroupDataItems(null));
        }

        public void ExpandAll()
        {
            if (_getGroupSettings() == null) return;

            ResetGroupToggleValues();
            var renderer = _getRenderer();
            if (renderer?.GroupItemSource == null) return;

            foreach (var item in renderer.GroupItemSource)
                item.IsExpanded = true;

            // B23 fix: also update GroupToggleDetails dictionary
            foreach (var key in GroupToggleDetails.Keys.ToList())
            {
                var res = GroupToggleDetails[key];
                res.IsExpended = true;
                GroupToggleDetails[key] = res;
            }

            _updateTotalRows();
            _refresh();

            // ONE bulk notification for every group (GroupName null), not one per group.
            NotifyExpandChanged(true, SkExpandChangeReason.ExpandAll,
                null, null, () => GroupDataItems(null));
        }

        // ── Reset Coordinates ───────────────────────────────────────────

        public void ResetGroupToggleValues()
        {
            var cv = _getCollectionView();
            if (cv == null && _getGroupSettings() == null) return;

            foreach (var groupName in GroupToggleDetails.Keys.ToList())
            {
                var res = GroupToggleDetails[groupName];
                res.x = 0; res.y = 0; res.width = 0; res.height = 0;
                GroupToggleDetails[groupName] = res;
            }
        }

        public void ResetRowToggleValues()
        {
            var cv = _getCollectionView();
            if (cv == null && _getGroupSettings() == null) return;

            foreach (var item in RowToggleDetails.Keys.ToList())
            {
                var res = RowToggleDetails[item];
                res.x = 0; res.y = 0; res.width = 0; res.height = 0;
                RowToggleDetails[item] = res;
            }
        }
    }
}
