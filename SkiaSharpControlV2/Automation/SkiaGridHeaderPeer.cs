using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace SkiaSharpControlV2.Automation
{
    /// <summary>
    /// Automation peer for a column header in SkiaGridViewV2.
    /// Exposes header text via IValueProvider and sort trigger via IInvokeProvider.
    /// </summary>
    public class SkiaGridHeaderPeer : SkiaGridItemPeerBase, IValueProvider, IInvokeProvider
    {
        private readonly SkiaGridViewV2AutomationPeer _gridPeer;
        private readonly SKGridViewColumn _column;
        private readonly int _colIndex;

        internal SkiaGridHeaderPeer(SkiaGridViewV2AutomationPeer gridPeer, SKGridViewColumn column, int colIndex)
        {
            _gridPeer = gridPeer;
            _column = column;
            _colIndex = colIndex;
        }

        // ── AutomationPeer overrides ────────────────────────────────────

        protected override string GetClassNameCore() => "SkiaGridHeader";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.HeaderItem;
        protected override string GetNameCore() => _column.DisplayHeader ?? _column.Header ?? $"Column {_colIndex}";
        protected override string GetAutomationIdCore() => $"Header_{_colIndex}";
        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
        protected override List<AutomationPeer>? GetChildrenCore() => null;
        // Real screen-coordinate bounds so AI can locate the header by hovering. Without
        // this, AI hit-tests fall back to the parent grid and the user sees "datagrid 'X'"
        // when hovering a header.
        protected override Rect GetBoundingRectangleCore() => _gridPeer._data.GetColumnHeaderBounds(_colIndex);
        protected override bool IsOffscreenCore() => false;

        protected override object? GetPatternInternal(PatternInterface patternInterface)
        {
            return patternInterface switch
            {
                PatternInterface.Value => this,
                PatternInterface.Invoke => _column.CanUserSort != false ? this : null,
                _ => null
            };
        }

        // ── IValueProvider ──────────────────────────────────────────────

        public string Value => _column.DisplayHeader ?? _column.Header ?? "";
        public bool IsReadOnly => true;
        public void SetValue(string value) => throw new InvalidOperationException("Header is read-only");

        // ── IInvokeProvider (triggers sort) ─────────────────────────────

        public void Invoke()
        {
            if (_column.CanUserSort == false) return;
            // Mirror HandleSortClick's 3-state cycle so screen readers and Inspect-driven
            // sort invocations behave identically to mouse clicks: None → Asc → Desc → None.
            _column.GridViewColumnSort = _column.GridViewColumnSort switch
            {
                SkGridViewColumnSort.None       => SkGridViewColumnSort.Ascending,
                SkGridViewColumnSort.Ascending  => SkGridViewColumnSort.Descending,
                SkGridViewColumnSort.Descending => SkGridViewColumnSort.None,
                _                               => SkGridViewColumnSort.Ascending
            };
        }
    }
}
