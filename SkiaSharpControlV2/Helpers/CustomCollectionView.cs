
using SkiaSharpControlV2.Diagnostics;


using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace SkiaSharpControlV2.Helpers
{

    // CV is a library-internal implementation detail. Consumers configure the grid
    // through SkiaGridViewV2 DPs / methods only; the CV itself is not part of the
    // public API surface. Tests reach it via InternalsVisibleTo (AssemblyInfo.cs).
    internal interface ICustomCollectionView : IEnumerable, IDisposable
    {
        IEnumerable Items { get; }
        bool IsLiveSort { get; set; }
        bool AddNewRowAtBottomInGroup { get; set; }
        bool FilterByGroup { get; set; }
        List<object> ViewList { get; }
        List<Filter> Filters { get; }
        List<SKGroupField>? GroupFields { get; set; }
        int Count { get; }
        IEnumerable<Group> GroupList { get; }
        void ApplyGroup(string propertyName);
        string ChildProperty { get; set; }
        void ClearGroup();
        void AddSort(string propertyName, ListSortDirection sortDirection);
        /// <summary>True while at least one sort is applied — i.e. view order is NOT source order.</summary>
        bool IsSorted { get; }
        void ClearSortDescriptions();
        bool AddOrUpdateFilter(Filter filter);
        bool RemoveFilter(Filter filter);

        /// <summary>Filter by exact value comparison (e.g., Price > 100).</summary>
        bool AddValueFilter(string column, string @operator, string value, Type dataType);
        /// <summary>Filter by wildcard text match (e.g., Name matches "ACCT*").</summary>
        bool AddTextFilter(string column, string text);
        /// <summary>Filter by list of allowed values (e.g., Status in ["Open", "Filled"]).</summary>
        bool AddListFilter(string column, List<string> list);
        /// <summary>Remove any active filter on the specified column.</summary>
        bool RemoveFilter(string column);
        void Refresh();
        /// <summary>Flush deferred refresh if dirty. Called by grid's render timer.</summary>
        void RefreshIfDirty();
        bool MoveRowUp(object item);
        bool MoveRowDown(object item);
        bool InsertBlankRow(object item, object newObject);
        bool DeleteEmptyRow(object item);

        /// <summary>
        /// Insert K items at once, raising a SINGLE Add (K NewItems + NewStartingIndex) so the grid
        /// splices K rows in one pass instead of re-flattening. Fast path requires no sort, no group,
        /// and no filters (view order == source order); otherwise DEGRADES to per-item insertion,
        /// which preserves sorted/filtered placement at the cost of the single-splice optimization.
        /// </summary>
        void InsertRange(int index, IReadOnlyList<object> items);
        /// <summary>
        /// Remove K items at once. Fast path (single Remove raise) requires no grouping and that the
        /// items form a contiguous block in view order; otherwise DEGRADES to per-item removal.
        /// </summary>
        void RemoveRange(IReadOnlyList<object> items);

        event NotifyCollectionChangedEventHandler CollectionChanged;
    }



    internal class Group
    {
        public object Key { get; set; }
        public List<object> Items { get; } = new List<object>();
        public Dictionary<string, object> Aggregates { get; } = new();

    }
    internal class Row
    {
        public bool IsExpanded { get; set; }
        public object Item { get; set; }
        public List<object> Details { get; } = new List<object>();

    }
    internal class CustomCollectionView : ICustomCollectionView
    {
        private readonly IList _source;
        internal readonly List<object> _viewList = new();
        private readonly List<Filter> _filters = new();
        private readonly List<(string Prop, ListSortDirection Dir)> _sorts = new();
        private string _groupProperty;

        public bool IsLiveSort { get; set; }
        public bool AddNewRowAtBottomInGroup { get; set; }
        public string ChildProperty { get; set; }
        public bool FilterByGroup { get; set; }

        // ── Optimization: O(1) lookup caches ────────────────────────────
        private HashSet<string> _filterColumns = new();
        private Dictionary<object, Group> _groupsByKey = new();
        private Dictionary<string, SKGroupField> _groupFieldMap = new();
        private SortComparer? _cachedSortComparer;

        // ── Optimization: Throttle Refresh ──────────────────────────────
        private volatile bool _isDirty;

        // ── Explicit-view-index insert (set by the grid before raising a source Add) ──
        // When non-null, the next InsertNewItem call places the item at exactly this
        // visual row index, bypassing the sort comparer AND the filter. Cleared once
        // consumed. Side-channel set via SkiaGridViewV2.InsertAtViewIndex().
        private int? _explicitNextViewIndex;
        internal void RequestExplicitViewIndex(int viewIndex)
        {
            _explicitNextViewIndex = viewIndex;
        }

        // Set while a range operation (InsertRange/RemoveRange fast path) batches its _source
        // mutations, so the per-item Source_CollectionChanged mirror does NOT decompose the batch
        // into K single raises — the range method performs the block _viewList splice and raises
        // ONE Add/Remove itself.
        private bool _suppressSourceMirror;

        /// <summary>True if data changed and Refresh should be called. Grid's timer calls RefreshIfDirty().</summary>
        public bool IsDirty => _isDirty;

        /// <summary>Call from the grid's render timer. Does one Refresh per render cycle instead of per-property-change.</summary>
        public void RefreshIfDirty()
        {
            if (_isDirty)
            {
                _isDirty = false;
                Refresh();
            }
        }
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public IEnumerable Items => GetCurrentItems();
        public int Count => _viewList.Count;
        public IEnumerable<Group> GroupList => _groups;

        private List<SKGroupField>? _groupFields;
        public List<SKGroupField>? GroupFields
        {
            get => _groupFields;
            set
            {
                _groupFields = value;
                RebuildGroupFieldMap();
            }
        }

        List<object> ICustomCollectionView.ViewList => _viewList;

        public List<Filter> Filters => _filters;

        private readonly List<Group> _groups = new();
        private ReflectionHelper reflectionHelper;

        public CustomCollectionView(IEnumerable items, ReflectionHelper reflectionHelper)
        {
            this.reflectionHelper = reflectionHelper;
            _source = items as IList ?? throw new ArgumentException("Items must be IList");

            foreach (var item in _source)
            {
                _viewList.Add(item);
            }

            if (_source is INotifyCollectionChanged notifier)
            {
                notifier.CollectionChanged -= Source_CollectionChanged;
                notifier.CollectionChanged += Source_CollectionChanged;
            }
            // B7 fix: synchronous registration (was Task.Run causing race condition)
            foreach (var item in _source)
            {
                AddPropertyChange(item);
            }
            Refresh();
        }

        private void AddPropertyChange(object item)
        {
            if (item is INotifyPropertyChanged npc)
                npc.PropertyChanged += Item_PropertyChanged;
        }
        private void RemovePropertyChange(object item)
        {
            if (item is INotifyPropertyChanged npc)
                npc.PropertyChanged -= Item_PropertyChanged;
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Normal filters: incremental per-item update (not full Refresh)
            if (!FilterByGroup && _filterColumns.Contains(e.PropertyName ?? ""))
            {
                int existingIdx = _viewList.IndexOf(sender);
                bool wasVisible = existingIdx >= 0;
                bool nowPasses = PassAllFilter(sender);

                if (wasVisible && !nowPasses)
                {
                    _viewList.RemoveAt(existingIdx);
                    // Event carries the view index so the grid can apply the change
                    // incrementally instead of re-flattening all N rows (M2 perf fix).
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, sender, existingIdx));
                }
                else if (!wasVisible && nowPasses)
                {
                    _viewList.Add(sender);
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, sender, _viewList.Count - 1));
                }
                // If visibility unchanged: no-op (most common case — just a value update)
                return;
            }

            // Sort column changed with live sort: mark dirty for next render cycle
            if (IsLiveSort && _sorts.Any(s => s.Prop == e.PropertyName))
            {
                _isDirty = true;
                return;
            }

            // Group-based filters: O(1) lookups
            if (FilterByGroup && _groupFieldMap.ContainsKey(e.PropertyName ?? ""))
            {
                var groupKey = reflectionHelper.GetPropValue(sender, _groupProperty);
                if (groupKey != null && _groupsByKey.TryGetValue(groupKey, out var group))
                {
                    CalculateAggregates(group);

                    if (!PassGroupFilter(group))
                        _groups.Remove(group);
                    else if (!_groups.Contains(group))
                        _groups.Add(group);

                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            }
        }

        private void Source_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // A range operation is batching source mutations and will raise ONE Add/Remove itself —
            // skip the per-item mirror so the batch stays a single event.
            if (_suppressSourceMirror) return;
            SkiaSharpControlV2.Diagnostics.GridMetrics.Increment(SkiaSharpControlV2.Diagnostics.GridMetrics.CounterCollectionChanges);
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    var countBefore = _viewList.Count;
                    int insertedViewIndex = InsertNewItem(item, e.NewStartingIndex);
                    AddPropertyChange(item);
                    // B17 fix: only fire Add event if item was actually added to viewList.
                    // The raise carries the VIEW index the item landed at so the grid can
                    // splice its flat row list incrementally instead of re-flattening all
                    // N rows per event (M2 perf fix — O(N) flatten 4×/sec saturated the
                    // dispatcher at 70k+ rows).
                    if (_viewList.Count > countBefore)
                        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, insertedViewIndex));
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    int idx = _viewList.IndexOf(item);
                    if (idx >= 0)
                    {
                        _viewList.RemoveAt(idx);
                        // B16 fix: also remove from Group.Items when grouping active
                        // Use O(1) _groupsByKey lookup instead of O(g) scan
                        if (!string.IsNullOrEmpty(_groupProperty))
                        {
                            var key = reflectionHelper.GetPropValue(item, _groupProperty) ?? "";
                            if (_groupsByKey.TryGetValue(key, out var group))
                            {
                                group.Items.Remove(item);
                                // Clean up empty groups
                                if (group.Items.Count == 0)
                                {
                                    _groups.Remove(group);
                                    _groupsByKey.Remove(key);
                                }
                            }
                        }
                        RemovePropertyChange(item);
                        // idx = the view index the item was removed from (see Add branch note).
                        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, idx));
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Move)
            {
                // Source collection reordered — rebuild view to reflect new order.
                // Refresh() already raises a single Reset (end of Refresh()); no extra raise here.
                Refresh();
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                // Source item replaced — rebuild view
                if (e.OldItems != null)
                    foreach (var item in e.OldItems) RemovePropertyChange(item);
                if (e.NewItems != null)
                    foreach (var item in e.NewItems) AddPropertyChange(item);
                // Refresh() already raises a single Reset; no extra raise here.
                Refresh();
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Unsubscribe PropertyChanged from every currently-viewed item. This loop is NOT
                // redundant with Refresh()'s _viewList.Clear() — that clear does not unsubscribe, so
                // dropping it would leak subscriptions on every source Reset.
                foreach (var item in _viewList)
                {
                    RemovePropertyChange(item);
                }
                // Refresh() clears _viewList and raises a single Reset itself — so no explicit
                // _viewList.Clear() and no second raise here. Previously a source Reset fired TWO
                // Resets (this branch + Refresh()), causing two full re-flattens on the grid side.
                Refresh();
            }
        }

        /// <summary>
        /// Insert a newly-arrived source item into the view. Returns the _viewList index
        /// the item landed at, or -1 when the item was filtered out (not added). The index
        /// is forwarded on the CollectionChanged raise so the grid can splice its flat
        /// row-model list incrementally instead of re-flattening all N rows per event.
        /// </summary>
        private int InsertNewItem(object item, int index)
        {
            using var _metrics = GridMetrics.Measure(GridMetrics.DataInsertNewItem);

            // Explicit-view-index path: the grid told us exactly where to place this row.
            // Bypass BOTH the filter and the sort comparer — the caller's intent is
            // "show this row at visual row N regardless of current view rules." The
            // override is consumed (cleared) so the next insert takes the normal path.
            if (_explicitNextViewIndex.HasValue)
            {
                int viewIdx = _explicitNextViewIndex.Value;
                _explicitNextViewIndex = null;
                int clamped = Math.Max(0, Math.Min(viewIdx, _viewList.Count));
                _viewList.Insert(clamped, item);
                return clamped;
            }

            if (!PassAllFilter(item) && !FilterByGroup)
                return -1;

            // Reuse cached SortComparer if available — avoids per-insert allocation.
            // Sorted-insert is decoupled from IsLiveSort: when a sort is active, new rows
            // appended via Add() land at the sort position regardless of IsLiveSort.
            // IsLiveSort only gates the property-mutation re-sort path in
            // Item_PropertyChanged (live reorder when a sort-key property changes on an
            // existing row).
            SortComparer? comparer = null;
            if (_sorts.Any())
                comparer = _cachedSortComparer ??= new SortComparer(_sorts, reflectionHelper);

            // Distinguish Append from Insert(middle):
            //   - source.Add(item)        → NewStartingIndex == _source.Count - 1 (end). "Append."
            //   - source.Insert(N, item)  → NewStartingIndex == N. If N < end, "explicit middle."
            // For ungrouped views we honor the user's explicit middle index even when a
            // sort is active — e.g. inserting a blank row for editing must stay where the
            // user put it, not jump to the top because its sort key happens to be empty.
            // Appends still go through the sort comparer (backend feed → sorted position).
            bool isExplicitMiddleInsert = index >= 0 && index < _source.Count - 1;

            int insertedViewIndex;

            if (!string.IsNullOrEmpty(_groupProperty))
            {
                var key = reflectionHelper.GetPropValue(item, _groupProperty) ?? "";

                // O(1) group lookup via _groupsByKey (was O(g) FirstOrDefault)
                if (!_groupsByKey.TryGetValue(key, out var group))
                {
                    group = new Group { Key = key };
                    _groups.Add(group);
                    _groupsByKey[key] = group;
                }

                // Grouped views always use the sort comparer when sort is active — the
                // user's flat source-index doesn't map to a meaningful position within a
                // group. (Stable behavior; the explicit-middle-insert escape hatch only
                // applies to ungrouped views.)
                if (comparer != null)
                {
                    var _index = group.Items.BinarySearch(item, comparer);
                    group.Items.Insert(_index < 0 ? ~_index : _index, item);
                    // B20 fix: sorted insertion into _viewList (was just appending)
                    var vIndex = _viewList.BinarySearch(item, comparer);
                    insertedViewIndex = vIndex < 0 ? ~vIndex : vIndex;
                    _viewList.Insert(insertedViewIndex, item);
                }
                else
                {
                    if (AddNewRowAtBottomInGroup)
                        group.Items.Add(item);
                    else
                        group.Items.Insert(0, item);

                    _viewList.Add(item);
                    insertedViewIndex = _viewList.Count - 1;
                }

                CalculateAggregates(group);

                if (FilterByGroup && !PassGroupFilter(group))
                {
                    _groups.Remove(group);
                    _groupsByKey.Remove(key);
                }
            }
            else
            {
                if (comparer != null && !isExplicitMiddleInsert)
                {
                    // Append with active sort → land at sort position.
                    var _index = _viewList.BinarySearch(item, comparer);
                    insertedViewIndex = _index < 0 ? ~_index : _index;
                    _viewList.Insert(insertedViewIndex, item);
                }
                else
                {
                    // No sort active, OR an explicit middle Insert(N, item): honor the
                    // user's index. Filters may have removed earlier items, so clamp
                    // index to the current view bounds.
                    int clamped = index < 0 ? 0 : Math.Min(index, _viewList.Count);
                    _viewList.Insert(clamped, item);
                    insertedViewIndex = clamped;
                }
            }

            return insertedViewIndex;
        }

        /// <inheritdoc/>
        public void InsertRange(int index, IReadOnlyList<object> items)
        {
            if (items == null || items.Count == 0) return;

            // Fast path only when view order == source order (no reordering/filtering/grouping),
            // so K items land contiguously at the mapped index and one Add is unambiguous.
            bool fastPath = _sorts.Count == 0 && string.IsNullOrEmpty(_groupProperty) && _filters.Count == 0;

            if (fastPath)
            {
                // Mirror into _source (so a later Refresh keeps the rows) WITHOUT per-item decomposition.
                _suppressSourceMirror = true;
                try
                {
                    for (int j = 0; j < items.Count; j++)
                        _source.Insert(index + j, items[j]);
                }
                finally { _suppressSourceMirror = false; }

                int viewIndex = Math.Max(0, Math.Min(index, _viewList.Count));
                for (int j = 0; j < items.Count; j++)
                {
                    _viewList.Insert(viewIndex + j, items[j]);
                    AddPropertyChange(items[j]);
                }
                // ONE Add carrying K NewItems + the view index → grid does a single K-row splice.
                CollectionChanged?.Invoke(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new List<object>(items), viewIndex));
            }
            else
            {
                // Degrade: normal per-item source mutation; the existing mirror applies sort/filter
                // placement and raises a granular Add per item.
                for (int j = 0; j < items.Count; j++)
                    _source.Insert(index + j, items[j]);
            }
        }

        /// <inheritdoc/>
        public void RemoveRange(IReadOnlyList<object> items)
        {
            if (items == null || items.Count == 0) return;

            int startIndex = _viewList.IndexOf(items[0]);
            bool contiguous = string.IsNullOrEmpty(_groupProperty)
                && startIndex >= 0
                && startIndex + items.Count <= _viewList.Count;
            if (contiguous)
            {
                for (int j = 0; j < items.Count; j++)
                    if (!ReferenceEquals(_viewList[startIndex + j], items[j])) { contiguous = false; break; }
            }

            if (contiguous)
            {
                _suppressSourceMirror = true;
                try
                {
                    foreach (var item in items) _source.Remove(item);
                }
                finally { _suppressSourceMirror = false; }

                _viewList.RemoveRange(startIndex, items.Count);
                foreach (var item in items) RemovePropertyChange(item);
                // ONE Remove carrying K OldItems + the view index → grid does a single K-row splice.
                CollectionChanged?.Invoke(this,
                    new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, new List<object>(items), startIndex));
            }
            else
            {
                // Degrade: per-item removal via the existing mirror (scattered / grouped set).
                foreach (var item in items) _source.Remove(item);
            }
        }

        public void ApplyGroup(string propertyName)
        {
            _groupProperty = propertyName;
            Refresh();
        }

        public void ClearGroup()
        {
            _groupProperty = null;
            _groups.Clear();
            Refresh();
        }

        public void AddSort(string propertyName, ListSortDirection sortDirection)
        {
            _sorts.Add((propertyName, sortDirection));
            Refresh();
        }

        public bool IsSorted => _sorts.Count > 0;

        public void ClearSortDescriptions()
        {
            _sorts.Clear();
            Refresh();
        }

        public bool AddOrUpdateFilter(Filter filter)
        {
            var f = _filters.FirstOrDefault(x => x.Column == filter.Column);
            if (f == null)
                _filters.Add(filter);
            else
            {
                _filters.Remove(f);
                _filters.Add(filter);
            }
            RebuildFilterColumns();
            Refresh();
            return true;
        }

        public bool RemoveFilter(Filter filter)
        {
            var f = _filters.FirstOrDefault(x => x.Column == filter.Column);
            if (f != null)
            {
                _filters.Remove(f);
                RebuildFilterColumns();
                Refresh();
                return true;
            }
            return false;
        }

        private void RebuildFilterColumns()
        {
            _filterColumns = new HashSet<string>(_filters.Where(f => f.Column != null).Select(f => f.Column!));
        }

        /// <inheritdoc />
        public bool AddValueFilter(string column, string @operator, string value, Type dataType)
        {
            return AddOrUpdateFilter(new Filter
            {
                Column = column,
                FilterType = FilterType.Value,
                Value = (@operator, value, dataType)
            });
        }

        /// <inheritdoc />
        public bool AddTextFilter(string column, string text)
        {
            return AddOrUpdateFilter(new Filter
            {
                Column = column,
                FilterType = FilterType.Text,
                Text = text
            });
        }

        /// <inheritdoc />
        public bool AddListFilter(string column, List<string> list)
        {
            return AddOrUpdateFilter(new Filter
            {
                Column = column,
                FilterType = FilterType.List,
                List = list
            });
        }

        /// <inheritdoc />
        public bool RemoveFilter(string column)
        {
            var f = _filters.FirstOrDefault(x => x.Column == column);
            if (f != null)
            {
                _filters.Remove(f);
                Refresh();
                return true;
            }
            return false;
        }

        public void Refresh()
        {
            using var _metrics = GridMetrics.Measure(GridMetrics.CollectionViewRefresh);

            // Mark clean first — any throttled property changes are about to be flushed
            _isDirty = false;

            // Fast path: no filters, no sort, no grouping (the common flat-grid state — P1's default).
            // The only necessary work is mirroring _source into _viewList in source order. This skips
            // per-item PassAllFilter evaluation, the SortComparer build, and the whole group-dictionary
            // pass. The resulting _viewList / _groups / _groupsByKey / _groupFieldMap / _cachedSortComparer
            // state is identical to what the full path below produces for this input, and it raises the
            // same single Reset. _cachedSortComparer is nulled to match the full path (:_sorts empty ⇒ null),
            // preventing a stale comparer from being reused by the lazy sorted-insert path.
            if (_filters.Count == 0 && _sorts.Count == 0 && string.IsNullOrEmpty(_groupProperty))
            {
                RebuildFilterColumns();
                _viewList.Clear();
                _groups.Clear();
                _groupsByKey.Clear();
                _cachedSortComparer = null;
                foreach (var item in _source)
                    _viewList.Add(item);
                RebuildGroupFieldMap();
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                return;
            }

            // Defensive cache rebuild — in case _filters or GroupFields were modified directly
            RebuildFilterColumns();
            RebuildGroupFieldMap();

            _viewList.Clear();
            _groups.Clear();
            _groupsByKey.Clear();

            // Rebuild sort comparer once (reused for viewList + each group)
            if (_sorts.Count > 0)
                _cachedSortComparer = new SortComparer(_sorts, reflectionHelper);
            else
                _cachedSortComparer = null;

            // Filter source into viewList
            foreach (var item in _source)
            {
                if (!FilterByGroup && PassAllFilter(item))
                    _viewList.Add(item);
                else if (FilterByGroup)
                    _viewList.Add(item);
            }

            // Sort once
            if (_cachedSortComparer != null)
                _viewList.Sort(_cachedSortComparer);

            // Group via manual dictionary (no LINQ GroupBy allocation)
            if (!string.IsNullOrEmpty(_groupProperty))
            {
                var groupDict = new Dictionary<object, Group>();
                var groupOrder = new List<object>(); // preserve insertion order for stable ordering

                foreach (var item in _viewList)
                {
                    var key = reflectionHelper.GetPropValue(item, _groupProperty);
                    if (key == null) key = "";

                    if (!groupDict.TryGetValue(key, out var group))
                    {
                        group = new Group { Key = key };
                        groupDict[key] = group;
                        groupOrder.Add(key);
                    }
                    group.Items.Add(item);
                }

                // Sort groups by key + sort items within each group
                // Use string comparison to avoid throwing on mixed-type keys
                foreach (var key in groupOrder.OrderBy(k => k?.ToString() ?? "", StringComparer.OrdinalIgnoreCase))
                {
                    var group = groupDict[key];

                    if (_cachedSortComparer != null)
                        group.Items.Sort(_cachedSortComparer);

                    CalculateAggregates(group);

                    if (!FilterByGroup || PassGroupFilter(group))
                        _groups.Add(group);
                }

                // Build O(1) group lookup
                _groupsByKey = groupDict;
            }

            // Rebuild group field map
            RebuildGroupFieldMap();

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void RebuildGroupFieldMap()
        {
            _groupFieldMap.Clear();
            if (GroupFields != null)
            {
                foreach (var field in GroupFields)
                {
                    if (!string.IsNullOrEmpty(field.BindingPath))
                        _groupFieldMap[field.BindingPath] = field;
                }
            }
        }

        private void CalculateAggregates(Group group)
        {
            using var _metrics = GridMetrics.Measure(GridMetrics.GroupAggregate);
            if (GroupFields == null || GroupFields.Count == 0) return;

            group.Aggregates.Clear();

            // Single-pass per field: iterate group items once per field, accumulate in-line
            foreach (var field in GroupFields)
            {
                if (field.Aggregation == SkAggregation.None) continue;

                object? result = null;
                decimal sum = 0;
                int count = 0;
                IComparable? min = null, max = null;
                HashSet<string>? distinctSet = null;

                int numericCount = 0;  // Only count numeric-convertible values for Avg
                Type? minMaxType = null; // Track first type for Min/Max to avoid mixed-type CompareTo

                foreach (var item in group.Items)
                {
                    var val = reflectionHelper.GetPropValue(item, field.BindingPath);
                    if (val == null) continue;

                    count++; // Total non-null count (used by Count aggregation)

                    switch (field.Aggregation)
                    {
                        case SkAggregation.Sum:
                        case SkAggregation.Avg:
                            var dec = TryConvertToDecimal(val);
                            if (dec.HasValue)
                            {
                                sum += dec.Value;
                                numericCount++;
                            }
                            break;

                        case SkAggregation.Min:
                            if (val is IComparable cMin)
                            {
                                // Only compare same types to avoid CompareTo throwing on mixed types
                                if (minMaxType == null) minMaxType = val.GetType();
                                if (val.GetType() == minMaxType)
                                    min = min == null || cMin.CompareTo(min) < 0 ? cMin : min;
                            }
                            break;

                        case SkAggregation.Max:
                            if (val is IComparable cMax)
                            {
                                if (minMaxType == null) minMaxType = val.GetType();
                                if (val.GetType() == minMaxType)
                                    max = max == null || cMax.CompareTo(max) > 0 ? cMax : max;
                            }
                            break;

                        case SkAggregation.Distinct:
                            if (val is string s && !string.IsNullOrWhiteSpace(s))
                            {
                                distinctSet ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                distinctSet.Add(s);
                            }
                            break;

                        case SkAggregation.Count:
                            break; // count++ already done above
                    }
                }

                if (count == 0) continue;

                result = field.Aggregation switch
                {
                    SkAggregation.Sum => sum,
                    SkAggregation.Avg => numericCount > 0 ? sum / numericCount : 0m,
                    SkAggregation.Min => min,
                    SkAggregation.Max => max,
                    SkAggregation.Count => count,
                    SkAggregation.Distinct => distinctSet != null
                        ? string.Join('/', distinctSet.OrderBy(x => x))
                        : "",
                    _ => null
                };

                group.Aggregates[field.BindingPath] = result;
            }
        }

        /// <summary> Safely sums only numeric values </summary>
        private static decimal SafeNumericSum(IEnumerable<object> values)
        {
            return values
                .Select(v => TryConvertToDecimal(v))
                .Where(v => v.HasValue)
                .Sum(v => v.Value);
        }

        private static decimal? TryConvertToDecimal(object v)
        {
            if (v == null) return null;

            try
            {
                if (v is decimal d) return d;
                if (v is IConvertible c)
                {
                    var str = c.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(str)) return null;

                    return Convert.ToDecimal(str, System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }


        /// <summary> Safely averages only numeric values </summary>
        // B12 fix: use TryConvertToDecimal (was Convert.ToDecimal which throws on non-numeric)
        private decimal SafeNumericAverage(IEnumerable<object> values)
        {
            var nums = values.Select(v => TryConvertToDecimal(v))
                             .Where(v => v.HasValue)
                             .Select(v => v.Value)
                             .ToList();
            return nums.Count > 0 ? nums.Average() : 0;
        }

        /// <summary> Handles Min/Max for IComparable values </summary>
        private object? SafeComparable(List<object> values, bool isMin)
        {
            var comparableValues = values.OfType<IComparable>().ToList();
            if (comparableValues.Count == 0) return null;

            return isMin
                ? comparableValues.Min()
                : comparableValues.Max();
        }

        private bool PassGroupFilter(Group group)
        {
            foreach (var filter in _filters)
            {
                if (!string.IsNullOrEmpty(filter.Column))
                {
                    if (group.Aggregates.TryGetValue(filter.Column, out var val))
                    {
                        string strValue = val?.ToString() ?? "";

                        switch (filter.FilterType)
                        {
                            case FilterType.Text:
                                if (!WildcardMatch(strValue, filter.Text))
                                    return false;
                                break;

                            case FilterType.List:
                                if (filter.List == null || !filter.List.Contains(strValue == "" ? null : strValue))
                                    return false;
                                break;

                            case FilterType.Value:
                                if (!ApplyComparison(strValue, filter.Value.Operator, filter.Value.Value, filter.Value.DataType))
                                    return false;
                                break;
                        }
                    }
                }
            }
            return true;
        }

        private IEnumerable GetCurrentItems()
        {
            if (!string.IsNullOrEmpty(_groupProperty))
                return _groups.SelectMany(g => g.Items);
            return _viewList;
        }

        private bool PassAllFilter(object item)
        {
            SkiaSharpControlV2.Diagnostics.GridMetrics.Increment(SkiaSharpControlV2.Diagnostics.GridMetrics.CounterFilterEvaluations);
            foreach (var filter in _filters)
            {
                if (!string.IsNullOrEmpty(filter.Column))
                {
                    (string? Value, Type? Type) value = reflectionHelper.ReadCurrentItemWithTypes(item, filter.Column);

                    // Null/empty property handling: List filters pass if null is in the list; others reject null
                    if (value.Value == null)
                    {
                        if (filter.FilterType == FilterType.List && filter.List != null && filter.List.Contains(null))
                            continue;
                        return false;
                    }

                    object val = value.Value;

                    // Date type conversion: parse once based on filter's target DataType
                    if (filter.FilterType == FilterType.Value)
                    {
                        if (filter.Value.DataType == typeof(DateOnly) && DateTime.TryParse(val?.ToString(), out DateTime dtValue))
                            val = DateOnly.FromDateTime(dtValue);
                        else if (filter.Value.DataType == typeof(TimeSpan) && DateTime.TryParse(val?.ToString(), out DateTime dtValue1))
                            val = dtValue1.TimeOfDay;
                    }

                    string strValue = val?.ToString() ?? "";

                    switch (filter.FilterType)
                    {
                        case FilterType.Text:
                            if (!WildcardMatch(strValue, filter.Text))
                                return false;
                            break;

                        case FilterType.List:
                            if (filter.List == null || !filter.List.Contains(strValue == "" ? null : strValue))
                                return false;
                            break;

                        case FilterType.Value:
                            if (!ApplyComparison(strValue, filter.Value.Operator, filter.Value.Value, filter.Value.DataType))
                                return false;
                            break;
                    }
                }
            }
            return true;
        }

        // B29 fix: cache compiled Regex per pattern (was recompiling on every call)
        private static readonly Dictionary<string, Regex> _regexCache = new();
        private static bool WildcardMatch(string input, string pattern)
        {
            if (!_regexCache.TryGetValue(pattern, out var regex))
            {
                var regexPattern = "^" + Regex.Escape(pattern)
                                                .Replace("\\*", ".*")
                                                .Replace("\\?", ".") + "$";
                regex = new Regex(regexPattern, RegexOptions.Compiled);
                _regexCache[pattern] = regex;
            }
            return regex.IsMatch(input);
        }

        private static bool ApplyComparison(string actualStr, string @operator, string expectedStr, Type valueType)
        {
            // Normalize operator aliases (e.g., "Equals" → "=", "NotEquals" → "<>")
            var op = NormalizeOperator(@operator);

            if (!TryConvert(actualStr, valueType, out var actual) ||
                !TryConvert(expectedStr, valueType, out var expected))
            {
                return op switch
                {
                    "=" => string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase),
                    "<>" => !string.Equals(actualStr, expectedStr, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }

            var comp = Comparer<object>.Default;

            return op switch
            {
                "=" => Equals(actual, expected),
                "<>" => !Equals(actual, expected),
                ">" => comp.Compare(actual, expected) > 0,
                "<" => comp.Compare(actual, expected) < 0,
                ">=" => comp.Compare(actual, expected) >= 0,
                "<=" => comp.Compare(actual, expected) <= 0,
                _ => false
            };
        }

        private static string NormalizeOperator(string op) => op switch
        {
            "Equals" or "equals" or "==" => "=",
            "NotEquals" or "notequals" or "!=" => "<>",
            "GreaterThan" or "greaterthan" => ">",
            "LessThan" or "lessthan" => "<",
            "GreaterThanOrEqual" or "greaterthanorequal" => ">=",
            "LessThanOrEqual" or "lessthanorequal" => "<=",
            _ => op
        };

        private static bool TryConvert(string input, Type type, out object result)
        {
            try
            {
                if (type == typeof(double) && double.TryParse(input, out var d))
                {
                    result = d;
                    return true;
                }
                if (type == typeof(int) && int.TryParse(input, out var i))
                {
                    result = i;
                    return true;
                }
                if (type == typeof(decimal) && decimal.TryParse(input, out var m))
                {
                    result = m;
                    return true;
                }
                if (type == typeof(DateTime) && DateTime.TryParse(input, out var dt))
                {
                    result = dt;
                    return true;
                }
                if (type == typeof(TimeSpan) && TimeSpan.TryParse(input, out var ts))
                {
                    result = ts;
                    return true;
                }
                if (type == typeof(DateOnly) && DateOnly.TryParse(input, out var doVal))
                {
                    result = doVal;
                    return true;
                }
                if (type == typeof(bool) && bool.TryParse(input, out var b))
                {
                    result = b;
                    return true;
                }

                result = Convert.ChangeType(input, type);
                return true;
            }
            catch
            {
                result = null!;
                return false;
            }
        }

        public IEnumerator GetEnumerator()
        {
            return _viewList.GetEnumerator();
        }

        [Obsolete("Manipulate your source ObservableCollection directly. Use source.Move(oldIndex, newIndex) instead.")]
        public bool MoveRowUp(object item)
        {
            if (item == null) return false;

            var index = _viewList.IndexOf(item);
            if (index > 0)
            {
                _viewList.RemoveAt(index);
                _viewList.Insert(index - 1, item);
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                return true;
            }
            return false;
        }

        [Obsolete("Manipulate your source ObservableCollection directly. Use source.Move(oldIndex, newIndex) instead.")]
        public bool MoveRowDown(object item)
        {
            if (item == null) return false;

            var index = _viewList.IndexOf(item);
            if (index >= 0 && index < _viewList.Count - 1)
            {
                _viewList.RemoveAt(index);
                _viewList.Insert(index + 1, item);
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                return true;
            }
            return false;
        }

        [Obsolete("Manipulate your source ObservableCollection directly. Use source.Insert(index, item) instead.")]
        public bool InsertBlankRow(object item, object newObject)
        {
            var index = _viewList.IndexOf(item);
            if (index >= 0 && index <= _viewList.Count - 1) // B40 fix: >= 0 (was > 0, failed at index 0)
            {
                _viewList.Insert(index, newObject);
                AddPropertyChange(newObject); // B19 fix: subscribe to new item property changes
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newObject)); // B2 fix: pass newObject (was item)
                return true;
            }
            return false;
        }

        [Obsolete("Manipulate your source ObservableCollection directly. Use source.Remove(item) instead.")]
        public bool DeleteEmptyRow(object item)
        {
            // B22 fix: return false if item not found, don't fire event
            bool removed = _viewList.Remove(item);
            if (removed)
            {
                RemovePropertyChange(item); // B18 fix: unsubscribe property changes
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
            }
            return removed;
        }
        private bool _disposed;

        // your fields...

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                // Unsubscribe from source collection events
                if (_source is INotifyCollectionChanged notifier)
                {
                    notifier.CollectionChanged -= Source_CollectionChanged;
                }

                // Unsubscribe property change handlers
                foreach (var item in _source)
                {
                    RemovePropertyChange(item);
                }

                // Clear everything
                _viewList.Clear();
                _groups.Clear();
                _filters.Clear();
                _sorts.Clear();
                GroupFields?.Clear();

                // Remove subscribers to our event
                CollectionChanged = null;
            }

            _disposed = true;
        }

        ~CustomCollectionView()
        {
            Dispose(false); // B11 fix: finalizer must call Dispose(false)
        }

        private class SortComparer : IComparer<object>
        {
            private readonly List<(string Prop, ListSortDirection Dir)> _sorts;
            private readonly ReflectionHelper _helper;
            // Per-field cached typed comparison + the runtime type it was resolved for. Resolved
            // lazily on the first Compare (the ctor can't infer the element type — no item yet).
            private readonly Comparison<object>?[] _typed;
            private readonly Type?[] _resolvedFor;

            public SortComparer(List<(string Prop, ListSortDirection Dir)> sorts, ReflectionHelper helper)
            {
                _sorts = sorts;
                _helper = helper;
                _typed = new Comparison<object>?[sorts.Count];
                _resolvedFor = new Type?[sorts.Count];
            }

            public int Compare(object x, object y)
            {
                for (int i = 0; i < _sorts.Count; i++)
                {
                    var (prop, dir) = _sorts[i];

                    // Typed, boxing-free fast path — used ONLY when x and y share a runtime type
                    // (the homogeneous list case, i.e. every realistic grid source). For mixed
                    // runtime types (a shared base property) we fall back to the original
                    // object-getter path so each operand is read via its own type — preserving
                    // exact prior behavior and avoiding an invalid cross-type cast.
                    var tx = x?.GetType();
                    Comparison<object>? cmp = null;
                    if (tx != null && tx == y?.GetType())
                    {
                        if (_resolvedFor[i] != tx)
                        {
                            _typed[i] = _helper.GetTypedComparison(tx, prop);
                            _resolvedFor[i] = tx;
                        }
                        cmp = _typed[i];
                    }

                    int result = cmp != null
                        ? cmp(x!, y!)
                        : Comparer.Default.Compare(_helper.GetPropValue(x!, prop), _helper.GetPropValue(y!, prop));

                    if (result != 0)
                        return dir == ListSortDirection.Ascending ? result : -result;
                }
                return 0;
            }
        }
    }



}
