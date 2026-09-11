using SkiaSharpControlV2.Renderer;

namespace SkiaSharpControlV2.Tests.Renderer;

/// <summary>
/// Tests CellRenderer's vertical text placement. The drawing itself needs a live SKCanvas, but the
/// baseline math is the part that changed when row height stopped being derived from the font size,
/// so it is pulled out and tested directly.
/// </summary>
public class CellRendererTests
{
    [Fact]
    public void TextBaselineY_UnknownRowHeight_UsesLegacyBaseline()
    {
        // rowHeight 0 = "caller didn't say" → historic y + fontSize.
        Assert.Equal(112f, CellRenderer.TextBaselineY(y: 100f, rowHeight: 0f, fontSize: 12f));
    }

    [Fact]
    public void TextBaselineY_LegacyGeometry_IsUnchanged()
    {
        // The pre-v2.14.0 pairing (row height == fontSize + 4) must land on the exact same
        // baseline it always did, so existing consumers see no pixel shift.
        Assert.Equal(112f, CellRenderer.TextBaselineY(y: 100f, rowHeight: 16f, fontSize: 12f));
        Assert.Equal(115f, CellRenderer.TextBaselineY(y: 100f, rowHeight: 19f, fontSize: 15f));
    }

    [Fact]
    public void TextBaselineY_TallerRow_SplitsTheSurplusEvenly()
    {
        // 17 px row, 11 px font: 2 px surplus over font+4 → baseline drops 1 px.
        Assert.Equal(112f, CellRenderer.TextBaselineY(y: 100f, rowHeight: 17f, fontSize: 11f));
        // 24 px row, 12 px font: 8 px surplus → baseline drops 4 px.
        Assert.Equal(116f, CellRenderer.TextBaselineY(y: 100f, rowHeight: 24f, fontSize: 12f));
    }

    [Fact]
    public void TextBaselineY_ShorterRow_LiftsTheBaseline()
    {
        // 14 px row, 11 px font: 1 px short of font+4 → baseline lifts 0.5 px.
        Assert.Equal(110.5f, CellRenderer.TextBaselineY(y: 100f, rowHeight: 14f, fontSize: 11f));
    }

    [Fact]
    public void TextBaselineY_KeepsGlyphBandCentred()
    {
        // Sanity check on the intent: with a nominal cap height of ~0.72em the ink band should
        // sit within ~1 px of centre across a wide row-height range.
        const float fontSize = 11f;
        float capHeight = 0.72f * fontSize;
        foreach (var rowHeight in new[] { 14f, 15f, 17f, 20f, 26f })
        {
            float baseline = CellRenderer.TextBaselineY(0f, rowHeight, fontSize);
            float above = baseline - capHeight;      // gap above the glyphs
            float below = rowHeight - baseline;      // gap below the glyphs
            Assert.True(Math.Abs(above - below) <= 1.5f,
                $"rowHeight={rowHeight}: above={above:0.##} below={below:0.##}");
        }
    }
}
