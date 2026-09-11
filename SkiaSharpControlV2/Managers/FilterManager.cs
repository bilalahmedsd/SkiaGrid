

using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SkiaSharpControlV2.Managers
{
    /// <summary>
    /// Manages filter apply/remove logic and column header styling.
    /// Extracted from SkiaGridViewV2 AddOrUpdateFilter/RemoveFilter (Step 7 refactoring).
    /// </summary>
    internal class FilterManager
    {
        public const string FILTER_COLOR = "#0072C6";
        /// <summary>
        /// Default (unfiltered, unsorted) column-header background. #343434 matches the reference
        /// trading terminal; it was #3F3F3F before v2.14.0. A grid can restore any value via the
        /// <c>ColumnHeaderBackground</c> DP, and a single column still wins via
        /// <see cref="SKGridViewColumn.BackColor"/>.
        /// </summary>
        public const string NORMAL_GRID_COLUMN_COLOR = "#FF343434";
        public const string TIMERBASED_SORTING_COLOR = "#008040";

        private readonly Func<ICustomCollectionView?> _getCollectionView;
        private readonly Func<SkGridColumnCollection> _getColumns;
        private readonly Func<DataGrid> _getDataListView;
        private readonly Action _scrollToTop;
        private readonly Func<int?> _getSortEvery;
        private readonly Action<string, SKGridViewColumn?> _updateTimerSortColor;
        private readonly Func<string?>? _getHeaderBackground;

        public FilterManager(
            Func<ICustomCollectionView?> getCollectionView,
            Func<SkGridColumnCollection> getColumns,
            Func<DataGrid> getDataListView,
            Action scrollToTop,
            Func<int?> getSortEvery,
            Action<string, SKGridViewColumn?> updateTimerSortColor,
            Func<string?>? getHeaderBackground = null)
        {
            _getCollectionView = getCollectionView;
            _getColumns = getColumns;
            _getDataListView = getDataListView;
            _scrollToTop = scrollToTop;
            _getSortEvery = getSortEvery;
            _updateTimerSortColor = updateTimerSortColor;
            _getHeaderBackground = getHeaderBackground;
        }

        /// <summary>
        /// Background a header reverts to when its filter is cleared: the grid's
        /// <c>ColumnHeaderBackground</c> when set, else <see cref="NORMAL_GRID_COLUMN_COLOR"/>.
        /// </summary>
        private string UnfilteredHeaderBackground()
        {
            var custom = _getHeaderBackground?.Invoke();
            return string.IsNullOrWhiteSpace(custom) ? NORMAL_GRID_COLUMN_COLOR : custom!;
        }

        public void AddOrUpdateFilter(Filter filter)
        {
            using var _metrics = GridMetrics.Measure(GridMetrics.FilterApply);
            GridLogger.Log(GridLogger.Filter, $"AddOrUpdateFilter: column={filter.Column}, type={filter.FilterType}");

            var collectionView = _getCollectionView();
            if (collectionView == null) return;

            if (collectionView.AddOrUpdateFilter(filter))
            {
                var columns = _getColumns();
                var col = columns.FirstOrDefault(x => x.BindingPath == filter.Column);
                if (col != null)
                {
                    var dataListView = _getDataListView();
                    var gridCol = dataListView.Columns
                        .Where(x => x.Header != null)
                        .FirstOrDefault(x => x.Header?.ToString() == col.Header || x.Header?.ToString() == col.DisplayHeader);

                    col.DisplayHeader = string.Format("{0} {1}", col.Header, filter.ToString());
                    if (gridCol != null)
                    {
                        gridCol.Header = string.Format("{0} {1}", col.Header, filter.ToString());
                        var newStyle = new Style(typeof(DataGridColumnHeader), gridCol.HeaderStyle);
                        newStyle.Setters.Add(new Setter(Control.BackgroundProperty, Helper.GetColorBrush(FILTER_COLOR)));
                        gridCol.HeaderStyle = newStyle;
                    }
                }

                var sortEvery = _getSortEvery();
                if (sortEvery.HasValue && sortEvery.Value > 0)
                    _updateTimerSortColor(TIMERBASED_SORTING_COLOR, columns.FirstOrDefault(x => x.GridViewColumnSort != SkGridViewColumnSort.None));

                _scrollToTop();
            }
        }

        /// <summary>Filter by exact value comparison (e.g., Price > 100).</summary>
        public void AddValueFilter(string column, string @operator, string value, Type dataType)
        {
            AddOrUpdateFilter(new Filter
            {
                Column = column,
                FilterType = FilterType.Value,
                Value = (@operator, value, dataType)
            });
        }

        /// <summary>Filter by wildcard text match (e.g., Name matches "ACCT*").</summary>
        public void AddTextFilter(string column, string text)
        {
            AddOrUpdateFilter(new Filter
            {
                Column = column,
                FilterType = FilterType.Text,
                Text = text
            });
        }

        /// <summary>Filter by list of allowed values (e.g., Status in ["Open", "Filled"]).</summary>
        public void AddListFilter(string column, List<string> list)
        {
            AddOrUpdateFilter(new Filter
            {
                Column = column,
                FilterType = FilterType.List,
                List = list
            });
        }

        /// <summary>Remove any active filter on the specified column.</summary>
        public void RemoveFilter(string column)
        {
            var filter = _getCollectionView()?.Filters.FirstOrDefault(x => x.Column == column);
            if (filter != null)
                RemoveFilter(filter);
        }

        public void RemoveFilter(Filter filter)
        {
            GridLogger.Log(GridLogger.Filter, $"RemoveFilter: column={filter.Column}");

            var collectionView = _getCollectionView();
            if (collectionView == null) return;

            if (collectionView.RemoveFilter(filter))
            {
                var columns = _getColumns();
                var col = columns.FirstOrDefault(x => x.BindingPath == filter.Column);
                if (col != null)
                {
                    var dataListView = _getDataListView();
                    var gridCol = dataListView.Columns
                        .Where(x => x.Header != null)
                        .FirstOrDefault(x => x.Header?.ToString() == col.Header || x.Header?.ToString() == col.DisplayHeader);

                    col.DisplayHeader = null;
                    if (gridCol != null)
                    {
                        gridCol.Header = col.Header;
                        var newStyle = new Style(typeof(DataGridColumnHeader), gridCol.HeaderStyle);
                        newStyle.Setters.Add(new Setter(Control.BackgroundProperty, Helper.GetColorBrush(UnfilteredHeaderBackground())));
                        gridCol.HeaderStyle = newStyle;
                    }
                }

                var sortEvery = _getSortEvery();
                if (sortEvery.HasValue && sortEvery.Value > 0)
                    _updateTimerSortColor(TIMERBASED_SORTING_COLOR, columns.FirstOrDefault(x => x.GridViewColumnSort != SkGridViewColumnSort.None));
            }
        }
    }
}
