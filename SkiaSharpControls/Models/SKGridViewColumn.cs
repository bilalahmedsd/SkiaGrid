using SkiaSharpControls.Enum;

namespace SkiaSharpControls.Models
{
    public class SkGridViewColumn
    {
        public required string Header { get; set; } = "";
        public double Width { get; set; } = 100;
        public bool IsVisible { get; set; } = true;
        public string? DisplayHeader { get; set; }
        public string? BackColor { get; set; }
        public SkGridViewColumnSort GridViewColumnSort { get; set; } 
        public CellContentAlignment ContentAlignment { get; set; } = CellContentAlignment.Left;
        public bool? CanUserResize { get; set; } 
        public bool? CanUserReorder { get; set; } 
        public bool? CanUserSort { get; set; } 
    }
}