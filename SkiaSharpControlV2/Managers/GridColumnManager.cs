
using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Helpers;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SkiaSharpControlV2.Managers
{
    /// <summary>
    /// Manages column sync between SKGridViewColumn model and WPF DataGrid columns.
    /// Handles resize monitoring, reorder sync, and Column_PropertyChanged forwarding.
    /// Extracted from SkiaGridViewV2 (Step 13 refactoring).
    /// </summary>
    internal class GridColumnManager
    {
        private readonly SkiaGridViewV2 _control;

        public GridColumnManager(SkiaGridViewV2 control)
        {
            _control = control;
        }

        /// <summary>Treat an empty / whitespace color string the same as unset.</summary>
        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        // ── Update Columns In DataGrid ──────────────────────────────────

        public void UpdateColumnsInDataGrid()
        {
            using var _metrics = GridMetrics.Measure(GridMetrics.ColumnUpdate);
            GridLogger.Log(GridLogger.Column, $"UpdateColumnsInDataGrid, columns={_control.Columns?.Count ?? 0}");

            if (_control.Columns == null || _control.Columns.Count == 0) return;

            _control.DataListView.Columns.Clear();
            var sortColumns = _control.Columns.Where(x => x?.GridViewColumnSort != null && x?.GridViewColumnSort != SkGridViewColumnSort.None).LastOrDefault();

            foreach (var column in _control.Columns.OrderBy(x => x.DisplayIndex))
            {
                var headerStyle = new Style(typeof(DataGridColumnHeader));
                headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,
                    column.ContentAlignment == CellContentAlignment.Right ? HorizontalAlignment.Right :
                    column.ContentAlignment == CellContentAlignment.Left ? HorizontalAlignment.Left : HorizontalAlignment.Center));
                // Background precedence: per-column BackColor → grid ColumnHeaderBackground → built-in default.
                headerStyle.Setters.Add(new Setter(Control.BackgroundProperty,
                    Helper.GetColorBrush(column.BackColor
                        ?? NullIfBlank(_control.ColumnHeaderBackground)
                        ?? FilterManager.NORMAL_GRID_COLUMN_COLOR)));
                // Foreground / separator: only override when the consumer set one, so an unset DP
                // leaves the ColumnHeaderStyle resource's own value in charge.
                if (NullIfBlank(_control.ColumnHeaderForeground) is { } fg)
                    headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, Helper.GetColorBrush(fg)));
                if (NullIfBlank(_control.ColumnHeaderSeparatorColor) is { } sep)
                    headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Helper.GetColorBrush(sep)));
                headerStyle.BasedOn = _control.Resources["ColumnHeaderStyle"] as Style;

                var dgColumn = new DataGridTextColumn
                {
                    Header = column.DisplayHeader ?? column.Header,
                    Width = column.Width,
                    Visibility = !column.IsVisible ? Visibility.Collapsed : Visibility.Visible,
                    HeaderStyle = headerStyle,
                    MinWidth = 40,
                };

                if (column.CanUserResize.HasValue) dgColumn.CanUserResize = column.CanUserResize.Value;
                if (column.CanUserReorder.HasValue) dgColumn.CanUserReorder = column.CanUserReorder.Value;
                if (column.CanUserSort.HasValue) dgColumn.CanUserSort = column.CanUserSort.Value;
                _control.DataListView.Columns.Add(dgColumn);
            }

            // Apply DisplayIndex after all columns added
            foreach (var column in _control.Columns)
            {
                if (column.DisplayIndex != null)
                {
                    var dgCol = _control.DataListView.Columns.FirstOrDefault(x => x.Header as string == (column.DisplayHeader ?? column.Header));
                    if (dgCol != null && column.DisplayIndex.Value >= 0 && column.DisplayIndex.Value < _control.DataListView.Columns.Count)
                        dgCol.DisplayIndex = column.DisplayIndex.Value;
                }
            }

            // Sync DisplayIndex back from DataGrid
            foreach (var item in _control.DataListView.Columns)
            {
                var col1 = _control.Columns.FirstOrDefault(x => x.Header == item.Header);
                if (col1 != null)
                {
                    UnSubscribeColumnEvent(col1);
                    col1.DisplayIndex = item.DisplayIndex;
                    SubscribeColumnEvent(col1);
                }
            }

            // Re-apply sort
            if (sortColumns != null)
            {
                _control.DataListView.Columns.Where(x => x.Header as string == (sortColumns.DisplayHeader ?? sortColumns.Header)).FirstOrDefault()!
                    .SortDirection = sortColumns.GridViewColumnSort == SkGridViewColumnSort.Ascending ? ListSortDirection.Ascending
                                   : (sortColumns.GridViewColumnSort == SkGridViewColumnSort.Descending ? ListSortDirection.Descending : null);
                // Glyph (SortDirection) set above; gate only the CollectionView sort (glyph-only mode).
                if (_control.UseCollectionViewSort)
                    _control._sortingManager.ApplySort(sortColumns.BindingPath, sortColumns.GridViewColumnSort == SkGridViewColumnSort.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending);
                if (_control.SortEvery.HasValue && _control.SortEvery.Value > 0)
                    _control._sortingManager.UpdateTimerbaseSortingColumnColor(FilterManager.TIMERBASED_SORTING_COLOR, _control.Columns.FirstOrDefault(x => x.GridViewColumnSort != SkGridViewColumnSort.None));
            }

            SubscribeToColumnEvents(_control.Columns);
            MonitorColumnResize(_control.DataListView);

            // The visible-column set (membership + widths + visibility) was rebuilt — drop the width cache.
            _control.InvalidateVisibleColumnsWidthCache();

            // The column set was (re)built — WPF will realize new header visuals on the
            // next layout pass. Flag the header-click handlers for (re)attachment then.
            _control.MarkHeadersDirty();
        }

        // ── Column Property Changed ─────────────────────────────────────

        public void Column_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not SKGridViewColumn column) return;

            // Sort is handled FIRST, before the col==null bail below. Applying a sort only
            // needs the CollectionView + BindingPath — not the WPF DataGridColumn. A
            // binding-set GridViewColumnSort (e.g. restored/persisted column settings) can
            // resolve BEFORE the WPF header column is realized; the old code dropped it at
            // the col==null return, so a default sort passed at window-open never applied.
            if (e.PropertyName == nameof(SKGridViewColumn.GridViewColumnSort))
            {
                ApplyColumnSort(column);
                return;
            }

            DataGridColumn? col = _control.DataListView.Columns.FirstOrDefault(x =>
                x.Header?.ToString() == column?.Header?.ToString() ||
                (column?.DisplayHeader != null && x.Header?.ToString() == column?.DisplayHeader?.ToString()));

            if (col == null) return;

            if (e.PropertyName == nameof(SKGridViewColumn.IsVisible))
            {
                col.Visibility = !column.IsVisible ? Visibility.Collapsed : Visibility.Visible;
                _control.InvalidateVisibleColumnsWidthCache(); // visible set changed
                _control._scrollManager.UpdateScrollValues();
                _control.UpdateSkiaGrid();
                // The set of visible columns changed — refresh the automation peer cache so
                // the newly-shown / hidden column's header peer (and its AutomationId)
                // enters / leaves the UIA tree without requiring the window to reopen.
                _control.InvalidateAutomationCache();
                // Header visuals change with visibility — re-hook click handlers next layout.
                _control.MarkHeadersDirty();
            }
            // B1 fix: CanUserReorder now correctly sets CanUserReorder (was swapped)
            if (e.PropertyName == nameof(SKGridViewColumn.CanUserReorder))
                col.CanUserReorder = column!.CanUserReorder!.Value;
            if (e.PropertyName == nameof(SKGridViewColumn.CanUserResize))
                col.CanUserResize = column!.CanUserResize!.Value;
            if (e.PropertyName == nameof(SKGridViewColumn.CanUserSort))
                col.CanUserSort = column!.CanUserSort!.Value;
            if (e.PropertyName == nameof(SKGridViewColumn.Width))
            {
                MonitorColumnResizeRemove(col);
                col.Width = new DataGridLength(column.Width);
                _control.InvalidateVisibleColumnsWidthCache(); // column width changed
                _control._scrollManager.UpdateScrollValues();
                _control.UpdateSkiaGrid();
                MonitorColumnResizeAdd(col);
                _control.Refresh();
            }
            if (e.PropertyName == nameof(SKGridViewColumn.BackColor))
            {
                var newStyle = new Style(typeof(DataGridColumnHeader), col.HeaderStyle);
                newStyle.Setters.Add(new Setter(Control.BackgroundProperty,
                    Helper.GetColorBrush(column.BackColor
                        ?? NullIfBlank(_control.ColumnHeaderBackground)
                        ?? FilterManager.NORMAL_GRID_COLUMN_COLOR)));
                col.HeaderStyle = newStyle;
            }
            if (e.PropertyName == nameof(SKGridViewColumn.DisplayIndex))
            {
                _control.DataListView.ColumnReordered -= _control.DataListView_ColumnReordered!;
                if (column.DisplayIndex.HasValue) col.DisplayIndex = column.DisplayIndex.Value;
                else column.DisplayIndex = col.DisplayIndex;
                _control.DataListView.ColumnReordered += _control.DataListView_ColumnReordered!;
                // Column order changed — UIA children ordering changes too. Refresh the cache.
                _control.InvalidateAutomationCache();
                // Reorder can regenerate header visuals — re-hook click handlers next layout.
                _control.MarkHeadersDirty();
            }
            _control.SkiaRenderer.UpdateVisibleColumns();
        }

        /// <summary>
        /// Apply a column's GridViewColumnSort to the CollectionView. Independent of the
        /// WPF DataGridColumn so it works even before headers are realized (binding-set /
        /// restored sort). The header sort glyph is set best-effort when the WPF column
        /// exists. Only acts on Ascending/Descending — matches the prior behavior that a
        /// programmatic None does not clear an existing sort (header-click handles None
        /// via SortingManager.ClearSort).
        /// </summary>
        private void ApplyColumnSort(SKGridViewColumn column)
        {
            if (column.GridViewColumnSort == SkGridViewColumnSort.None) return;
            if (string.IsNullOrEmpty(column.BindingPath)) return;

            var dir = column.GridViewColumnSort == SkGridViewColumnSort.Ascending
                ? ListSortDirection.Ascending
                : ListSortDirection.Descending;

            // Gate the CollectionView sort; the header glyph below is set regardless so a
            // binding-set / restored GridViewColumnSort still shows ▲/▼ in glyph-only mode.
            if (_control.UseCollectionViewSort)
                _control._sortingManager.ApplySort(column.BindingPath, dir);

            // Best-effort header glyph — the WPF column may not be realized yet.
            var col = _control.DataListView.Columns.FirstOrDefault(x =>
                x.Header?.ToString() == column.Header?.ToString() ||
                (column.DisplayHeader != null && x.Header?.ToString() == column.DisplayHeader));
            if (col != null) col.SortDirection = dir;

            _control._groupingManager.ResetGroupToggleValues();
            _control._groupingManager.ResetRowToggleValues();
            _control.SkiaRenderer?.UpdateVisibleColumns();
            _control.SkiaCanvas?.InvalidateVisual();
        }

        // ── Column Resize Monitoring ────────────────────────────────────

        public void MonitorColumnResize(DataGrid dataGrid)
        {
            foreach (var column in dataGrid.Columns)
            {
                var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.ActualWidthProperty, typeof(DataGridColumn));
                descriptor?.RemoveValueChanged(column, OnColumnWidthChanged!);
                descriptor?.AddValueChanged(column, OnColumnWidthChanged!);
            }
        }

        private void MonitorColumnResizeRemove(DataGridColumn? column)
        {
            var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.ActualWidthProperty, typeof(DataGridColumn));
            descriptor?.RemoveValueChanged(column, OnColumnWidthChanged!);
        }

        private void MonitorColumnResizeAdd(DataGridColumn? column)
        {
            var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.ActualWidthProperty, typeof(DataGridColumn));
            descriptor?.AddValueChanged(column, OnColumnWidthChanged!);
        }

        private void OnColumnWidthChanged(object sender, EventArgs e)
        {
            try
            {
                if (sender is DataGridColumn dgCol)
                {
                    var col = _control.Columns?.FirstOrDefault(x =>
                        x.Header == dgCol.Header?.ToString() || x.DisplayHeader == dgCol.Header?.ToString());
                    if (col != null)
                    {
                        UnSubscribeColumnEvent(col);
                        col.Width = dgCol.ActualWidth;
                        SubscribeColumnEvent(col);
                    }
                    // Drag-resize: the model Width was set with the column event unsubscribed, so the
                    // Width branch above did NOT run — invalidate the width cache here directly.
                    _control.InvalidateVisibleColumnsWidthCache();
                    _control._scrollManager.UpdateScrollValues();
                    _control.UpdateSkiaGrid();
                    _control.SkiaRenderer.UpdateVisibleColumns();
                    _control.Refresh();
                }
            }
            catch { }
        }

        // ── Column Event Subscription ───────────────────────────────────

        public void SubscribeToColumnEvents(IEnumerable<SKGridViewColumn> columns)
        {
            foreach (var col in columns)
            {
                UnSubscribeColumnEvent(col);
                SubscribeColumnEvent(col);
            }
        }

        public void UnSubscribeColumnEvent(SKGridViewColumn col)
        {
            col.PropertyChanged -= Column_PropertyChanged;
        }

        public void SubscribeColumnEvent(SKGridViewColumn col)
        {
            col.PropertyChanged += Column_PropertyChanged;
        }
    }
}
