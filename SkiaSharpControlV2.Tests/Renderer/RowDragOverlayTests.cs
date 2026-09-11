using SkiaSharp;

using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Renderer;

namespace SkiaSharpControlV2.Tests.Renderer;

/// <summary>
/// Row drag visuals (2.18.0). Rendered to a real <see cref="SKSurface"/> and read back pixel by
/// pixel, the same technique the image-button geometry tests use: the whole point of the indicator
/// is that a user can see WHERE the drop will land, and only painted pixels prove that.
///
/// The overlay is also the one part of the drag that runs inside the paint path, so the
/// "costs nothing when not dragging" guarantee is asserted here too.
/// </summary>
public class RowDragOverlayTests
{
    private const int Width = 120;
    private const int Height = 100;
    private const float RowHeight = 14f;   // default Compact row
    private const int TotalRows = 6;

    private static SkiaRenderer BuildRenderer(double columnWidth = 100)
    {
        var renderer = new SkiaRenderer(new ReflectionHelper());
        renderer.SetColumns(new[] { new SKGridViewColumn { Name = "c0", Width = columnWidth } });
        renderer.UpdateVisibleColumns();
        return renderer;
    }

    /// <summary>Renders the overlay onto a transparent surface and returns the pixels.</summary>
    private static SKBitmap Render(SkiaRenderer renderer)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        renderer.DrawRowDragOverlay(surface.Canvas, RowHeight, TotalRows);

        var bitmap = new SKBitmap(Width, Height);
        surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
        return bitmap;
    }

    /// <summary>
    /// Rows whose CENTRE pixel is painted — i.e. the ghosted rows. Probing the centre rather than
    /// bucketing every painted y by row height is deliberate: the 2 px indicator straddles a row
    /// boundary, so bucketing would attribute it to two rows and could not be told from a ghost.
    /// </summary>
    private static List<int> GhostedRows(SKBitmap bitmap)
    {
        var rows = new List<int>();
        for (int row = 0; row < TotalRows; row++)
        {
            int y = (int)(row * RowHeight + RowHeight / 2);
            if (bitmap.GetPixel(10, y).Alpha != 0) rows.Add(row);
        }
        return rows;
    }

    /// <summary>True when any pixel at all was painted.</summary>
    private static bool AnythingPainted(SKBitmap bitmap)
    {
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Alpha != 0) return true;
        return false;
    }

    private static (int Top, int Bottom, int Left, int Right) PaintedBounds(SKBitmap bitmap)
    {
        int top = int.MaxValue, bottom = -1, left = int.MaxValue, right = -1;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).Alpha != 0)
                {
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                    if (x < left) left = x;
                    if (x > right) right = x;
                }
        return (top, bottom, left, right);
    }

    [Fact]
    public void NotDragging_PaintsNothing()
    {
        // The guarantee that matters for every grid that never opted in: RowDragInsertIndex stays
        // -1, so the overlay pass leaves the frame untouched.
        var renderer = BuildRenderer();
        Assert.Equal(-1, renderer.RowDragInsertIndex);

        using var bitmap = Render(renderer);

        Assert.False(AnythingPainted(bitmap));
    }

    [Fact]
    public void IndicatorSitsOnTheBoundaryOfTheDropGap()
    {
        var renderer = BuildRenderer();
        renderer.RowDragInsertIndex = 3;   // between row 2 and row 3

        using var bitmap = Render(renderer);
        var bounds = PaintedBounds(bitmap);

        // Gap 3 is at y = 3 * 14 = 42, and the 2 px line straddles it.
        Assert.Equal(41, bounds.Top);
        Assert.Equal(42, bounds.Bottom);
    }

    [Fact]
    public void IndicatorSpansTheVisibleColumnWidthOnly()
    {
        // 100 px of columns on a 120 px surface: the line must stop at the last column, not run to
        // the edge of the canvas.
        var renderer = BuildRenderer(columnWidth: 100);
        renderer.RowDragInsertIndex = 2;

        using var bitmap = Render(renderer);
        var bounds = PaintedBounds(bitmap);

        Assert.Equal(0, bounds.Left);
        Assert.Equal(99, bounds.Right);
    }

    [Fact]
    public void IndicatorAtGapZero_StaysInsideTheCanvas()
    {
        // Half the line would sit at negative y — it must be clamped, not clipped away.
        var renderer = BuildRenderer();
        renderer.RowDragInsertIndex = 0;

        using var bitmap = Render(renderer);
        var bounds = PaintedBounds(bitmap);

        Assert.Equal(0, bounds.Top);
        Assert.True(bounds.Bottom >= 1, $"expected a visible line at the top, got bottom={bounds.Bottom}");
    }

    [Fact]
    public void DraggedRowsAreGhosted()
    {
        var renderer = BuildRenderer();
        renderer.RowDragInsertIndex = 5;
        renderer.RowDragRowIndexes = new List<int> { 1, 3 };

        using var bitmap = Render(renderer);

        Assert.Equal(new[] { 1, 3 }, GhostedRows(bitmap));
    }

    [Fact]
    public void GhostIsTranslucent_ButTheIndicatorIsNot()
    {
        var renderer = BuildRenderer();
        renderer.RowDragInsertIndex = 4;
        renderer.RowDragRowIndexes = new List<int> { 0 };

        using var bitmap = Render(renderer);

        var ghost = bitmap.GetPixel(10, 7);       // middle of ghosted row 0
        var indicator = bitmap.GetPixel(10, 56);  // the line at gap 4 (y = 56)

        Assert.InRange(ghost.Alpha, 1, 254);
        Assert.Equal(255, indicator.Alpha);
    }

    [Fact]
    public void GhostIndexesOutsideTheGridAreIgnored()
    {
        // A stale index can survive a data tick that shrank the grid mid-drag.
        var renderer = BuildRenderer();
        renderer.RowDragInsertIndex = 1;
        renderer.RowDragRowIndexes = new List<int> { -1, 99 };

        using var bitmap = Render(renderer);

        // The indicator still drew (proving the pass ran), but nothing was ghosted.
        Assert.True(AnythingPainted(bitmap));
        Assert.Empty(GhostedRows(bitmap));
    }

    [Fact]
    public void NoVisibleColumns_PaintsNothing()
    {
        var renderer = new SkiaRenderer(new ReflectionHelper());
        renderer.UpdateVisibleColumns();   // no columns at all
        renderer.RowDragInsertIndex = 2;

        using var bitmap = Render(renderer);

        Assert.False(AnythingPainted(bitmap));
    }

    // ── ghost colour derivation ─────────────────────────────────────────────────

    [Fact]
    public void GhostHex_AddsAlphaToASixDigitColour()
    {
        Assert.Equal("#553399FF", SkiaRenderer.GhostHex("#3399FF"));
    }

    [Fact]
    public void GhostHex_ReplacesAnAlphaThatIsAlreadyThere()
    {
        // Otherwise a caller-supplied ARGB would yield a 10-digit string that SKColor.Parse rejects,
        // and SKPaintCache would silently hand back a transparent paint — an invisible ghost.
        Assert.Equal("#553399FF", SkiaRenderer.GhostHex("#FF3399FF"));
    }

    [Fact]
    public void GhostHex_ExpandsShorthand()
    {
        Assert.Equal("#5533AAFF", SkiaRenderer.GhostHex("#3AF"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-colour")]
    public void GhostHex_LeavesSomethingUnparseableAlone(string input)
    {
        Assert.Equal(input, SkiaRenderer.GhostHex(input));
    }
}
