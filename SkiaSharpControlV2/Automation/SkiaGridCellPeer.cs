using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace SkiaSharpControlV2.Automation
{
    /// <summary>
    /// Automation peer for a single cell in SkiaGridViewV2.
    /// Exposes cell value via IValueProvider, grid position via IGridItemProvider,
    /// and buttons as children via IInvokeProvider.
    /// </summary>
    public class SkiaGridCellPeer : SkiaGridItemPeerBase, IValueProvider, IGridItemProvider, ITableItemProvider
    {
        private readonly SkiaGridViewV2AutomationPeer _gridPeer;
        private readonly AutomationDataProvider _data;
        private readonly int _rowIndex;
        private readonly int _colIndex;

        internal SkiaGridCellPeer(SkiaGridViewV2AutomationPeer gridPeer, AutomationDataProvider data, int rowIndex, int colIndex)
        {
            _gridPeer = gridPeer;
            _data = data;
            _rowIndex = rowIndex;
            _colIndex = colIndex;
        }

        // ── AutomationPeer overrides ────────────────────────────────────

        protected override string GetClassNameCore() => "SkiaGridCell";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;
        protected override string GetNameCore() => _data.GetCellValue(_rowIndex, _colIndex);
        protected override string GetAutomationIdCore() => $"Cell_{_rowIndex}_{_colIndex}";
        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;

        /// <summary>
        /// Expose the cell's live BG/FG/Border colors via the standard UIA ItemStatus
        /// property — same format as P1's AutomationHelper:
        ///   "BG=#AARRGGBB;FG=#AARRGGBB;BR=#AARRGGBB"
        /// Border component is omitted when the cell has no border color resolved.
        /// Colors are computed by replaying the full 6-step style cascade on demand.
        /// </summary>
        protected override string GetItemStatusCore() => _data.GetCellItemStatus(_rowIndex, _colIndex);

        protected override List<AutomationPeer>? GetChildrenCore()
        {
            // Expose buttons as children
            var buttons = _data.GetCellButtons(_rowIndex, _colIndex);
            if (buttons == null || buttons.Count == 0) return null;

            var children = new List<AutomationPeer>();
            foreach (var btn in buttons)
            {
                children.Add(new SkiaGridButtonPeer(_gridPeer, _data, btn, _rowIndex, _colIndex));
            }
            return children;
        }

        protected override Rect GetBoundingRectangleCore() => _data.GetCellBounds(_rowIndex, _colIndex);

        // Off-screen check uses LOCAL canvas coordinates — see SkiaGridRowPeer.IsOffscreenCore
        // for the rationale (the previous implementation mixed screen-Y with local
        // ViewportHeight, hiding rows from automation when the window was not maximized).
        protected override bool IsOffscreenCore() => _data.IsRowOffscreen(_rowIndex);

        protected override object? GetPatternInternal(PatternInterface patternInterface)
        {
            return patternInterface switch
            {
                PatternInterface.Value => this,
                PatternInterface.GridItem => this,
                PatternInterface.TableItem => this,
                _ => null
            };
        }

        // ── IValueProvider ──────────────────────────────────────────────

        public string Value => _data.GetCellValue(_rowIndex, _colIndex);
        public bool IsReadOnly => true;
        public void SetValue(string value) => throw new InvalidOperationException("Grid is read-only");

        // ── IGridItemProvider ───────────────────────────────────────────

        public int Row => _rowIndex;
        public int Column => _colIndex;
        public int RowSpan => 1;
        public int ColumnSpan => 1;
        public IRawElementProviderSimple ContainingGrid => ProviderFromPeer(_gridPeer);

        // ── ITableItemProvider ──────────────────────────────────────────

        public IRawElementProviderSimple[] GetRowHeaderItems() => Array.Empty<IRawElementProviderSimple>();

        public IRawElementProviderSimple[] GetColumnHeaderItems()
        {
            var headerPeer = _gridPeer.GetHeaderPeer(_colIndex);
            return headerPeer != null
                ? new[] { ProviderFromPeer(headerPeer) }
                : Array.Empty<IRawElementProviderSimple>();
        }
    }
}
