
using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Helpers;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace SkiaSharpControlV2.Managers
{
    /// <summary>
    /// Manages sorting logic, timer-based periodic re-sort, and column header color state.
    /// Extracted from SkiaGridViewV2 (Step 8 refactoring).
    /// Owns the SortTimer lifecycle (create, start, stop, dispose).
    /// </summary>
    internal class SortingManager : IDisposable
    {
        private readonly DispatcherTimer _sortTimer = new();
        private readonly Func<ICustomCollectionView?> _getCollectionView;
        private readonly Func<SkGridColumnCollection> _getColumns;
        private readonly Func<DataGrid> _getDataListView;
        private readonly Action _resetGroupToggleValues;

        // Callbacks to fire DP-bound events on the control
        private readonly Func<Action<string?, SkGridViewColumnSort?>?> _getSortChanged;
        private readonly Func<Action?> _getSortChanging;
        private readonly Func<System.Windows.Input.ICommand?> _getSortChangedCommand;
        private readonly Func<bool> _getUseCollectionViewSort;

        private bool _isSorting; // re-entrancy guard (B36 fix)

        public SortingManager(
            Func<ICustomCollectionView?> getCollectionView,
            Func<SkGridColumnCollection> getColumns,
            Func<DataGrid> getDataListView,
            Action resetGroupToggleValues,
            Func<Action<string?, SkGridViewColumnSort?>?> getSortChanged,
            Func<Action?> getSortChanging,
            Func<System.Windows.Input.ICommand?>? getSortChangedCommand = null,
            Func<bool>? getUseCollectionViewSort = null)
        {
            _getCollectionView = getCollectionView;
            _getColumns = getColumns;
            _getDataListView = getDataListView;
            _resetGroupToggleValues = resetGroupToggleValues;
            _getSortChanged = getSortChanged;
            _getSortChanging = getSortChanging;
            _getSortChangedCommand = getSortChangedCommand ?? (() => null);
            // Default true preserves current behavior when a caller omits the flag.
            _getUseCollectionViewSort = getUseCollectionViewSort ?? (() => true);
        }

        // ── Core Sort ───────────────────────────────────────────────────

        public void ApplySort(string propertyName, ListSortDirection direction)
        {
            if (_isSorting) return; // B36 fix: prevent double-sort from Column_PropertyChanged re-entrancy
            _isSorting = true;
            try
            {
                using var _metrics = GridMetrics.Measure(GridMetrics.SortApply);
                GridLogger.Log(GridLogger.Sort, $"ApplySort: column={propertyName}, direction={direction}");

                var cv = _getCollectionView();
                cv?.ClearSortDescriptions();
                cv?.AddSort(propertyName, direction);
                _resetGroupToggleValues();
            }
            finally
            {
                _isSorting = false;
            }
        }

        /// <summary>
        /// Remove all sort descriptions from the collection view. The view returns to source-collection
        /// order on the next refresh. ClearSortDescriptions() already calls Refresh() internally, so do
        /// NOT call cv.Refresh() again here — that would rebuild the view twice per click.
        /// Live sort and timer sort naturally become no-ops because the rebuilt _cachedSortComparer is null
        /// when _sorts is empty.
        /// </summary>
        public void ClearSort()
        {
            if (_isSorting) return;
            _isSorting = true;
            try
            {
                using var _metrics = GridMetrics.Measure(GridMetrics.SortApply);
                GridLogger.Log(GridLogger.Sort, "ClearSort");

                var cv = _getCollectionView();
                cv?.ClearSortDescriptions();
                _resetGroupToggleValues();
            }
            finally
            {
                _isSorting = false;
            }
        }

        // ── Sort Click Handler (replaces DataListView_Sorting) ──────────

        public void HandleSortClick(DataGridSortingEventArgs e, int? sortEvery)
        {
            e.Handled = true; // B37 fix: prevent WPF DataGrid from running its own sort pass
            _getSortChanging()?.Invoke();

            var column = e.Column;
            var columns = _getColumns();

            // Locate the SkGridViewColumn matching the WPF column that was clicked
            var col = columns.FirstOrDefault(x => x.Header == column?.Header?.ToString() || x.DisplayHeader == column?.Header?.ToString());
            if (col == null) return;

            // 3-state cycle on the CLICKED column: None → Asc → Desc → None → Asc → ...
            // (Was 2-state Asc ↔ Desc; users could not unset a sort via the header click.)
            var nextDirection = col.GridViewColumnSort switch
            {
                SkGridViewColumnSort.None       => SkGridViewColumnSort.Ascending,
                SkGridViewColumnSort.Ascending  => SkGridViewColumnSort.Descending,
                SkGridViewColumnSort.Descending => SkGridViewColumnSort.None,
                _                               => SkGridViewColumnSort.Ascending
            };

            // Reset timer-sort header color on whichever column is currently the sorted one
            // (before we mutate state). If sortEvery isn't active this is a no-op.
            if (sortEvery.HasValue && sortEvery.Value != 0)
            {
                var oldSortCol = columns.FirstOrDefault(x => x.GridViewColumnSort != SkGridViewColumnSort.None);
                UpdateTimerbaseSortingColumnColor(FilterManager.NORMAL_GRID_COLUMN_COLOR, oldSortCol);
            }

            // Reset every column to None so only one sort is ever active at a time.
            foreach (var item in columns.Where(x => x.GridViewColumnSort != SkGridViewColumnSort.None))
                item.GridViewColumnSort = SkGridViewColumnSort.None;

            // Apply the new state.
            col.GridViewColumnSort = nextDirection;

            if (nextDirection == SkGridViewColumnSort.None)
            {
                // Cycling OFF: clear WPF column glyph (SortArrow trigger fires only on Asc/Desc),
                // drop sort descriptions, and skip timer-sort color (column is no longer active).
                column.SortDirection = null;
                if (_getUseCollectionViewSort())
                    ClearSort();
                // Header color already reset above (oldSortCol path). Nothing to do here.
            }
            else
            {
                column.SortDirection = nextDirection == SkGridViewColumnSort.Ascending
                    ? ListSortDirection.Ascending
                    : ListSortDirection.Descending;

                // Glyph (SortDirection) is set above unconditionally; only the CollectionView sort
                // is gated so glyph-only mode (UseCollectionViewSort=false) shows ▲/▼ without reordering.
                if (_getUseCollectionViewSort())
                    ApplySort(col.BindingPath, column.SortDirection.Value);

                if (sortEvery.HasValue && sortEvery.Value != 0)
                    UpdateTimerbaseSortingColumnColor(FilterManager.TIMERBASED_SORTING_COLOR, col);
            }

            _getSortChanged()?.Invoke(col.Header, nextDirection);
            var cmd = _getSortChangedCommand();
            if (cmd != null && cmd.CanExecute((col.Header, nextDirection)))
                cmd.Execute((col.Header, nextDirection));
        }

        // ── Timer-Based Sort ────────────────────────────────────────────

        public void StartTimer(int seconds)
        {
            _sortTimer.Stop();
            _sortTimer.Interval = TimeSpan.FromSeconds(seconds);
            _sortTimer.Start();
            GridLogger.Log(GridLogger.Sort, $"SortTimer started: every {seconds}s");
        }

        public void StopTimer()
        {
            _sortTimer.Stop();
            GridLogger.Log(GridLogger.Sort, "SortTimer stopped");
        }

        public void SubscribeTimer()
        {
            _sortTimer.Tick += SortTimerChanged;
        }

        public void UnsubscribeTimer()
        {
            _sortTimer.Tick -= SortTimerChanged;
            _sortTimer.Stop();
        }

        private void SortTimerChanged(object? sender, EventArgs e)
        {
            var cv = _getCollectionView();
            if (cv != null)
            {
                cv.Refresh();
                _getSortChanged()?.Invoke(null, null);
            }
        }

        // ── Timer Sort Color Update on SortEvery DP Change ──────────────

        public void HandleSortEveryChanged(int? newValue)
        {
            var columns = _getColumns();
            if (newValue.HasValue && newValue.Value > 0)
            {
                StopTimer();
                _sortTimer.Interval = TimeSpan.FromSeconds(newValue.Value);
                UpdateTimerbaseSortingColumnColor(FilterManager.TIMERBASED_SORTING_COLOR, columns.FirstOrDefault(x => x.GridViewColumnSort != SkGridViewColumnSort.None));
                _sortTimer.Start();
            }
            else
            {
                StopTimer();
                UpdateTimerbaseSortingColumnColor(FilterManager.NORMAL_GRID_COLUMN_COLOR, columns.FirstOrDefault(x => x.GridViewColumnSort != SkGridViewColumnSort.None));
            }
        }

        // ── Column Header Color ─────────────────────────────────────────

        public void UpdateTimerbaseSortingColumnColor(string color, SKGridViewColumn? column)
        {
            if (column == null) return;

            var cv = _getCollectionView();
            var isFilterApplied = cv?.Filters.Any(x => x.Column == column.BindingPath);
            if (isFilterApplied == true && color == FilterManager.NORMAL_GRID_COLUMN_COLOR)
                color = FilterManager.FILTER_COLOR;

            var dataListView = _getDataListView();
            var gridCol = dataListView.Columns
                .Where(x => x.Header != null)
                .FirstOrDefault(x => x.Header?.ToString() == column.Header || x.Header?.ToString() == column.DisplayHeader);

            if (gridCol != null)
            {
                var newStyle = new Style(typeof(DataGridColumnHeader), gridCol.HeaderStyle);
                newStyle.Setters.Add(new Setter(Control.BackgroundProperty, Helper.GetColorBrush(color)));
                gridCol.HeaderStyle = newStyle;
            }
        }

        public void Dispose()
        {
            UnsubscribeTimer();
        }
    }
}
