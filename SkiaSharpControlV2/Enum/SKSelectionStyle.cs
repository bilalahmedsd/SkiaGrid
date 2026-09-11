namespace SkiaSharpControlV2
{
    /// <summary>
    /// Controls how selected rows are visually highlighted.
    /// Fill: full background color change (default).
    /// Border: border outline around selected row, original colors preserved.
    /// </summary>
    public enum SKSelectionStyle
    {
        /// <summary>Selected row background and text colors change entirely.</summary>
        Fill,
        /// <summary>Selected row gets a border outline only — original styling preserved.</summary>
        Border
    }
}
