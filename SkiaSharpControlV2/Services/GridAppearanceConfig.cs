using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Renderer;

namespace SkiaSharpControlV2.Services
{
    /// <summary>
    /// Centralized holder for font, color, and grid line configuration.
    /// DependencyProperty callbacks on SkiaGridViewV2 update this config,
    /// which then pushes changes to the SkiaRenderer.
    /// Other managers read RowHeight from here instead of the control field.
    /// Extracted from R14 (Font & Appearance Properties).
    /// </summary>
    internal class GridAppearanceConfig
    {
        private readonly SkiaRenderer _renderer;

        public GridAppearanceConfig(SkiaRenderer renderer)
        {
            _renderer = renderer;
        }

        // ── Metric Padding (font size → row / header height) ────────────
        //
        // Heights are always DERIVED from the font size so a grid keeps its density when the font
        // changes. Density picks which padding pair applies; nothing here is a fixed pixel height.

        /// <summary>
        /// Padding added to the font size for a <see cref="SKGridDensity.Normal"/> data row.
        /// 6 px = the reference terminal's 17 px row at an 11 px font.
        /// </summary>
        internal const float RowHeightPadding = 6f;

        /// <summary>
        /// Padding added to the font size for a <see cref="SKGridDensity.Normal"/> column header.
        /// 12 px = the reference terminal's 23 px header at an 11 px font.
        /// </summary>
        internal const float ColumnHeaderHeightPadding = 12f;

        /// <summary>
        /// Padding for a <see cref="SKGridDensity.Compact"/> data row — 3 px, i.e. the reference
        /// terminal's 14 px time-and-sales row at an 11 px font. This is the DEFAULT density's
        /// padding since v2.14.0 (the pre-2.14 hardcoded value was font + 4).
        /// </summary>
        internal const float CompactRowHeightPadding = 3f;

        /// <summary>
        /// Padding for a <see cref="SKGridDensity.Compact"/> column header — 7 px, i.e. the reference
        /// terminal's 18 px tape header at an 11 px font. Default density since v2.14.0
        /// (the pre-2.14 hardcoded value was font + 10).
        /// </summary>
        internal const float CompactColumnHeaderHeightPadding = 7f;

        /// <summary>Row padding for the current <see cref="Density"/>.</summary>
        private float RowPadding => Density == SKGridDensity.Compact ? CompactRowHeightPadding : RowHeightPadding;

        /// <summary>Header padding for the current <see cref="Density"/>.</summary>
        private float HeaderPadding => Density == SKGridDensity.Compact ? CompactColumnHeaderHeightPadding : ColumnHeaderHeightPadding;

        // ── Explicit overrides (escape hatch; null/&lt;=0 means "derive from font + density") ──

        private float? _rowHeightOverride;
        private double? _columnHeaderHeightOverride;

        // ── Computed State ──────────────────────────────────────────────

        /// <summary>
        /// Effective row height: the explicit <c>SKRowHeight</c> override when one is set,
        /// otherwise font size + the current <see cref="Density"/>'s row padding. Used by
        /// scrolling, rendering, hit testing.
        /// </summary>
        public float RowHeight { get; private set; } = 11f + CompactRowHeightPadding; // default: Compact, 11 + 3 = 14

        /// <summary>
        /// Effective column header height: the explicit <c>SKColumnHeaderHeight</c> override when one
        /// is set, otherwise font size + the current <see cref="Density"/>'s header padding.
        /// Returns 0 if column headers are hidden.
        /// </summary>
        public double ColumnHeaderHeight { get; private set; } = 11.0 + CompactColumnHeaderHeightPadding; // default: Compact, 11 + 7 = 18

        /// <summary>Vertical density the heights are derived from. Default <see cref="SKGridDensity.Compact"/>.</summary>
        public SKGridDensity Density { get; private set; } = SKGridDensity.Compact;

        /// <summary>
        /// Extra baseline shift (px) applied to cell text so it stays vertically centred when
        /// the row height is decoupled from the font size. Zero when the row height is the
        /// historic font+4, so text lands on exactly the same baseline as before this existed.
        /// </summary>
        public float TextBaselineOffset => (RowHeight - FontSize - 4f) / 2f;

        // ── Current Values ──────────────────────────────────────────────

        public float FontSize { get; private set; } = 11f;
        public string FontFamily { get; private set; } = "Microsoft Sans Serif";
        public string FontStyle { get; private set; } = "Normal";
        public string? ForegroundColor { get; private set; }
        public string? RowBackground { get; private set; }
        public string? AlternatingRowBackground { get; private set; }
        public bool ShowGridLines { get; private set; }
        public string? GridLinesColor { get; private set; }
        public bool ColumnHeaderVisible { get; private set; } = true;

        // ── Update Methods (called by DP callbacks) ─────────────────────

        public void SetFontSize(float size)
        {
            FontSize = size;
            RecomputeMetrics();
            _renderer.SetFontSize(size);
            GridLogger.Log(GridLogger.Render, $"FontSize={size}, RowHeight={RowHeight}");
        }

        /// <summary>
        /// Vertical density. Row and header heights are re-derived from the font size using this
        /// density's padding pair — no pixel values involved, so the grid keeps its density across
        /// font-size changes. Explicit height overrides still win.
        /// </summary>
        public void SetDensity(SKGridDensity density)
        {
            Density = density;
            RecomputeMetrics();
            GridLogger.Log(GridLogger.Render, $"Density={density}, RowHeight={RowHeight}, HeaderHeight={ColumnHeaderHeight}");
        }

        /// <summary>
        /// Explicit data-row height in px — an escape hatch that wins over
        /// <see cref="Density"/>. <c>null</c> or a non-positive value hands control back to
        /// font size + density.
        /// </summary>
        public void SetRowHeightOverride(double? height)
        {
            _rowHeightOverride = height is > 0 ? (float)height.Value : null;
            RecomputeMetrics();
            GridLogger.Log(GridLogger.Render, $"RowHeightOverride={_rowHeightOverride}, RowHeight={RowHeight}");
        }

        /// <summary>
        /// Explicit column-header height in px — an escape hatch that wins over
        /// <see cref="Density"/>. <c>null</c> or a non-positive value hands control back to font size
        /// + density. Ignored while headers are hidden (height stays 0), re-applied when shown again.
        /// </summary>
        public void SetColumnHeaderHeightOverride(double? height)
        {
            _columnHeaderHeightOverride = height is > 0 ? height : null;
            RecomputeMetrics();
            GridLogger.Log(GridLogger.Render, $"ColumnHeaderHeightOverride={_columnHeaderHeightOverride}, ColumnHeaderHeight={ColumnHeaderHeight}");
        }

        /// <summary>
        /// Recompute row/header height from the current font size and density, honoring any explicit
        /// override. Single place the font-size, density, override and header-visibility setters all
        /// funnel through, so they can be set in any order without drift.
        /// </summary>
        private void RecomputeMetrics()
        {
            RowHeight = _rowHeightOverride ?? (FontSize + RowPadding);
            ColumnHeaderHeight = ColumnHeaderVisible
                ? (_columnHeaderHeightOverride ?? (FontSize + HeaderPadding))
                : 0;
        }

        public void SetFontFamily(string family)
        {
            FontFamily = family;
            _renderer.SetFontFamily(family);
            GridLogger.Log(GridLogger.Render, $"FontFamily={family}");
        }

        public void SetFontStyle(string style)
        {
            FontStyle = style;
            _renderer.SetFontStyle(style.ToLower());
            GridLogger.Log(GridLogger.Render, $"FontStyle={style}");
        }

        public void SetForegroundColor(string? color)
        {
            ForegroundColor = color;
            _renderer.SetForeground(color);
        }

        public void SetRowBackground(string? color)
        {
            RowBackground = color;
            _renderer.SetRowBackgroundColor(color);
        }

        public void SetAlternatingRowBackground(string? color)
        {
            AlternatingRowBackground = color;
            _renderer.SetAlternatingRowBackground(color);
        }

        public void SetShowGridLines(bool show)
        {
            ShowGridLines = show;
            _renderer.SetGridLinesVisibility(show);
        }

        public void SetGridLinesColor(string? color)
        {
            GridLinesColor = color;
            _renderer.SetGridLinesColor(color);
        }

        public void SetColumnHeaderVisible(bool visible)
        {
            ColumnHeaderVisible = visible;
            RecomputeMetrics();
        }

        // ── Scroll Bar Thickness ────────────────────────────────────────
        //
        // Only the THICKNESS is configurable — a vertical bar's width, a horizontal bar's height.
        // The other dimension is layout-driven (the vertical bar spans the header + canvas rows, the
        // horizontal one spans the canvas column), so there is nothing to set there.

        /// <summary>
        /// Built-in horizontal scroll bar height in px — the literal SkiaGridViewV2.xaml has always
        /// carried. Kept as the fallback so an unset <c>HorizontalScrollBarHeight</c> looks exactly
        /// as it did before that DP existed.
        /// </summary>
        internal const double DefaultHorizontalScrollBarHeight = 20d;

        /// <summary>
        /// Explicit vertical scroll bar width in px, or <c>null</c> for "leave WPF's own default
        /// alone" (the system metric, ~17 px). <c>null</c> is the default, so a grid that never sets
        /// the DP is unchanged. There is deliberately no numeric fallback here: the control clears
        /// its local values instead of writing a width we guessed.
        /// </summary>
        public double? VerticalScrollBarWidth { get; private set; }

        /// <summary>
        /// Effective horizontal scroll bar height in px. Unlike the vertical width this always has a
        /// number, because the XAML has always pinned the bar to
        /// <see cref="DefaultHorizontalScrollBarHeight"/> rather than to the system metric.
        /// </summary>
        public double HorizontalScrollBarHeight { get; private set; } = DefaultHorizontalScrollBarHeight;

        /// <summary>
        /// Vertical scroll bar width in px. <c>null</c> or a non-positive value means "system
        /// default" (same escape-hatch convention as <see cref="SetRowHeightOverride"/>).
        /// </summary>
        public void SetVerticalScrollBarWidth(double? width)
        {
            VerticalScrollBarWidth = width is > 0 ? width : null;
            GridLogger.Log(GridLogger.Scroll, $"VerticalScrollBarWidth={VerticalScrollBarWidth?.ToString() ?? "system"}");
        }

        /// <summary>
        /// Horizontal scroll bar height in px. <c>null</c> or a non-positive value restores the
        /// built-in <see cref="DefaultHorizontalScrollBarHeight"/>.
        /// </summary>
        public void SetHorizontalScrollBarHeight(double? height)
        {
            HorizontalScrollBarHeight = height is > 0 ? height.Value : DefaultHorizontalScrollBarHeight;
            GridLogger.Log(GridLogger.Scroll, $"HorizontalScrollBarHeight={HorizontalScrollBarHeight}");
        }
    }
}
