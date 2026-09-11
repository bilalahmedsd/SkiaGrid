using SkiaSharpControlV2.Services;
using SkiaSharpControlV2.Renderer;
using SkiaSharpControlV2.Helpers;

namespace SkiaSharpControlV2.Tests.Services;

/// <summary>
/// Tests GridAppearanceConfig's computed metrics: RowHeight / ColumnHeaderHeight derivation from
/// the font size, the explicit override path (SKRowHeight / SKColumnHeaderHeight), and the
/// text-baseline offset that keeps cell text centred once row height is decoupled from font size.
/// A real SkiaRenderer is used — it needs SkiaSharp but not WPF, so it constructs headless.
/// </summary>
public class GridAppearanceConfigTests
{
    private static GridAppearanceConfig NewConfig(out SkiaRenderer renderer)
    {
        renderer = new SkiaRenderer(new ReflectionHelper());
        return new GridAppearanceConfig(renderer);
    }

    [Fact]
    public void DefaultValues_MatchReferenceTerminalTapeMetrics()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            Assert.Equal(11f, config.FontSize);
            Assert.Equal("Microsoft Sans Serif", config.FontFamily);
            Assert.Equal(SKGridDensity.Compact, config.Density);
            Assert.Equal(14f, config.RowHeight);           // 11 + CompactRowHeightPadding(3)
            Assert.Equal(18.0, config.ColumnHeaderHeight); // 11 + CompactColumnHeaderHeightPadding(7)
        }
    }

    [Theory]
    [InlineData(10f, 13f)]
    [InlineData(11f, 14f)] // default font
    [InlineData(12f, 15f)]
    [InlineData(18f, 21f)]
    public void RowHeight_DefaultDensity_IsFontSizePlusThree(float fontSize, float expected)
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetFontSize(fontSize);
            Assert.Equal(expected, config.RowHeight);
        }
    }

    [Theory]
    [InlineData(10f, 16f)]
    [InlineData(11f, 17f)]
    [InlineData(12f, 18f)]
    [InlineData(18f, 24f)]
    public void RowHeight_NormalDensity_IsFontSizePlusSix(float fontSize, float expected)
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetDensity(SKGridDensity.Normal);
            config.SetFontSize(fontSize);
            Assert.Equal(expected, config.RowHeight);
        }
    }

    [Theory]
    [InlineData(11f, true, 18.0)]
    [InlineData(18f, true, 25.0)]
    [InlineData(11f, false, 0.0)]
    [InlineData(24f, false, 0.0)]
    public void ColumnHeaderHeight_DefaultDensity_IsFontSizePlusSevenAndZeroWhenHidden(float fontSize, bool visible, double expected)
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetFontSize(fontSize);
            config.SetColumnHeaderVisible(visible);
            Assert.Equal(expected, config.ColumnHeaderHeight);
        }
    }

    [Theory]
    [InlineData(11f, true, 23.0)]
    [InlineData(18f, true, 30.0)]
    [InlineData(11f, false, 0.0)]
    public void ColumnHeaderHeight_NormalDensity_IsFontSizePlusTwelve(float fontSize, bool visible, double expected)
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetDensity(SKGridDensity.Normal);
            config.SetFontSize(fontSize);
            config.SetColumnHeaderVisible(visible);
            Assert.Equal(expected, config.ColumnHeaderHeight);
        }
    }

    [Fact]
    public void Density_DefaultsToCompact()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
            Assert.Equal(SKGridDensity.Compact, config.Density);
    }

    [Theory]
    // Compact = font+3 rows / font+7 header. At the default 11px font that is the reference
    // terminal's 14/18 tape — the numbers the demo used to hardcode.
    [InlineData(11f, 14f, 18.0)]
    [InlineData(12f, 15f, 19.0)]
    [InlineData(13f, 16f, 20.0)]
    public void Density_Compact_DerivesTighterHeightsFromFontSize(float fontSize, float expectedRow, double expectedHeader)
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetDensity(SKGridDensity.Compact);
            config.SetFontSize(fontSize);
            Assert.Equal(expectedRow, config.RowHeight);
            Assert.Equal(expectedHeader, config.ColumnHeaderHeight);
        }
    }

    [Fact]
    public void Density_SurvivesAFontSizeChange_SoProportionsTrackTheFont()
    {
        // The whole reason Density exists rather than hardcoded pixels: change the font and the
        // heights follow, instead of being stuck at the value someone typed once.
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetDensity(SKGridDensity.Compact);
            config.SetFontSize(11f);
            Assert.Equal(14f, config.RowHeight);

            config.SetFontSize(16f);
            Assert.Equal(19f, config.RowHeight);       // 16 + 3, still compact
            Assert.Equal(23.0, config.ColumnHeaderHeight); // 16 + 7
        }
    }

    [Fact]
    public void Density_SetInEitherOrderRelativeToFontSize_GivesTheSameResult()
    {
        var a = NewConfig(out var r1);
        var b = NewConfig(out var r2);
        using (r1)
        using (r2)
        {
            a.SetDensity(SKGridDensity.Compact);
            a.SetFontSize(13f);

            b.SetFontSize(13f);
            b.SetDensity(SKGridDensity.Compact);

            Assert.Equal(a.RowHeight, b.RowHeight);
            Assert.Equal(a.ColumnHeaderHeight, b.ColumnHeaderHeight);
        }
    }

    [Fact]
    public void Density_BackToNormal_RestoresTheWiderHeights()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetDensity(SKGridDensity.Compact);
            Assert.Equal(14f, config.RowHeight);

            config.SetDensity(SKGridDensity.Normal);
            Assert.Equal(17f, config.RowHeight);
            Assert.Equal(23.0, config.ColumnHeaderHeight);
        }
    }

    [Fact]
    public void ExplicitOverride_WinsOverDensity()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetDensity(SKGridDensity.Compact);
            config.SetRowHeightOverride(30d);
            Assert.Equal(30f, config.RowHeight);

            // Clearing the override hands control back to density, not to Normal.
            config.SetRowHeightOverride(null);
            Assert.Equal(14f, config.RowHeight);
        }
    }

    [Fact]
    public void Density_Compact_HiddenHeader_StillZero()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetDensity(SKGridDensity.Compact);
            config.SetColumnHeaderVisible(false);
            Assert.Equal(0.0, config.ColumnHeaderHeight);

            config.SetColumnHeaderVisible(true);
            Assert.Equal(18.0, config.ColumnHeaderHeight);
        }
    }

    [Fact]
    public void SetRowHeightOverride_WinsOverFontDerivedHeight()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetFontSize(11f);
            // 30 is deliberately not any density's derived value — at 11px Compact gives 14 and
            // Normal 17, so asserting 30 actually proves the override is in charge.
            config.SetRowHeightOverride(30d);
            Assert.Equal(30f, config.RowHeight);
        }
    }

    [Fact]
    public void SetRowHeightOverride_SurvivesALaterFontSizeChange()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetRowHeightOverride(30d);
            config.SetFontSize(18f); // would otherwise compute 21 (Compact) / 24 (Normal)
            Assert.Equal(30f, config.RowHeight);
        }
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-5d)]
    [InlineData(null)]
    public void SetRowHeightOverride_NonPositiveOrNull_RestoresAutoHeight(double? value)
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetFontSize(11f);
            config.SetRowHeightOverride(30d);
            config.SetRowHeightOverride(value);
            Assert.Equal(14f, config.RowHeight); // back to 11 + 3 (default Compact density)
        }
    }

    [Fact]
    public void SetColumnHeaderHeightOverride_WinsOverFontDerivedHeight()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetFontSize(11f);
            config.SetColumnHeaderHeightOverride(30d); // neither 18 (Compact) nor 23 (Normal)
            Assert.Equal(30.0, config.ColumnHeaderHeight);
        }
    }

    [Fact]
    public void SetColumnHeaderHeightOverride_StillZeroWhileHeadersHidden_AndReturnsWhenShown()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetColumnHeaderHeightOverride(30d);
            config.SetColumnHeaderVisible(false);
            Assert.Equal(0.0, config.ColumnHeaderHeight);

            config.SetColumnHeaderVisible(true);
            Assert.Equal(30.0, config.ColumnHeaderHeight);
        }
    }

    [Fact]
    public void ScrollBarThickness_Defaults_KeepThePreDpAppearance()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            // null = "leave WPF's system width alone" — the control clears its local values
            // instead of writing a guessed number.
            Assert.Null(config.VerticalScrollBarWidth);
            // The horizontal bar has always been pinned to 20 px in XAML, so it has a real default.
            Assert.Equal(GridAppearanceConfig.DefaultHorizontalScrollBarHeight, config.HorizontalScrollBarHeight);
            Assert.Equal(20d, config.HorizontalScrollBarHeight);
        }
    }

    [Theory]
    [InlineData(8d)]   // thinner than the system metric (~17) — the case MinWidth has to allow
    [InlineData(17d)]
    [InlineData(28d)]
    public void SetVerticalScrollBarWidth_PositiveValue_IsKept(double width)
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetVerticalScrollBarWidth(width);
            Assert.Equal(width, config.VerticalScrollBarWidth);
        }
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-4d)]
    [InlineData(null)]
    public void SetVerticalScrollBarWidth_NonPositiveOrNull_RestoresSystemWidth(double? width)
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetVerticalScrollBarWidth(10d);
            config.SetVerticalScrollBarWidth(width);
            Assert.Null(config.VerticalScrollBarWidth);
        }
    }

    [Theory]
    [InlineData(6d)]
    [InlineData(20d)]
    [InlineData(32d)]
    public void SetHorizontalScrollBarHeight_PositiveValue_IsKept(double height)
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetHorizontalScrollBarHeight(height);
            Assert.Equal(height, config.HorizontalScrollBarHeight);
        }
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-9d)]
    [InlineData(null)]
    public void SetHorizontalScrollBarHeight_NonPositiveOrNull_RestoresTheBuiltIn20(double? height)
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetHorizontalScrollBarHeight(9d);
            config.SetHorizontalScrollBarHeight(height);
            Assert.Equal(20d, config.HorizontalScrollBarHeight);
        }
    }

    [Fact]
    public void ScrollBarThickness_IsIndependentOfTheRowMetrics()
    {
        // Thickness is not derived from the font the way row/header heights are — a font change
        // must not quietly resize the bars, and a bar change must not touch the row height.
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetVerticalScrollBarWidth(9d);
            config.SetHorizontalScrollBarHeight(9d);

            config.SetFontSize(18f);
            Assert.Equal(9d, config.VerticalScrollBarWidth);
            Assert.Equal(9d, config.HorizontalScrollBarHeight);
            Assert.Equal(21f, config.RowHeight); // 18 + Compact(3), untouched by the bar settings
        }
    }

    [Fact]
    public void TextBaselineOffset_IsZeroAtTheLegacyFontPlusFourRowHeight()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetFontSize(12f);
            config.SetRowHeightOverride(16d); // the pre-v2.14.0 default geometry
            Assert.Equal(0f, config.TextBaselineOffset);
        }
    }

    [Fact]
    public void TextBaselineOffset_SplitsSurplusRowHeightEvenly()
    {
        var config = NewConfig(out var renderer);
        using (renderer)
        {
            config.SetFontSize(11f);   // default Compact row height 14 → 1 px short of font+4
            Assert.Equal(-0.5f, config.TextBaselineOffset);

            config.SetDensity(SKGridDensity.Normal); // row height 17 → 2 px surplus over font+4
            Assert.Equal(1f, config.TextBaselineOffset);

            config.SetRowHeightOverride(25d); // 10 px surplus → shift down by 5
            Assert.Equal(5f, config.TextBaselineOffset);
        }
    }
}
