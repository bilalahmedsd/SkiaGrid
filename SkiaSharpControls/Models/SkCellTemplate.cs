using SkiaSharp;
using SkiaSharpControls.Enum;

namespace SkiaSharpControls.Models
{
    public class SkCellTemplate
    {
        /// <summary>
        /// Indicates if the cell should be drawn as a toggle button.
        /// </summary>
        public bool IsToggleButton { get; set; }

        public bool IsToggleButtonOn { get; set; }

        public string CellContent { get; set; } = "";
        public CellContentAlignment CellContentAlignment { get; set; } = CellContentAlignment.Left;

        /// <summary>
        /// Gives canvas on which drawing is being done along with current row's x and y positions
        /// </summary>
        public Action<SKCanvas, float, float>? CustomDrawing { get; set; }

        public SkRendererProperties? RendererProperties { get; set; }
    }
}