namespace SkiaSharpControlV2.Renderer
{
    /// <summary>
    /// Lightweight context passed to SkiaRenderer.Draw() instead of the full SkiaGridViewV2 control.
    /// Contains only the state needed for rendering: toggle dictionaries, templates.
    /// Built by SkiaGridViewV2 before each paint cycle.
    /// Created in Step 14 refactoring to remove CurrentContext back-reference.
    /// </summary>
    internal class GridRenderContext
    {
        public Dictionary<string, (bool IsExpended, float x, float y, float height, float width)> GroupToggleDetails { get; init; } = new();
        public Dictionary<object, (float x, float y, float height, float width)> RowToggleDetails { get; init; } = new();
        public Dictionary<(object, string name), (float x, float y, float height, float width, SkButton btn)> ButtonDetails { get; init; } = new();
        public SKRowTemplate? RowTemplate { get; init; }
        public SKCellTemplate? CellTemplate { get; init; }
        public Dictionary<object, bool>? ExpandedItems { get; init; }
        public Dictionary<(object item, string bindingPath), (float x, float y, float w, float h)> CheckboxDetails { get; init; } = new();
    }
}
