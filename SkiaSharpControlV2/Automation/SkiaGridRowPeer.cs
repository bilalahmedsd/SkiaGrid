using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace SkiaSharpControlV2.Automation
{
    /// <summary>
    /// Automation peer for a row in SkiaGridViewV2.
    /// Exposes selection, scroll-into-view, and expand/collapse (for group headers).
    /// </summary>
    public class SkiaGridRowPeer : SkiaGridItemPeerBase, ISelectionItemProvider, IScrollItemProvider, IExpandCollapseProvider, IGridItemProvider, IInvokeProvider
    {
        private readonly SkiaGridViewV2AutomationPeer _gridPeer;
        private readonly AutomationDataProvider _data;
        private readonly int _rowIndex;

        internal SkiaGridRowPeer(SkiaGridViewV2AutomationPeer gridPeer, AutomationDataProvider data, int rowIndex)
        {
            _gridPeer = gridPeer;
            _data = data;
            _rowIndex = rowIndex;
        }

        internal int RowIndex => _rowIndex;

        // ── AutomationPeer overrides ────────────────────────────────────

        protected override string GetClassNameCore() => "SkiaGridRow";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;

        protected override string GetNameCore()
        {
            if (_data.IsGroupHeader(_rowIndex))
                return $"Group: {_data.GetGroupName(_rowIndex)}";
            var val = _data.ColumnCount > 0 ? _data.GetCellValue(_rowIndex, 0) : "";
            return $"Row {_rowIndex}: {val}";
        }

        protected override string GetAutomationIdCore() => $"Row_{_rowIndex}";
        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;

        /// <summary>
        /// Expose the row's live BG/FG/Border colors via the standard UIA ItemStatus
        /// property — same format as P1's AutomationHelper:
        ///   "BG=#AARRGGBB;FG=#AARRGGBB;BR=#AARRGGBB"
        /// Computed from RowTemplate setters + triggers only (cell-level cascade is
        /// surfaced per-cell on SkiaGridCellPeer).
        /// </summary>
        protected override string GetItemStatusCore() => _data.GetRowItemStatus(_rowIndex);

        // Cell peers cached for this row peer's lifetime. Previously every walk
        // allocated fresh SkiaGridCellPeer objects per column — on a 30k-row tree
        // that was ~400k allocations per client walk (+500 MB / 4 min working-set
        // climb measured with a UIA listener attached). Row peers themselves are
        // dropped wholesale by the grid peer's InvalidateRowCache on data/column
        // changes, so this cache dies with the row peer — no separate invalidation.
        // The column-count check covers any path that bypassed the invalidation.
        // Note: peers read data LIVE per query (GetCellValue at ask-time), so a
        // cached peer object never serves stale values.
        private List<AutomationPeer>? _cellPeerCache;

        protected override List<AutomationPeer>? GetChildrenCore()
        {
            int colCount = _data.ColumnCount;
            if (_cellPeerCache != null && _cellPeerCache.Count == colCount)
                return _cellPeerCache;

            var children = new List<AutomationPeer>(colCount);
            for (int col = 0; col < colCount; col++)
                children.Add(new SkiaGridCellPeer(_gridPeer, _data, _rowIndex, col));
            _cellPeerCache = children;
            return children;
        }

        protected override Rect GetBoundingRectangleCore() => _data.GetRowBounds(_rowIndex);

        // Off-screen check uses LOCAL canvas coordinates (see AutomationDataProvider.IsRowOffscreen).
        // The previous version mixed screen-Y from GetBoundingRectangle with local ViewportHeight,
        // which caused all rows except row 0 to be reported off-screen whenever the window was not
        // at screen origin (normal/floating window state). Accessibility Insights then filtered
        // those rows out and the user could only reach one row from automation.
        protected override bool IsOffscreenCore() => _data.IsRowOffscreen(_rowIndex);

        protected override object? GetPatternInternal(PatternInterface patternInterface)
        {
            return patternInterface switch
            {
                PatternInterface.SelectionItem => this,
                PatternInterface.ScrollItem => this,
                PatternInterface.GridItem => this,
                PatternInterface.ExpandCollapse => _data.IsGroupHeader(_rowIndex) ? this : null,
                // Invoke opens the row's context menu — data rows only (group headers use
                // ExpandCollapse). No-op when the grid has no row context menu wired.
                PatternInterface.Invoke => _data.IsGroupHeader(_rowIndex) ? null : this,
                _ => null
            };
        }

        /// <summary>
        /// Run a state-mutating UIA provider call on the control's UI thread. UIA calls may
        /// arrive on a UIA/RPC worker thread; the selection + WPF touches below must run on
        /// the Dispatcher. <see cref="System.Windows.Threading.Dispatcher.Invoke(Action)"/>
        /// is synchronous, so the provider call returns only after the control state (and the
        /// TwoWay-bound SelectedItems) has actually updated — exactly like a real click.
        /// </summary>
        private void InvokeOnUi(Action action)
        {
            var dispatcher = _gridPeer.Owner.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }

        /// <summary>Raise a UIA event on this row peer (called by the grid peer's diff).</summary>
        internal void RaiseSelectionEvent(AutomationEvents ev) => RaiseAutomationEvent(ev);

        // ── ISelectionItemProvider ──────────────────────────────────────

        public bool IsSelected => _data.IsRowSelected(_rowIndex);
        public IRawElementProviderSimple SelectionContainer => ProviderFromPeer(_gridPeer);

        // Route through the SAME path a user left-click uses (SelectionManager +
        // SelectedItems DP + SelectionChangedCommand), synchronously on the UI thread, so
        // the bound VM selection updates and dependent ICommand.CanExecute re-queries.
        // The grid raises ElementSelected/AddedToSelection/RemovedFromSelection from that
        // path (see SkiaGridViewV2AutomationPeer.RaiseSelectionChanged).
        public void Select() => InvokeOnUi(() => _gridPeer.Owner.SelectRowFromAutomation(_rowIndex));

        public void AddToSelection() => InvokeOnUi(() => _gridPeer.Owner.AddRowToSelectionFromAutomation(_rowIndex));

        public void RemoveFromSelection() => InvokeOnUi(() => _gridPeer.Owner.RemoveRowFromSelectionFromAutomation(_rowIndex));

        // ── IInvokeProvider (open the row's context menu) ───────────────

        public void Invoke() => InvokeOnUi(() => _gridPeer.Owner.OpenRowContextMenuFromAutomation(_rowIndex));

        // ── IScrollItemProvider ─────────────────────────────────────────

        public void ScrollIntoView()
        {
            var offset = _rowIndex * _data.RowHeight;
            InvokeOnUi(() => _gridPeer.Owner.ScrollToVerticalOffset(offset));
        }

        // ── IExpandCollapseProvider (group headers only) ────────────────

        public ExpandCollapseState ExpandCollapseState
        {
            get
            {
                if (!_data.IsGroupHeader(_rowIndex))
                    return ExpandCollapseState.LeafNode;
                return _data.IsGroupExpanded(_rowIndex)
                    ? ExpandCollapseState.Expanded
                    : ExpandCollapseState.Collapsed;
            }
        }

        public void Expand() => InvokeOnUi(() => SetGroupExpanded(true));

        public void Collapse() => InvokeOnUi(() => SetGroupExpanded(false));

        private void SetGroupExpanded(bool expanded)
        {
            if (!_data.IsGroupHeader(_rowIndex)) return;
            var name = _data.GetGroupName(_rowIndex) ?? "";
            var control = _gridPeer.Owner;
            if (control.GroupToggleDetails.ContainsKey(name))
            {
                var details = control.GroupToggleDetails[name];
                control.GroupToggleDetails[name] = (expanded, details.x, details.y, details.height, details.width);
            }
        }

        // ── IGridItemProvider ───────────────────────────────────────────

        public int Row => _rowIndex;
        public int Column => 0;
        public int RowSpan => 1;
        public int ColumnSpan => _data.ColumnCount;
        public IRawElementProviderSimple ContainingGrid => ProviderFromPeer(_gridPeer);
    }
}
