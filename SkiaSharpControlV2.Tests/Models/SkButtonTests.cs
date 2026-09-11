

namespace SkiaSharpControlV2.Tests.Models;

public class SkButtonTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var btn = new SkButton();

        Assert.Null(btn.Text);
        Assert.Null(btn.Width);
        Assert.Null(btn.BackgroundColor);
        Assert.Null(btn.BorderColor);
        Assert.Null(btn.ForegroundColor);
        Assert.Null(btn.ImageSource);
        Assert.Equal(0.0, btn.MarginRight);
        Assert.Equal(0.0, btn.MarginLeft);
        Assert.Equal(CellContentAlignment.Center, btn.ContentAlignment);
    }

    [Fact]
    public void Name_SetToNull_ThrowsInvalidOperationException()
    {
        var btn = new SkButton();
        btn.Name = "Valid"; // set first

        Assert.Throws<InvalidOperationException>(() => btn.Name = null!);
    }

    [Fact]
    public void Name_CanBeSetToValidString()
    {
        var btn = new SkButton();
        btn.Name = "TestBtn";

        Assert.Equal("TestBtn", btn.Name);
    }

    [Fact]
    public void AllPropertiesCanBeSet()
    {
        bool clicked = false;
        var btn = new SkButton
        {
            Name = "OkBtn",
            Text = "OK",
            Width = 50,
            BackgroundColor = "#333",
            BorderColor = "#666",
            ForegroundColor = "#FFF",
            MarginLeft = 5,
            MarginRight = 3,
            ContentAlignment = CellContentAlignment.Left,
            OnClicked = (b, o) => clicked = true
        };

        Assert.Equal("OkBtn", btn.Name);
        Assert.Equal("OK", btn.Text);
        Assert.Equal(50.0, btn.Width);
        Assert.Equal("#333", btn.BackgroundColor);
        Assert.Equal("#666", btn.BorderColor);
        Assert.Equal("#FFF", btn.ForegroundColor);
        Assert.Equal(5.0, btn.MarginLeft);
        Assert.Equal(3.0, btn.MarginRight);
        Assert.Equal(CellContentAlignment.Left, btn.ContentAlignment);

        btn.OnClicked(btn, new object());
        Assert.True(clicked);
    }
}
