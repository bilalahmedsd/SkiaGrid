using SkiaSharp;

namespace SkiaSharpControls.Renderer
{
    public interface ISkGridRenderer : IDisposable
    {
        void Draw(SKCanvas canvas, float scrollOffsetX, float scrollOffsetY, SKFont font, float rowHeight, int totalRows);
    }
}