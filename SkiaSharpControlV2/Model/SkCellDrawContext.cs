using SkiaSharp;

namespace SkiaSharpControlV2
{
    /// <summary>
    /// Argument bundle passed to <see cref="SKCellTemplate.CustomDraw"/>.
    /// Carries everything a consumer drawer needs to render inside a single cell:
    /// the live <see cref="SKCanvas"/>, the row's bound data item, the cell rect
    /// in canvas coordinates, and the renderer's resolved paints/font so the
    /// drawer can match the grid's styling if it wants to.
    /// <para>
    /// The canvas is clipped to <see cref="Bounds"/> before the drawer is invoked,
    /// so anything drawn outside the cell is dropped — the drawer cannot bleed into
    /// neighbouring cells, headers, or the scroll bars. Use <see cref="Bounds"/>
    /// (not raw absolute coords) when positioning your draw calls.
    /// </para>
    /// </summary>
    public sealed class SkCellDrawContext
    {
        /// <summary>The Skia canvas being painted. Drawing is clipped to <see cref="Bounds"/>.</summary>
        public SKCanvas Canvas { get; init; } = null!;

        /// <summary>The row's bound data item (cast to your own row type).</summary>
        public object? Data { get; init; }

        /// <summary>
        /// The cell's rectangle in canvas coordinates. Use this to position draw calls —
        /// the canvas has been clipped to this rect so any pixels outside it are dropped.
        /// </summary>
        public SKRect Bounds { get; init; }

        /// <summary>Flattened row index within the rendered list (0-based).</summary>
        public int RowIndex { get; init; }

        /// <summary>Visible column index (0-based, after column reordering).</summary>
        public int ColumnIndex { get; init; }

        /// <summary>
        /// The resolved background paint for this cell (after the full 6-step style cascade
        /// plus any selection overlay). Drawn for you BEFORE the delegate runs — provided
        /// here only so your drawing can match it (e.g., compute contrasting text color).
        /// </summary>
        public SKPaint Background { get; init; } = null!;

        /// <summary>
        /// The resolved foreground paint for this cell. Use this if your custom drawing
        /// includes text and you want to match the grid's default text color / triggers.
        /// </summary>
        public SKPaint Foreground { get; init; } = null!;

        /// <summary>The grid's current default font (size, family, style).</summary>
        public SKFont Font { get; init; } = null!;
    }
}
