using System.Collections;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SkiaSharpControlV2.Input
{
    /// <summary>
    /// Row drag-and-drop state machine (2.18.0).
    ///
    /// Rows are drawn pixels, not WPF visuals, so WPF's own DragDrop framework has nothing to attach
    /// to and none of the built-in adorner / drop-target plumbing applies. Tracking is therefore
    /// done by hand on the canvas: arm on left-button-down, promote to a real drag once the pointer
    /// passes the system drag threshold, follow MouseMove, finish on MouseLeftButtonUp.
    ///
    /// Everything here is inert while <c>RowDragMode</c> is <see cref="SKRowDragMode.None"/> (the
    /// default): <see cref="Arm"/> returns immediately, so a grid that has not opted in pays one
    /// enum comparison per click and nothing per frame.
    ///
    /// The index math is deliberately in static methods (<see cref="GapIndexFromContentY"/>,
    /// <see cref="MoveBlock"/>) so it is unit-testable without a WPF dispatcher or an STA thread.
    /// </summary>
    internal class RowDragController
    {
        // Distance in px from the top/bottom edge of the row band that starts auto-scrolling.
        private const double AutoScrollZone = 14;
        private const int AutoScrollIntervalMs = 60;

        private readonly Func<SKRowDragMode> _getMode;
        private readonly Func<List<object>> _getRowItems;
        private readonly Func<double> _getRowHeight;
        private readonly Func<double> _getScrollOffsetY;
        private readonly Func<double> _getViewportHeight;
        private readonly Func<IEnumerable?> _getSelectedItems;
        private readonly Func<object, bool> _isDraggable;
        private readonly Action<int> _scrollRows;
        private readonly Action _invalidate;
        private readonly Func<SKRowDragStartingEventArgs, bool> _raiseStarting;
        private readonly Action<SKRowDroppedEventArgs> _raiseDropped;
        private readonly Action<int, List<int>?> _publishVisual;
        private readonly Func<bool> _isLeftButtonDown;
        private readonly Action<List<object>, int, SKGridViewColumn?> _beginExternalDrag;

        private DispatcherTimer? _autoScrollTimer;
        private int _autoScrollDirection;

        private IInputElement? _capturedElement;
        private Point _armPoint;
        private Point _lastPoint;
        private int _armRowIndex = -1;
        private SKGridViewColumn? _armColumn;
        private bool _armed;
        private List<object> _dragItems = new();

        public RowDragController(
            Func<SKRowDragMode> getMode,
            Func<List<object>> getRowItems,
            Func<double> getRowHeight,
            Func<double> getScrollOffsetY,
            Func<double> getViewportHeight,
            Func<IEnumerable?> getSelectedItems,
            Func<object, bool> isDraggable,
            Action<int> scrollRows,
            Action invalidate,
            Func<SKRowDragStartingEventArgs, bool> raiseStarting,
            Action<SKRowDroppedEventArgs> raiseDropped,
            Action<int, List<int>?> publishVisual,
            Action<List<object>, int, SKGridViewColumn?>? beginExternalDrag = null,
            Func<bool>? isLeftButtonDown = null)
        {
            _getMode = getMode;
            _getRowItems = getRowItems;
            _getRowHeight = getRowHeight;
            _getScrollOffsetY = getScrollOffsetY;
            _getViewportHeight = getViewportHeight;
            _getSelectedItems = getSelectedItems;
            _isDraggable = isDraggable;
            _scrollRows = scrollRows;
            _invalidate = invalidate;
            _raiseStarting = raiseStarting;
            _raiseDropped = raiseDropped;
            _publishVisual = publishVisual;
            // Injectable purely so a test can drive a whole drag: there is no physically pressed
            // mouse button in a test run, and Mouse.LeftButton is a global read with no seam.
            _isLeftButtonDown = isLeftButtonDown ?? (() => Mouse.LeftButton == MouseButtonState.Pressed);
            _beginExternalDrag = beginExternalDrag ?? ((_, _, _) => { });
        }

        /// <summary>True once the drag threshold was passed and the indicator is live.</summary>
        public bool IsDragging { get; private set; }

        /// <summary>Gap index the indicator currently sits at, or -1 when not dragging.</summary>
        public int InsertIndex { get; private set; } = -1;

        /// <summary>
        /// Left-button-down on the canvas: remember where, so a later MouseMove can decide whether
        /// this is a click or a drag. Called AFTER selection has been updated by MouseHandler, so
        /// the pressed row is already part of <c>SelectedItems</c> — which is what lets a
        /// multi-row selection drag as a block.
        /// </summary>
        public void Arm(IInputElement canvas, Point point, int rowIndex, SKGridViewColumn? column = null)
        {
            if (_getMode() == SKRowDragMode.None) return;

            var rows = _getRowItems();
            if (rowIndex < 0 || rowIndex >= rows.Count) return;
            if (!_isDraggable(rows[rowIndex])) return;

            _capturedElement = canvas;
            _armPoint = point;
            _armRowIndex = rowIndex;
            _armColumn = column;
            _armed = true;
        }

        /// <summary>
        /// MouseMove on the canvas. Returns true when the move was consumed by a drag, so the
        /// caller can skip hover / tooltip work (both are meaningless mid-drag and the tooltip
        /// popup would steal the mouse).
        /// </summary>
        public bool Update(Point point)
        {
            if (_getMode() == SKRowDragMode.None) return false;

            if (!IsDragging)
            {
                if (!_armed) return false;
                if (!_isLeftButtonDown())
                {
                    // Button released outside our handlers (e.g. over another window) — disarm.
                    _armed = false;
                    return false;
                }
                if (!PassedThreshold(_armPoint, point)) return false;

                // DragOut is a completely different shape of gesture: WPF's DoDragDrop runs its own
                // nested message loop and owns the cursor, the drop feedback and the delivery. We
                // must NOT capture the mouse, draw an indicator, or auto-scroll — we hand the
                // gesture over and are done.
                if (_getMode() == SKRowDragMode.DragOut) return BeginExternal();

                if (!Begin()) return false;
            }

            Track(point);
            return true;
        }

        /// <summary>MouseLeftButtonUp on the canvas — commit the drop. Returns true if a drag ended.</summary>
        public bool Complete(Point point)
        {
            _armed = false;
            if (!IsDragging) return false;

            Track(point);
            int gap = InsertIndex;
            var items = _dragItems;

            EndTracking();

            var rows = _getRowItems();
            object? target = gap >= 0 && gap < rows.Count ? rows[gap] : null;
            // The target must not be one of the dragged rows — dropping a block onto itself is a
            // no-op, and passing a dragged row as the target would make MoveBlock chase its own
            // tail. Walk down to the first row that is staying put.
            while (target != null && items.Contains(target))
            {
                gap++;
                target = gap < rows.Count ? rows[gap] : null;
            }

            // Every trailing row can be part of the block, in which case the walk above ran off the
            // end — keep the reported gap inside its documented 0..RowCount range.
            var args = new SKRowDroppedEventArgs(items, Math.Min(gap, rows.Count), target);
            _raiseDropped(args);
            return true;
        }

        /// <summary>Escape, lost capture, or the grid being torn down — abandon without dropping.</summary>
        public void Cancel()
        {
            _armed = false;
            if (!IsDragging) return;
            EndTracking();
        }

        // ── internals ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Hand the gesture to WPF drag-and-drop. Returns true so the caller treats the move as
        /// consumed; no tracking state is kept, because DoDragDrop blocks until the drop is over and
        /// by then our own notion of "dragging" would be stale anyway.
        /// </summary>
        private bool BeginExternal()
        {
            var rows = _getRowItems();
            if (_armRowIndex < 0 || _armRowIndex >= rows.Count) { _armed = false; return false; }

            var items = BuildDragSet(rows, _armRowIndex, _getSelectedItems(), _isDraggable);
            int rowIndex = _armRowIndex;
            var column = _armColumn;

            // Disarm FIRST: DoDragDrop does not return until the user lets go, and any state left
            // armed here would still be armed on the far side of that.
            _armed = false;
            if (items.Count == 0) return false;

            _beginExternalDrag(items, rowIndex, column);
            return true;
        }

        private bool Begin()
        {
            var rows = _getRowItems();
            if (_armRowIndex < 0 || _armRowIndex >= rows.Count) { _armed = false; return false; }

            _dragItems = BuildDragSet(rows, _armRowIndex, _getSelectedItems(), _isDraggable);
            if (_dragItems.Count == 0) { _armed = false; return false; }

            // Capture is taken only AFTER this event returns, deliberately: a handler is allowed
            // to start a WPF DragDrop.DoDragDrop from here for a cross-window drop, and that pumps
            // its own nested message loop which cannot run while we hold the mouse. Such a handler
            // must also set Cancel = true, so we do not start tracking a drag it has already run.
            var starting = new SKRowDragStartingEventArgs(_dragItems, _armRowIndex);
            if (!_raiseStarting(starting)) { _armed = false; return false; }

            IsDragging = true;
            _armed = false;
            _capturedElement?.CaptureMouse();
            return true;
        }

        private void Track(Point point)
        {
            _lastPoint = point;
            var rowHeight = _getRowHeight();
            if (rowHeight <= 0) return;

            var rows = _getRowItems();
            int gap = GapIndexFromContentY(point.Y + _getScrollOffsetY(), rowHeight, rows.Count);

            int direction = AutoScrollDirection(point.Y, _getViewportHeight());
            SetAutoScroll(direction);

            if (gap == InsertIndex) return;
            InsertIndex = gap;
            PublishVisual(rows);
            _invalidate();
        }

        private void PublishVisual(List<object> rows)
        {
            List<int>? indexes = null;
            if (_dragItems.Count > 0)
            {
                indexes = new List<int>(_dragItems.Count);
                for (int i = 0; i < rows.Count; i++)
                    if (_dragItems.Contains(rows[i])) indexes.Add(i);
            }
            _publishVisual(InsertIndex, indexes);
        }

        private void EndTracking()
        {
            IsDragging = false;
            InsertIndex = -1;
            SetAutoScroll(0);
            _publishVisual(-1, null);
            if (_capturedElement != null && Mouse.Captured == _capturedElement)
                _capturedElement.ReleaseMouseCapture();
            _invalidate();
        }

        private void SetAutoScroll(int direction)
        {
            if (direction == _autoScrollDirection) return;
            _autoScrollDirection = direction;

            if (direction == 0)
            {
                _autoScrollTimer?.Stop();
                return;
            }

            _autoScrollTimer ??= CreateAutoScrollTimer();
            _autoScrollTimer.Start();
        }

        private DispatcherTimer CreateAutoScrollTimer()
        {
            var timer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(AutoScrollIntervalMs)
            };
            timer.Tick += (_, _) =>
            {
                if (!IsDragging || _autoScrollDirection == 0) { _autoScrollTimer?.Stop(); return; }
                _scrollRows(_autoScrollDirection);
                // The pointer has not moved, but the content under it has — re-evaluate the gap so
                // the indicator keeps following the pointer while the grid scrolls beneath it.
                Track(_lastPoint);
            };
            return timer;
        }

        /// <summary>Stop and drop the auto-scroll timer. Called from the grid's Dispose.</summary>
        public void Teardown()
        {
            Cancel();
            _autoScrollTimer?.Stop();
            _autoScrollTimer = null;
            _capturedElement = null;
        }

        // ── pure helpers (unit-tested directly, no WPF state) ───────────────────────

        internal static bool PassedThreshold(Point from, Point to)
            => Math.Abs(to.X - from.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(to.Y - from.Y) >= SystemParameters.MinimumVerticalDragDistance;

        /// <summary>
        /// Nearest GAP for a content-space Y. 0 = above the first row, <paramref name="rowCount"/> =
        /// below the last. Rounding to the nearest boundary (rather than truncating to a row index)
        /// is what makes the indicator follow the half of the row the pointer is in.
        /// </summary>
        internal static int GapIndexFromContentY(double contentY, double rowHeight, int rowCount)
        {
            if (rowHeight <= 0 || rowCount <= 0) return 0;
            int gap = (int)Math.Round(contentY / rowHeight, MidpointRounding.AwayFromZero);
            return Math.Clamp(gap, 0, rowCount);
        }

        /// <summary>
        /// -1 to scroll up, +1 to scroll down, 0 to stop — from the pointer's position inside the
        /// row band. Held near an edge the pointer stops producing MouseMove events, which is why
        /// the actual scrolling is driven by a timer off this value.
        /// </summary>
        internal static int AutoScrollDirection(double pointerY, double viewportHeight)
        {
            if (viewportHeight <= AutoScrollZone * 2) return 0;
            if (pointerY < AutoScrollZone) return -1;
            if (pointerY > viewportHeight - AutoScrollZone) return 1;
            return 0;
        }

        /// <summary>
        /// The rows a press on <paramref name="rowIndex"/> should drag: the whole selection when the
        /// pressed row is part of a multi-row selection, otherwise just that row. Order is view
        /// order, and undraggable rows (group headers, tree children) are filtered out.
        /// </summary>
        internal static List<object> BuildDragSet(List<object> rows, int rowIndex, IEnumerable? selected, Func<object, bool> isDraggable)
        {
            var pressed = rows[rowIndex];
            if (!isDraggable(pressed)) return new List<object>();

            var selectedSet = new HashSet<object>();
            if (selected != null)
                foreach (var s in selected)
                    if (s != null) selectedSet.Add(s);

            if (selectedSet.Count < 2 || !selectedSet.Contains(pressed))
                return new List<object> { pressed };

            var block = new List<object>();
            foreach (var row in rows)
                if (selectedSet.Contains(row) && isDraggable(row)) block.Add(row);

            return block.Count > 0 ? block : new List<object> { pressed };
        }

        /// <summary>
        /// Move <paramref name="dragged"/> so it sits immediately above <paramref name="target"/>
        /// in <paramref name="source"/> (or at the end when target is null), preserving the block's
        /// relative order. Returns true if the list changed.
        ///
        /// Expressed against the TARGET ITEM rather than an index because a view index is not a
        /// source index once a filter is active — but the item under the indicator always is.
        /// Prefers ObservableCollection&lt;T&gt;.Move (one Move notification, so the collection view
        /// rebuilds once) and falls back to Remove+Insert for a plain IList.
        /// </summary>
        internal static bool MoveBlock(IList source, IReadOnlyList<object> dragged, object? target)
        {
            if (source == null || dragged == null || dragged.Count == 0) return false;
            if (source.IsFixedSize || source.IsReadOnly) return false;

            // Single-row fast path: an ObservableCollection Move raises one notification.
            if (dragged.Count == 1)
            {
                int from = source.IndexOf(dragged[0]);
                if (from < 0) return false;
                int to = target != null ? source.IndexOf(target) : source.Count;
                if (to < 0) to = source.Count;
                // Removing the item first shifts everything after it down one.
                if (to > from) to--;
                if (to == from) return false;
                if (TryObservableMove(source, from, to)) return true;
                source.RemoveAt(from);
                source.Insert(Math.Clamp(to, 0, source.Count), dragged[0]);
                return true;
            }

            var present = new List<object>(dragged.Count);
            foreach (var item in dragged)
                if (source.Contains(item)) present.Add(item);
            if (present.Count == 0) return false;

            foreach (var item in present)
                source.Remove(item);

            int insertAt = target != null ? source.IndexOf(target) : source.Count;
            if (insertAt < 0) insertAt = source.Count;
            insertAt = Math.Clamp(insertAt, 0, source.Count);

            for (int i = 0; i < present.Count; i++)
                source.Insert(insertAt + i, present[i]);

            return true;
        }

        private static bool TryObservableMove(IList source, int from, int to)
        {
            // ObservableCollection<T>.Move is not on any non-generic interface, and the closed
            // generic type is unknown here — reflect once per drop (not per frame).
            var move = source.GetType().GetMethod("Move", new[] { typeof(int), typeof(int) });
            if (move == null) return false;
            move.Invoke(source, new object[] { from, to });
            return true;
        }
    }
}
