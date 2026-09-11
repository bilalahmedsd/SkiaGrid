namespace SkiaSharpControlV2.Model
{
    /// <summary>Wrapper for a grouped row (header, subtotal, or data) in flattened rendering list.</summary>
    public class GroupModel
    {
        public bool IsHeaderSubTotal { get; set; }
        public bool IsGroupSubTotal { get; set; }
        public bool IsGroupHeader { get; set; }
        public string? GroupName { get; set; }
        public string? SubTotalGroupName { get; set; }
        public string? BindingPath { get; set; }
        public bool IsExpanded { get; set; } = true;
        public object? Item { get; set; }
    }

    /// <summary>Wrapper for a data row (parent or child) in flattened rendering list.</summary>
    public class RowModel
    {
        public bool IsChildRow { get; set; }
        public object? Item { get; set; }
        public bool HasChild { get; set; }
    }
}
