
using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SkiaSharpControlV2.Managers
{
    /// <summary>
    /// Manages scroll offset tracking, viewport calculation, auto-visibility, and scroll bar events.
    /// Extracted from SkiaGridViewV2 (Step 11 refactoring).
    /// </summary>
    internal class ScrollManager
    {
        private readonly Func<ScrollBar> _getHScrollBar;
        private readonly Func<ScrollBar> _getVScrollBar;
        private readonly Func<Grid> _getMainGrid;
        private readonly Func<double> _getRowAreaHeight;
        private readonly Func<double> _getVisibleColumnsWidth;
        private readonly Func<int> _getTotalRows;
        private readonly Func<float> _getRowHeight;
        private readonly Func<ScrollViewer?> _getDataListViewScroll;
        private readonly Func<SKScrollBarVisibility> _getHScrollBarVisibility;
        private readonly Func<SKScrollBarVisibility> _getVScrollBarVisibility;
        private readonly Func<bool> _getIsDeferredScrolling;
        private readonly Func<Action<double>?> _getHScrollPositionChanged;
        private readonly Func<Action<double>?> _getVScrollPositionChanged;
        private readonly Func<Action<bool>?> _getVScrollVisibilityChanged;
        private readonly Action _invalidateVisual;

        public float ScrollOffsetX { get; set; }
        public float ScrollOffsetY { get; set; }
        private bool _isBusy;
        private bool _inUpdateScrollValues;
        private float _tempScrollOffsetX;
        private float _tempScrollOffsetY;

        public ScrollManager(
            Func<ScrollBar> getHScrollBar,
            Func<ScrollBar> getVScrollBar,
            Func<Grid> getMainGrid,
            Func<double> getRowAreaHeight,
            Func<double> getVisibleColumnsWidth,
            Func<int> getTotalRows,
            Func<float> getRowHeight,
            Func<ScrollViewer?> getDataListViewScroll,
            Func<SKScrollBarVisibility> getHScrollBarVisibility,
            Func<SKScrollBarVisibility> getVScrollBarVisibility,
            Func<bool> getIsDeferredScrolling,
            Func<Action<double>?> getHScrollPositionChanged,
            Func<Action<double>?> getVScrollPositionChanged,
            Func<Action<bool>?> getVScrollVisibilityChanged,
            Action invalidateVisual)
        {
            _getHScrollBar = getHScrollBar;
            _getVScrollBar = getVScrollBar;
            _getMainGrid = getMainGrid;
            _getRowAreaHeight = getRowAreaHeight;
            _getVisibleColumnsWidth = getVisibleColumnsWidth;
            _getTotalRows = getTotalRows;
            _getRowHeight = getRowHeight;
            _getDataListViewScroll = getDataListViewScroll;
            _getHScrollBarVisibility = getHScrollBarVisibility;
            _getVScrollBarVisibility = getVScrollBarVisibility;
            _getIsDeferredScrolling = getIsDeferredScrolling;
            _getHScrollPositionChanged = getHScrollPositionChanged;
            _getVScrollPositionChanged = getVScrollPositionChanged;
            _getVScrollVisibilityChanged = getVScrollVisibilityChanged;
            _invalidateVisual = invalidateVisual;
        }

        // ── Scroll Value Calculation ────────────────────────────────────

        /// <summary>
        /// Height of the band the rows actually render into — the middle grid row, i.e.
        /// <c>skiaContainer.ActualHeight</c>, which is the same value <c>GetSkiaHeight()</c> renders
        /// against.
        /// <para>
        /// This used to be <c>MainGrid.ActualHeight</c>, which ALSO spans the column-header row and
        /// the horizontal scroll bar row — so the vertical bar measured its viewport as up to ~38 px
        /// taller than the area the rows occupy, under-reporting overflow and leaving the last rows
        /// unreachable. A <c>totalRows + 3.3</c> fudge compensated by inflating the CONTENT side by
        /// ~3.3 rows (~46 px at the 14 px Compact row) instead of fixing the viewport, and that
        /// over-corrected: the bar went visible before there was any real overflow, reserving an
        /// empty gutter with a full-length thumb. Both are gone — the two sides are now measured
        /// against the same band.
        /// </para>
        /// Falls back to the whole grid's height while the row band has not been laid out yet (0),
        /// which keeps the old behavior for that transient.
        /// </summary>
        private double RowAreaHeight()
        {
            var rowArea = _getRowAreaHeight();
            return rowArea > 0 ? rowArea : _getMainGrid().ActualHeight;
        }

        /// <summary>
        /// Width the vertical bar occupies right now — safe to call in the SAME pass that just made
        /// it visible.
        /// <para>
        /// <c>ActualWidth</c> is only filled in by a layout pass, so a bar switched from Collapsed to
        /// Visible still reports 0. Sizing the canvas against that 0 leaves the canvas a full
        /// bar-width too wide, and the bar then draws ON TOP of the last column. Fall back to the
        /// explicit <c>Width</c> when the consumer set one (via <c>VerticalScrollBarWidth</c>),
        /// otherwise to the system metric that WPF's default ScrollBar style would use.
        /// </para>
        /// </summary>
        internal static double EffectiveVerticalBarWidth(ScrollBar? bar)
        {
            if (bar == null || bar.Visibility != Visibility.Visible) return 0;
            if (bar.ActualWidth > 0) return bar.ActualWidth;
            if (!double.IsNaN(bar.Width) && bar.Width > 0) return bar.Width;
            return SystemParameters.VerticalScrollBarWidth;
        }

        /// <summary>
        /// Recomputes both scroll bars' range and auto-visibility.
        /// <para>
        /// RE-ENTRANT-SAFE. Setting <c>SKGridViewColumn.Width</c> synchronously calls back into this
        /// method (<c>Managers/GridColumnManager.cs</c>, the Width branch), so a consumer that resizes
        /// columns from <c>VerticalScrollBarVisibilityChanged</c> would otherwise trigger one full
        /// recalculation per column. The nested calls are dropped; the OUTER pass finishes with the new
        /// widths, because the column-width cache was invalidated before it reads
        /// <c>_getVisibleColumnsWidth()</c> below.
        /// </para>
        /// </summary>
        public void UpdateScrollValues()
        {
            if (_inUpdateScrollValues) return;
            _inUpdateScrollValues = true;
            try
            {
                UpdateScrollValuesCore();
            }
            finally
            {
                _inUpdateScrollValues = false;
            }
        }

        private void UpdateScrollValuesCore()
        {
            var vScroll = _getVScrollBar();
            var hScroll = _getHScrollBar();
            var mainGrid = _getMainGrid();
            float rowHeight = _getRowHeight();
            int totalRows = _getTotalRows();
            double rowAreaHeight = RowAreaHeight();

            vScroll.Minimum = 0;
            vScroll.ViewportSize = Math.Max(0, rowAreaHeight);
            vScroll.Maximum = Math.Max(0, (totalRows * rowHeight) - rowAreaHeight);
            ManageVerticalScrollBar();

            // Clamp at 0. Before the first layout pass ActualWidth is 0 while
            // EffectiveVerticalBarWidth() already reports the bar's real width, so the subtraction
            // goes negative — and ScrollBar.ViewportSize REJECTS a negative value with an
            // ArgumentException rather than coercing it, which took the whole app down at startup.
            var vScrollWidth = EffectiveVerticalBarWidth(vScroll);
            var contentWidth = Math.Max(0, mainGrid.ActualWidth - vScrollWidth);
            hScroll.Minimum = 0;
            hScroll.ViewportSize = contentWidth;
            hScroll.Maximum = Math.Max(0, _getVisibleColumnsWidth() - contentWidth);
            ManageHorizontalScrollBar();
        }

        // ── Auto-Visibility ─────────────────────────────────────────────

        private void ManageVerticalScrollBar()
        {
            if (_getVScrollBarVisibility() == SKScrollBarVisibility.Auto)
            {
                var vScroll = _getVScrollBar();
                float rowHeight = _getRowHeight();
                int totalRows = _getTotalRows();

                // Same measure as Maximum above: real rows against the real row band. No phantom rows.
                var wanted = ((totalRows * rowHeight) - RowAreaHeight()) > 0
                    ? Visibility.Visible : Visibility.Collapsed;

                // Only on a REAL change — this runs on every data tick, and a callback per tick would
                // be useless noise. Raised BEFORE the caller sizes the canvas (every path does scroll
                // values first, then UpdateSkiaGrid), so a consumer can resize columns here and have
                // the canvas picked up in the same pass instead of a frame later.
                if (vScroll.Visibility != wanted)
                {
                    vScroll.Visibility = wanted;
                    _getVScrollVisibilityChanged()?.Invoke(wanted == Visibility.Visible);
                }
            }
        }

        private void ManageHorizontalScrollBar()
        {
            if (_getHScrollBarVisibility() == SKScrollBarVisibility.Auto)
            {
                var hScroll = _getHScrollBar();
                var mainGrid = _getMainGrid();
                var vScroll = _getVScrollBar();
                var vScrollWidth = EffectiveVerticalBarWidth(vScroll);
                var contentWidth = Math.Max(0, mainGrid.ActualWidth - vScrollWidth);

                hScroll.Visibility = (_getVisibleColumnsWidth() - contentWidth) > 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ── Horizontal Scroll Events ────────────────────────────────────

        public void HandleHorizontalValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _tempScrollOffsetX = (float)e.NewValue;
            if (!_getIsDeferredScrolling())
                UpdateHorizontalScroll();
        }

        public void HandleHorizontalPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_getIsDeferredScrolling())
                UpdateHorizontalScroll();
        }

        public void HandleHorizontalScroll(object sender, ScrollEventArgs e)
        {
            if (!_getIsDeferredScrolling())
                UpdateHorizontalScroll();
        }

        public void UpdateHorizontalScroll()
        {
            using var _metrics = SkiaSharpControlV2.Diagnostics.GridMetrics.Measure(SkiaSharpControlV2.Diagnostics.GridMetrics.ScrollUpdate);
            if (_isBusy) return;
            _isBusy = true;
            ScrollOffsetX = _tempScrollOffsetX;
            _getDataListViewScroll()?.ScrollToHorizontalOffset(ScrollOffsetX);
            _getHScrollPositionChanged()?.Invoke(ScrollOffsetX);
            _isBusy = false;
            _invalidateVisual();
        }

        // ── Vertical Scroll Events ──────────────────────────────────────

        public void HandleVerticalValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _tempScrollOffsetY = (float)e.NewValue;
            if (!_getIsDeferredScrolling())
                UpdateVerticalScroll();
        }

        public void HandleVerticalPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_getIsDeferredScrolling())
                UpdateVerticalScroll();
        }

        public void HandleVerticalScroll(object sender, ScrollEventArgs e)
        {
            if (!_getIsDeferredScrolling())
                UpdateVerticalScroll();
        }

        public void UpdateVerticalScroll()
        {
            using var _metrics = SkiaSharpControlV2.Diagnostics.GridMetrics.Measure(SkiaSharpControlV2.Diagnostics.GridMetrics.ScrollUpdate);
            if (_isBusy) return;
            _isBusy = true;
            ScrollOffsetY = _tempScrollOffsetY;
            _getVScrollPositionChanged()?.Invoke(ScrollOffsetY);
            _isBusy = false;
            _invalidateVisual();
        }

        // ── Public API ──────────────────────────────────────────────────

        public void ScrollToVerticalOffset(double offset)
        {
            var vScroll = _getVScrollBar();
            vScroll.Value = offset;
            UpdateVerticalScroll();
        }

        public void ScrollToHorizontalOffset(double offset)
        {
            var hScroll = _getHScrollBar();
            hScroll.Value = offset;
            UpdateHorizontalScroll();
        }
    }
}
