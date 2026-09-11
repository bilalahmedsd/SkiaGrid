using SkiaSharpControlV2.Helpers;

namespace SkiaSharpControlV2.Tests.Helpers;

public class HelperTests
{
    [Fact]
    public void GetColorBrush_ValidHex_ReturnsBrush()
    {
        var brush = Helper.GetColorBrush("#FF0000");
        Assert.NotNull(brush);
    }

    [Fact]
    public void GetColorBrush_NamedColor_ReturnsBrush()
    {
        var brush = Helper.GetColorBrush("Red");
        Assert.NotNull(brush);
    }

    [Fact]
    public void GetSystemDpi_ReturnsPositiveValue()
    {
        var dpi = Helper.GetSystemDpi();
        Assert.True(dpi > 0);
    }

    [Fact]
    public void IsFontInstalled_Arial_ReturnsTrue()
    {
        Assert.True(Helper.IsFontInstalled("Arial"));
    }

    [Fact]
    public void IsFontInstalled_NonExistentFont_ReturnsFalse()
    {
        Assert.False(Helper.IsFontInstalled("ThisFontDefinitelyDoesNotExist_XYZ_12345"));
    }

    [Theory]
    [InlineData(typeof(double), "123.456", "N2", false, false, "123.46")]
    [InlineData(typeof(int), "1000", "N0", false, false, "1,000")]
    [InlineData(typeof(double), "-50.5", "N1", true, false, "(50.5)")]
    public void ApplyFormat_NumericValues_FormatsCorrectly(Type type, string value, string format, bool showBracket, bool showAcronym, string expected)
    {
        var result = Helper.ApplyFormat(type, value, format, showBracket, showAcronym);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApplyFormat_WithAcronym_LargeNumber_ShowsK()
    {
        var result = Helper.ApplyFormat(typeof(double), "1500", "N0", false, true);
        Assert.Contains("K", result);
    }

    [Fact]
    public void ApplyFormat_WithAcronym_MillionNumber_ShowsM()
    {
        var result = Helper.ApplyFormat(typeof(double), "1500000", "N0", false, true);
        Assert.Contains("M", result);
    }

    [Fact]
    public void ApplyFormat_WithAcronym_BillionNumber_ShowsB()
    {
        var result = Helper.ApplyFormat(typeof(double), "1500000000", "N0", false, true);
        Assert.Contains("B", result);
    }

    [Fact]
    public void ApplyFormat_NullValue_ReturnsEmpty()
    {
        var result = Helper.ApplyFormat(typeof(double), null, "N2");
        Assert.Equal("", result);
    }

    [Fact]
    public void ApplyFormat_EmptyValue_ReturnsEmpty()
    {
        var result = Helper.ApplyFormat(typeof(string), "", "N2");
        Assert.Equal("", result);
    }

    [Fact]
    public void ApplyFormat_DateTimeType_FormatsDate()
    {
        var date = new DateTime(2024, 3, 15);
        var result = Helper.ApplyFormat(typeof(DateTime), date.ToString(), "MM/dd/yyyy");
        Assert.Contains("03/15/2024", result);
    }

    [Fact]
    public void ApplyFormat_NoFormat_ReturnsOriginalValue()
    {
        var result = Helper.ApplyFormat(typeof(string), "hello", "");
        Assert.Equal("hello", result);
    }
}
