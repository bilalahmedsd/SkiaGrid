namespace SkiaSharpControlV2
{
    /// <summary>
    /// Vertical density of the grid: how much padding is added to <c>SKFontSize</c> to get the
    /// data-row and column-header heights. Both are DERIVED from the font size, so a grid keeps its
    /// intended density when the font changes — no pixel values to maintain in the consumer.
    /// <para>
    /// Padding pairs are taken from the reference trading terminal, measured at an 11 px font:
    /// <c>Compact</c> = 14 px rows / 18 px header, <c>Normal</c> = 17 px rows / 23 px header.
    /// </para>
    /// Use <c>SKRowHeight</c> / <c>SKColumnHeaderHeight</c> only when an exact pixel height is
    /// genuinely required — they override whatever this resolves to.
    /// </summary>
    public enum SKGridDensity
    {
        /// <summary>
        /// <c>SKFontSize + 6</c> rows, <c>SKFontSize + 12</c> header — 17/23 px at an 11 px font.
        /// The reference terminal's positions / account / portfolio grid density: more breathing room
        /// per row. Opt into this per grid when rows should not be at tape density.
        /// </summary>
        Normal,

        /// <summary>
        /// Default since v2.14.0. <c>SKFontSize + 3</c> rows, <c>SKFontSize + 7</c> header — 14/18 px
        /// at an 11 px font, the reference terminal's time-and-sales tape density. Fits the most rows
        /// on screen, which is the common case for market-data grids.
        /// </summary>
        Compact
    }
}
