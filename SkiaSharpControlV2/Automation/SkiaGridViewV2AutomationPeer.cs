using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace SkiaSharpControlV2.Automation
{
    /// <summary>
    /// Top-level automation peer for SkiaGridViewV2.
    /// Exposes the grid as a DataGrid with discoverable rows, cells, columns, headers.
    /// </summary>
    public class SkiaGridViewV2AutomationPeer : FrameworkElementAutomationPeer,
        IGridProvider, ITableProvider, ISelectionProvider, IScrollProvider
    {
        internal readonly AutomationDataProvider _data;
        private Dictionary<int, WeakReference<SkiaGridRowPeer>> _rowPeerCache = new();
        private List<SkiaGridHeaderPeer>? _headerPeerCache;

        // ── StructureChanged coalescing state ───────────────────────────
        // With a UIA listener attached, every StructureChanged triggers a client
        // re-walk of the subtree. The grid's data drain used to raise it 4×/sec
        // (per 250 ms UpdateCollection tick) even when only cell VALUES changed —
        // measured at 21.8 s of a 23 s window spent in UpdateSubtree on a 30k-row
        // book. Now: raise only when the exposed shape (row count, column count,
        // viewport range) actually changed, at most once per coalescing window.
        private DateTime _lastStructureRaiseUtc = DateTime.MinValue;
        private (int Rows, int Cols, int First, int Last, bool Headers) _lastRaisedShape = (-1, -1, -1, -1, false);
        private bool _structureRaisePending;
        private static readonly TimeSpan StructureRaiseWindow = TimeSpan.FromMilliseconds(500);

        /// <summary>Rows exposed above/below the viewport in the peer tree.</summary>
        internal const int ViewportOverscan = 5;

        internal SkiaGridViewV2 Owner => (SkiaGridViewV2)base.Owner;

        public SkiaGridViewV2AutomationPeer(SkiaGridViewV2 owner) : base(owner)
        {
            _data = new AutomationDataProvider(owner);
        }

        // ── AutomationPeer overrides ────────────────────────────────────

        protected override string GetClassNameCore() => "SkiaGridViewV2";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataGrid;
        protected override string GetNameCore() => Owner.Name ?? "SkiaGridView";
        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;

        protected override List<AutomationPeer> GetChildrenCore()
        {
            var children = new List<AutomationPeer>();

            // Kill-switch: when automation is disabled on a grid whose peer already
            // exists (WPF caches peers — they can't be un-created), expose an empty
            // subtree so client walks collapse to O(1).
            if (!Owner.EffectiveAutomationEnabled) return children;

            // Column headers first — but ONLY while the header row is actually shown. Hiding it
            // (ColumnHeaderVisible=false) genuinely removes it from the UI, so leaving Header_N in the
            // tree made "assert the header row is hidden" pass or fail for the wrong reason: the peers
            // stayed at their old rectangle, overlapping Row_0.
            if (Owner.ColumnHeaderVisible)
            {
                EnsureHeaderPeers();
                children.AddRange(_headerPeerCache!);
            }

            // VIRTUALIZED: expose only rows intersecting the viewport (± overscan),
            // not all rows. A 30k-row book previously realized 30k row peers (and
            // ~400k cell peers) per client walk — linear-in-rows menu freezes and
            // a per-walk allocation storm. Off-screen rows remain reachable on
            // demand via IGridProvider.GetItem(row, col) and IScrollItemProvider
            // .ScrollIntoView — same semantics as WPF's own virtualized DataGrid.
            var (firstRow, rowCount) = _data.GetViewportRowRange(ViewportOverscan);
            for (int i = firstRow; i < firstRow + rowCount; i++)
                children.Add(GetOrCreateRowPeer(i));

            // The scroll bars are REAL WPF ScrollBars in the control template
            // (SkiaGridViewV2.xaml:148 / :164) and WPF already gives each one a
            // ScrollBarAutomationPeer for free. But a UIA client can only reach an element by
            // walking GetChildren() from the root, and this method hand-builds that list without
            // ever calling base.GetChildrenCore() — so those peers were valid ORPHANS: they
            // existed, had no parent, and no client could ever see them. Re-parent them here.
            //
            // Deliberately NOT base.GetChildrenCore(): that would walk the whole template
            // (including the header DataGrid) and change the tree shape for every consumer.
            AddScrollBarPeer(children, Owner.VerticalScrollViewer);
            AddScrollBarPeer(children, Owner.HorizontalScrollViewer);

            return children;
        }

        /// <summary>
        /// Adds a template scroll bar's own WPF peer to the tree, but only while the bar is showing —
        /// so it is absent from BOTH the raw and control views when hidden, matching what the user
        /// sees. Auto-visibility is driven by ManageVerticalScrollBar() /
        /// ManageHorizontalScrollBar() in <c>Managers/ScrollManager.cs</c>, so "present only while
        /// showing" needs no extra bookkeeping here.
        /// </summary>
        private static void AddScrollBarPeer(List<AutomationPeer> children, System.Windows.Controls.Primitives.ScrollBar? bar)
        {
            if (bar == null || bar.Visibility != Visibility.Visible) return;
            var peer = UIElementAutomationPeer.CreatePeerForElement(bar);
            if (peer != null) children.Add(peer);
        }

        public override object GetPattern(PatternInterface patternInterface)
        {
            return patternInterface switch
            {
                PatternInterface.Grid => this,
                PatternInterface.Table => this,
                PatternInterface.Selection => this,
                PatternInterface.Scroll => this,
                _ => base.GetPattern(patternInterface)
            };
        }

        // ── Row Peer Cache ──────────────────────────────────────────────

        private SkiaGridRowPeer GetOrCreateRowPeer(int rowIndex)
        {
            if (_rowPeerCache.TryGetValue(rowIndex, out var weakRef) && weakRef.TryGetTarget(out var existing))
                return existing;

            var peer = new SkiaGridRowPeer(this, _data, rowIndex);
            _rowPeerCache[rowIndex] = new WeakReference<SkiaGridRowPeer>(peer);
            return peer;
        }

        /// <summary>
        /// Called when data or column structure changes: drops cached peers and requests
        /// a (coalesced) StructureChanged raise. Cache clearing is cheap and always done;
        /// the event raise is shape-gated + throttled — see NotifyStructureChanged.
        /// </summary>
        internal void InvalidateRowCache()
        {
            _rowPeerCache.Clear();
            _headerPeerCache = null;
            _data.InvalidateColumnCache();
            // Clearing OUR caches is not enough: WPF caches the list this peer last returned from
            // GetChildrenCore() and will keep serving it, so a client walking the tree still sees the
            // old children. ResetChildrenCache() is what makes WPF ask again. Without it, toggling
            // ColumnHeaderVisible left Header_N in the tree even though the gate below already
            // excluded them — it only APPEARED to work when an unrelated layout change happened to
            // refresh the cache as a side effect.
            ResetChildrenCache();
            NotifyStructureChanged(force: false);
        }

        // ── Viewport-scroll settle coalescing (M1 perf fix) ──────────────────────
        //
        // 2.7.8 raised a (shape-gated, 500 ms-coalesced) StructureChanged per vertical
        // scroll tick. Under sustained scrolling the shape changes every tick, so the
        // coalescer re-armed continuously → a ~2 Hz raise train. With ANY session UIA
        // listener attached, each raise arms WPF's post-layout fireAutomationEvents →
        // UpdateSubtree walk: measured per-scroll-call p95 97.7 ms (vs 2.0 ms without
        // listeners) and 62.6% of the dispatcher inside the walk at a 30k-row grid.
        // Fix: never raise mid-scroll. Each viewport move (re)arms a settle timer; the
        // raise fires ONCE ~1 s after scrolling stops, going through the existing
        // shape-gate (scroll-away-and-back → no raise at all). Genuine structure changes
        // (collection membership, columns, grouping) still raise through
        // InvalidateRowCache → NotifyStructureChanged, unchanged. Eventual tree state
        // for event-driven clients is identical — only the mid-scroll raise train is gone.
        private System.Windows.Threading.DispatcherTimer? _viewportSettleTimer;
        private static readonly TimeSpan ViewportSettleDelay = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Vertical scroll moved the viewport — the exposed (virtualized) row range may
        /// have changed. Arms/re-arms the settle timer; the structure notification goes
        /// out once scrolling stops. No-op without listeners or when automation is disabled.
        /// </summary>
        internal void NotifyViewportChanged()
        {
            // Cheap gates first: no UIA client subscribed → skip even the timer bookkeeping.
            if (!ListenerExists(AutomationEvents.StructureChanged)) return;
            if (!Owner.EffectiveAutomationEnabled) return;

            if (_viewportSettleTimer == null)
            {
                _viewportSettleTimer = new System.Windows.Threading.DispatcherTimer(
                    System.Windows.Threading.DispatcherPriority.Background)
                { Interval = ViewportSettleDelay };
                _viewportSettleTimer.Tick += (_, _) =>
                {
                    _viewportSettleTimer!.Stop();
                    NotifyStructureChanged(force: false); // shape-gated: no raise if range unchanged
                };
            }

            // Restart: while scrolling continues, the timer never fires.
            _viewportSettleTimer.Stop();
            _viewportSettleTimer.Start();
        }

        /// <summary>
        /// Coalesced StructureChanged: raises only when the exposed tree shape — row
        /// count, visible column count, viewport row range — actually changed, and at
        /// most once per <see cref="StructureRaiseWindow"/>. In-place cell value churn
        /// (the per-drain-tick case) changes no shape and raises nothing; clients read
        /// fresh values from the live peers on their own cadence. A raise suppressed by
        /// the throttle window is marked pending and goes out on a later call (data
        /// drains arrive 4×/sec, so a pending raise is delivered within ~one window).
        /// RaiseAutomationEvent is itself a no-op when no UIA client is listening.
        /// </summary>
        internal void NotifyStructureChanged(bool force)
        {
            // Listener-free sessions skip the shape compute AND the raise — WPF's
            // RaiseAutomationEvent would no-op anyway, but the shape tuple computation
            // (viewport range + counts) is also pure overhead with nobody listening.
            // Safe: a client that attaches later always walks the tree fresh on attach.
            if (!ListenerExists(AutomationEvents.StructureChanged)) return;
            if (!Owner.EffectiveAutomationEnabled && !force) return;

            var (firstRow, rowCount) = _data.GetViewportRowRange(ViewportOverscan);
            // Headers is part of the shape on purpose: toggling ColumnHeaderVisible adds/removes the
            // Header_N peers but leaves rows and columns untouched, so without it the gate saw "no
            // change" and raised NOTHING — an already-attached client would keep the stale subtree.
            var shape = (Rows: _data.RowCount, Cols: _data.ColumnCount, First: firstRow,
                         Last: firstRow + rowCount - 1, Headers: Owner.ColumnHeaderVisible);
            bool shapeChanged = shape != _lastRaisedShape;

            if (!force && !shapeChanged && !_structureRaisePending) return;

            var now = DateTime.UtcNow;
            if (!force && now - _lastStructureRaiseUtc < StructureRaiseWindow)
            {
                _structureRaisePending = true;
                return;
            }

            _lastStructureRaiseUtc = now;
            _lastRaisedShape = shape;
            _structureRaisePending = false;
            RaiseAutomationEvent(AutomationEvents.StructureChanged);
        }

        // ── Selection-change events ─────────────────────────────────────────────
        //
        // UIA clients (FlaUI/Accessibility Insights) wait on selection events instead of
        // sleeping. We raise, on ACTUAL change only:
        //   * per newly/no-longer-selected row: SelectionItem ElementSelected (single) /
        //     ElementAddedToSelection / ElementRemovedFromSelection on that row's peer, and
        //   * once per change: the grid-level SelectionPatternOnInvalidated (the reliable
        //     "selection changed" wait target).
        // Fired for EVERY selection path — click, keyboard, SelectAll, UIA Select(), VM
        // swap — because the control funnels them all through RaiseSelectionChanged().
        // The last-selected snapshot makes redundant calls (same set) raise nothing.
        private readonly HashSet<object> _lastSelectedItems = new();

        // Cap per-row event fan-out (e.g. Ctrl+A on a huge book) — above it, only the
        // grid-level invalidated event fires. Keeps SelectAll from an event storm.
        private const int PerRowSelectionEventCap = 64;

        /// <summary>
        /// Raise UIA selection events reflecting the grid's current selection. Change-gated
        /// against the last snapshot, so calling it from a path that didn't change selection
        /// is a no-op. Must run on the UI thread (all callers do). Cheap-exits when no UIA
        /// client is listening for the relevant events.
        /// </summary>
        internal void RaiseSelectionChanged()
        {
            if (!Owner.EffectiveAutomationEnabled) { _lastSelectedItems.Clear(); return; }

            // Cheap gate: with NO UIA client listening for any selection event, do nothing
            // (and allocate nothing) — a client that attaches later re-walks the tree and
            // reads live selection via GetSelection(), so no snapshot bookkeeping is needed.
            bool anyListener =
                ListenerExists(AutomationEvents.SelectionPatternOnInvalidated) ||
                ListenerExists(AutomationEvents.SelectionItemPatternOnElementSelected) ||
                ListenerExists(AutomationEvents.SelectionItemPatternOnElementAddedToSelection) ||
                ListenerExists(AutomationEvents.SelectionItemPatternOnElementRemovedFromSelection);
            if (!anyListener) return;

            var current = new HashSet<object>();
            var sel = Owner.SelectedItems;
            if (sel != null)
                foreach (var it in sel)
                    if (it != null) current.Add(it);

            // No net change → nothing to raise (and don't churn the snapshot).
            if (current.SetEquals(_lastSelectedItems)) return;

            bool perRowListeners =
                ListenerExists(AutomationEvents.SelectionItemPatternOnElementSelected) ||
                ListenerExists(AutomationEvents.SelectionItemPatternOnElementAddedToSelection) ||
                ListenerExists(AutomationEvents.SelectionItemPatternOnElementRemovedFromSelection);

            if (perRowListeners)
            {
                var added = current.Where(i => !_lastSelectedItems.Contains(i)).ToList();
                var removed = _lastSelectedItems.Where(i => !current.Contains(i)).ToList();
                if (added.Count + removed.Count <= PerRowSelectionEventCap)
                {
                    // A resulting single-item selection → ElementSelected (UIA's convention
                    // for a single-select change); otherwise per-item added/removed events.
                    bool single = current.Count == 1;
                    foreach (var it in added)
                        TryGetRowPeerForItem(it)?.RaiseSelectionEvent(single
                            ? AutomationEvents.SelectionItemPatternOnElementSelected
                            : AutomationEvents.SelectionItemPatternOnElementAddedToSelection);
                    foreach (var it in removed)
                        TryGetRowPeerForItem(it)?.RaiseSelectionEvent(
                            AutomationEvents.SelectionItemPatternOnElementRemovedFromSelection);
                }
            }

            _lastSelectedItems.Clear();
            foreach (var it in current) _lastSelectedItems.Add(it);

            if (ListenerExists(AutomationEvents.SelectionPatternOnInvalidated))
                RaiseAutomationEvent(AutomationEvents.SelectionPatternOnInvalidated);
        }

        private SkiaGridRowPeer? TryGetRowPeerForItem(object item)
        {
            int idx = _data.GetRowIndexOfItem(item);
            return idx < 0 ? null : GetOrCreateRowPeer(idx);
        }

        internal SkiaGridHeaderPeer? GetHeaderPeer(int colIndex)
        {
            EnsureHeaderPeers();
            return colIndex >= 0 && colIndex < _headerPeerCache!.Count ? _headerPeerCache[colIndex] : null;
        }

        private void EnsureHeaderPeers()
        {
            if (_headerPeerCache != null) return;
            _headerPeerCache = new List<SkiaGridHeaderPeer>();
            var cols = _data.VisibleColumns;
            for (int i = 0; i < cols.Count; i++)
                _headerPeerCache.Add(new SkiaGridHeaderPeer(this, cols[i], i));
        }

        // ── IGridProvider ───────────────────────────────────────────────

        public int RowCount => _data.RowCount;
        public int ColumnCount => _data.ColumnCount;

        public IRawElementProviderSimple GetItem(int row, int column)
        {
            return ProviderFromPeer(new SkiaGridCellPeer(this, _data, row, column));
        }

        // ── ITableProvider ──────────────────────────────────────────────

        public IRawElementProviderSimple[] GetRowHeaders()
            => Array.Empty<IRawElementProviderSimple>();

        public IRawElementProviderSimple[] GetColumnHeaders()
        {
            // Same gate as GetChildrenCore — this reads the same cache, so without it a client that
            // asked the table provider still got headers the user cannot see.
            if (!Owner.ColumnHeaderVisible) return Array.Empty<IRawElementProviderSimple>();
            EnsureHeaderPeers();
            return _headerPeerCache!.Select(h => ProviderFromPeer(h)).ToArray();
        }

        public RowOrColumnMajor RowOrColumnMajor => RowOrColumnMajor.RowMajor;

        // ── ISelectionProvider ──────────────────────────────────────────

        public IRawElementProviderSimple[] GetSelection()
        {
            if (!Owner.EffectiveAutomationEnabled)
                return Array.Empty<IRawElementProviderSimple>();

            var selectedItems = Owner.SelectedItems;
            if (selectedItems == null || selectedItems.Count == 0)
                return Array.Empty<IRawElementProviderSimple>();

            var result = new List<IRawElementProviderSimple>();
            for (int i = 0; i < _data.RowCount; i++)
            {
                if (_data.IsRowSelected(i))
                    result.Add(ProviderFromPeer(GetOrCreateRowPeer(i)));
            }
            return result.ToArray();
        }

        public bool CanSelectMultiple => true;
        public bool IsSelectionRequired => false;

        // ── IScrollProvider ─────────────────────────────────────────────

        public double HorizontalScrollPercent
        {
            get
            {
                var max = _data.TotalWidth - _data.ViewportWidth;
                return max > 0 ? (_data.ScrollOffsetX / max) * 100 : -1;
            }
        }

        public double VerticalScrollPercent
        {
            get
            {
                var max = _data.TotalHeight - _data.ViewportHeight;
                return max > 0 ? (_data.ScrollOffsetY / max) * 100 : -1;
            }
        }

        public double HorizontalViewSize
        {
            get
            {
                var total = _data.TotalWidth;
                return total > 0 ? (_data.ViewportWidth / total) * 100 : 100;
            }
        }

        public double VerticalViewSize
        {
            get
            {
                var total = _data.TotalHeight;
                return total > 0 ? (_data.ViewportHeight / total) * 100 : 100;
            }
        }

        public bool HorizontallyScrollable => _data.TotalWidth > _data.ViewportWidth;
        public bool VerticallyScrollable => _data.TotalHeight > _data.ViewportHeight;

        public void SetScrollPercent(double horizontalPercent, double verticalPercent)
        {
            if (horizontalPercent >= 0 && horizontalPercent <= 100)
            {
                var maxH = _data.TotalWidth - _data.ViewportWidth;
                if (maxH > 0)
                    Owner.ScrollToHorizontalOffset(horizontalPercent / 100 * maxH);
            }
            if (verticalPercent >= 0 && verticalPercent <= 100)
            {
                var maxV = _data.TotalHeight - _data.ViewportHeight;
                if (maxV > 0)
                    Owner.ScrollToVerticalOffset(verticalPercent / 100 * maxV);
            }
        }

        public void Scroll(ScrollAmount horizontalAmount, ScrollAmount verticalAmount)
        {
            var rowHeight = _data.RowHeight;
            var viewH = _data.ViewportHeight;

            if (verticalAmount == ScrollAmount.SmallIncrement)
                Owner.ScrollToVerticalOffset(_data.ScrollOffsetY + rowHeight);
            else if (verticalAmount == ScrollAmount.SmallDecrement)
                Owner.ScrollToVerticalOffset(Math.Max(0, _data.ScrollOffsetY - rowHeight));
            else if (verticalAmount == ScrollAmount.LargeIncrement)
                Owner.ScrollToVerticalOffset(_data.ScrollOffsetY + viewH);
            else if (verticalAmount == ScrollAmount.LargeDecrement)
                Owner.ScrollToVerticalOffset(Math.Max(0, _data.ScrollOffsetY - viewH));
        }
    }
}
