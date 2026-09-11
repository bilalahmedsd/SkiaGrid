using SkiaSharpControlV2.Helpers;

namespace SkiaSharpControlV2.Tests.Helpers;

public class SkFontFactoryTests
{
    [Fact]
    public void CreateSkFont_Normal_ReturnsFont()
    {
        var font = SkFontFactory.CreateSkFont("Arial", "Normal", 14f);

        Assert.NotNull(font);
        Assert.Equal(14f, font.Size);
    }

    [Fact]
    public void CreateSkFont_Bold_ReturnsFont()
    {
        var font = SkFontFactory.CreateSkFont("Arial", "Bold", 12f);

        Assert.NotNull(font);
        Assert.Equal(12f, font.Size);
    }

    [Fact]
    public void CreateSkFont_Italic_ReturnsFont()
    {
        var font = SkFontFactory.CreateSkFont("Arial", "Italic", 16f);

        Assert.NotNull(font);
        Assert.Equal(16f, font.Size);
    }

    [Fact]
    public void CreateSkFont_BoldItalic_ReturnsFont()
    {
        var font = SkFontFactory.CreateSkFont("Arial", "BoldItalic", 10f);

        Assert.NotNull(font);
        Assert.Equal(10f, font.Size);
    }

    [Fact]
    public void CreateSkFont_UnknownStyle_DefaultsToNormal()
    {
        var font = SkFontFactory.CreateSkFont("Arial", "SomethingRandom", 12f);

        Assert.NotNull(font);
        Assert.Equal(12f, font.Size);
    }

    [Fact]
    public void CreateSkFont_CaseInsensitiveStyle()
    {
        var font1 = SkFontFactory.CreateSkFont("Arial", "BOLD", 12f);
        var font2 = SkFontFactory.CreateSkFont("Arial", "bold", 12f);

        Assert.NotNull(font1);
        Assert.NotNull(font2);
    }
}
