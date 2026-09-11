using System.Windows.Media;
using System.Windows.Media.Imaging;

using SkiaSharp;

using SkiaSharpControlV2.Renderer;

namespace SkiaSharpControlV2.Tests.Renderer;

/// <summary>
/// Tests ButtonRenderer's image placement. Drawing needs a live SKCanvas, but the destination-rect
/// math is what shipped wrong (fixed in v2.16.0: a 12 px icon rendered 6x9 px on a 14 px Compact row), so it
/// is pulled out into <see cref="ButtonRenderer.ImageRect"/> and tested directly.
/// </summary>
public class ButtonRendererTests
{
    // The default Compact geometry a trading grid actually runs at: font 11 → 14 px rows.
    private const float CompactBand = 14f;

    [Fact]
    public void ImageRect_Auto_IsSquare()
    {
        var r = ButtonRenderer.ImageRect(slotX: 0f, slotWidth: 40f, bandTop: 0f, bandHeight: CompactBand,
                                         requestedSize: 0, alignment: CellContentAlignment.Left);

        // The old math produced 6 x 9 here — a non-square glyph out of a 10 px budget.
        Assert.Equal(r.Width, r.Height);
        Assert.Equal(12f, r.Width);
    }

    [Fact]
    public void ImageRect_Auto_FillsTheBandLessOnePxAboveAndBelow()
    {
        var r = ButtonRenderer.ImageRect(0f, 40f, bandTop: 100f, bandHeight: CompactBand,
                                         0, CellContentAlignment.Left);

        Assert.Equal(101f, r.Top);
        Assert.Equal(113f, r.Bottom);
    }

    [Fact]
    public void ImageRect_Auto_NeverExceedsTheSlot()
    {
        // A 16 px alert-icon slot on a 30 px row: the slot, not the band, is the binding constraint.
        var r = ButtonRenderer.ImageRect(0f, slotWidth: 16f, bandTop: 0f, bandHeight: 30f,
                                         0, CellContentAlignment.Left);

        Assert.Equal(16f, r.Width);
        Assert.Equal(16f, r.Height);
    }

    [Fact]
    public void ImageRect_CentresVerticallyInTheBand()
    {
        // 12 px glyph in a 20 px band → 4 px above and below.
        var r = ButtonRenderer.ImageRect(0f, 12f, bandTop: 50f, bandHeight: 20f,
                                         0, CellContentAlignment.Left);

        Assert.Equal(54f, r.Top);
        Assert.Equal(66f, r.Bottom);
    }

    [Theory]
    [InlineData(CellContentAlignment.Left, 100f)]
    [InlineData(CellContentAlignment.Center, 114f)]
    [InlineData(CellContentAlignment.Right, 128f)]
    public void ImageRect_HonorsContentAlignment(CellContentAlignment alignment, float expectedLeft)
    {
        // 12 px glyph in a 40 px slot starting at x=100. The old code ignored alignment entirely
        // and always pinned the image to slotX.
        var r = ButtonRenderer.ImageRect(slotX: 100f, slotWidth: 40f, bandTop: 0f, bandHeight: CompactBand,
                                         0, alignment);

        Assert.Equal(expectedLeft, r.Left);
    }

    [Fact]
    public void ImageRect_ExplicitSize_IsHonored()
    {
        var r = ButtonRenderer.ImageRect(0f, 40f, 0f, bandHeight: 30f,
                                         requestedSize: 18, alignment: CellContentAlignment.Left);

        Assert.Equal(18f, r.Width);
        Assert.Equal(18f, r.Height);
    }

    [Fact]
    public void ImageRect_ExplicitSize_ClampedToBandAndSlot()
    {
        // Asking for 20 px on a 14 px row can't be granted — the next row would overpaint it.
        Assert.Equal(CompactBand, ButtonRenderer.ImageRect(0f, 40f, 0f, CompactBand, 20, CellContentAlignment.Left).Width);
        // Same for a slot narrower than the request.
        Assert.Equal(8f, ButtonRenderer.ImageRect(0f, slotWidth: 8f, bandTop: 0f, bandHeight: 30f, 20, CellContentAlignment.Left).Width);
    }

    [Fact]
    public void ImageRect_DegenerateSlot_IsEmpty()
    {
        // A collapsed column must not produce a negative-size rect.
        Assert.True(ButtonRenderer.ImageRect(0f, slotWidth: 0f, bandTop: 0f, bandHeight: CompactBand, 0, CellContentAlignment.Left).IsEmpty);
        Assert.True(ButtonRenderer.ImageRect(0f, 40f, 0f, bandHeight: 2f, 0, CellContentAlignment.Left).IsEmpty);
    }

    // ── End-to-end: actually draw the button and measure the painted glyph ──

    /// <summary>
    /// Draws a real image button onto an SKSurface and measures the icon's painted extent, so the
    /// rect math above is proven to reach the canvas (and not be re-derived on the way).
    /// Mirrors the L2 order book: a 40 px column, a centred icon, a 14 px Compact row.
    /// </summary>
    [Fact]
    public void DrawButton_PaintsASquareCentredGlyph()
    {
        var bounds = DrawAndMeasure(buttonWidth: 40, cellWidth: 40, rowHeight: CompactBand,
                                    alignment: CellContentAlignment.Center, imageSize: 0);

        Assert.Equal(12, bounds.Width);
        Assert.Equal(12, bounds.Height);   // square — the shipped bug painted 6 x 9 here
        Assert.Equal(14, bounds.Left);     // centred in the 40 px column
        Assert.Equal(1, bounds.Top);       // centred in the 14 px row band
    }

    /// <summary>
    /// The L2 order book configures a 60 px bolt button inside a 40 px Image column. The glyph must
    /// stay inside the column instead of centring itself against the oversized button slot.
    /// </summary>
    [Fact]
    public void DrawButton_ButtonWiderThanCell_KeepsGlyphInsideTheColumn()
    {
        var bounds = DrawAndMeasure(buttonWidth: 60, cellWidth: 40, rowHeight: CompactBand,
                                    alignment: CellContentAlignment.Center, imageSize: 0);

        Assert.Equal(14, bounds.Left);
        Assert.True(bounds.Right <= 40, $"glyph spilled past the column edge: right={bounds.Right}");
    }

    [Fact]
    public void DrawButton_ExplicitImageSize_PaintsThatSize()
    {
        var bounds = DrawAndMeasure(buttonWidth: 40, cellWidth: 40, rowHeight: 30f,
                                    alignment: CellContentAlignment.Center, imageSize: 8);

        Assert.Equal(8, bounds.Width);
        Assert.Equal(8, bounds.Height);
    }

    /// <summary>
    /// Renders one button onto a white surface and returns the bounding box of the red icon pixels
    /// (inclusive left/top, exclusive right/bottom).
    /// </summary>
    private static (int Left, int Top, int Right, int Bottom, int Width, int Height) DrawAndMeasure(
        double buttonWidth, float cellWidth, float rowHeight, CellContentAlignment alignment, double imageSize)
    {
        var button = new SkButton
        {
            Name = "Icon",
            Width = buttonWidth,
            ImageSource = CreateRedBitmap(),
            ImageSize = imageSize,
            ContentAlignment = alignment,
        };

        using var symbolFont = new SKFont(SKTypeface.Default, 11);
        using var unicodeFont = new SKFont(SKTypeface.Default, 11);
        using var foreground = new SKPaint { Color = SKColors.Black };
        using var background = new SKPaint { Color = SKColors.White };
        // No border paint: a border would paint over the row band and confuse the measurement.
        using var renderer = new ButtonRenderer(
            new Dictionary<(object, string), (float x, float y, float height, float width, SkButton btn)>(),
            symbolFont, unicodeFont, foreground, null!);

        using var surface = SKSurface.Create(new SKImageInfo(80, (int)rowHeight + 4));
        surface.Canvas.Clear(SKColors.White);
        renderer.DrawButton(surface.Canvas, button, data: new object(), currentX: 0f, y: 0f,
                            width: cellWidth, rowHeight: rowHeight, rowBackColor: background,
                            columnsIndex: 0, rowIndex: 0, isBorderDrawed: false);

        using var snapshot = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(snapshot);

        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int py = 0; py < pixels.Height; py++)
            for (int px = 0; px < pixels.Width; px++)
            {
                var c = pixels.GetPixel(px, py);
                if (c.Red <= 128 || c.Green > 128 || c.Blue > 128) continue; // not the red icon
                left = Math.Min(left, px);
                top = Math.Min(top, py);
                right = Math.Max(right, px + 1);
                bottom = Math.Max(bottom, py + 1);
            }

        Assert.True(right > 0, "no icon pixels were painted");
        return (left, top, right, bottom, right - left, bottom - top);
    }

    private static ImageSource CreateRedBitmap()
    {
        const int size = 8;
        const int stride = size * 4;
        var buffer = new byte[stride * size];
        for (int i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = 0;         // B
            buffer[i + 1] = 0;     // G
            buffer[i + 2] = 255;   // R
            buffer[i + 3] = 255;   // A
        }

        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, buffer, stride);
        bitmap.Freeze();
        return bitmap;
    }
}
