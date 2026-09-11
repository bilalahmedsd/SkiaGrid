namespace SkiaSharpControlV2
{
    /// <summary>
    /// What caused an expand/collapse state change reported by
    /// <see cref="SkiaGridViewV2.ExpandChanged"/> / <c>ExpandChangedCommand</c>.
    /// </summary>
    public enum SkExpandChangeReason
    {
        /// <summary>The user clicked the expand/collapse toggle glyph on a group header or parent row.</summary>
        UserToggle,
        /// <summary>Caused by an explicit <see cref="SkiaGridViewV2.ToggleGroup"/> / <see cref="SkiaGridViewV2.ToggleRow"/> call.</summary>
        Api,
        /// <summary>Caused by <see cref="SkiaGridViewV2.ExpandAll"/> — one bulk notification, not one per group.</summary>
        ExpandAll,
        /// <summary>Caused by <see cref="SkiaGridViewV2.CollapseAll"/> — one bulk notification, not one per group.</summary>
        CollapseAll
    }
}
