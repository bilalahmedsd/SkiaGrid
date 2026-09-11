using SkiaSharpControlV2.Helpers;

namespace SkiaSharpControlV2.Tests.Helpers;

public class HelperReadCurrentItemTests
{
    private class TestObj
    {
        public string Name { get; set; } = "Test";
        public double Price { get; set; } = 99.5;
        public bool IsActive { get; set; } = true;
    }

    [Fact]
    public void ReadCurrentItemWithTypes_ValidProperty_ReturnsValueAndType()
    {
        var (value, type) = Helper.ReadCurrentItemWithTypes(new TestObj(), "Name");

        Assert.Equal("Test", value);
        Assert.Equal(typeof(string), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_NullItem_ReturnsNullAndVoid()
    {
        var (value, type) = Helper.ReadCurrentItemWithTypes(null!, "Name");

        Assert.Null(value);
        Assert.Equal(typeof(void), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_EmptyPropertyName_ReturnsNullAndVoid()
    {
        var (value, type) = Helper.ReadCurrentItemWithTypes(new TestObj(), "");

        Assert.Null(value);
        Assert.Equal(typeof(void), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_NonExistentProperty_ReturnsNullAndVoid()
    {
        var (value, type) = Helper.ReadCurrentItemWithTypes(new TestObj(), "Missing");

        Assert.Null(value);
        Assert.Equal(typeof(void), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_DoubleProperty_ReturnsStringValue()
    {
        var (value, type) = Helper.ReadCurrentItemWithTypes(new TestObj(), "Price");

        Assert.Equal("99.5", value);
        Assert.Equal(typeof(double), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_BoolProperty_ReturnsBoolType()
    {
        var (value, type) = Helper.ReadCurrentItemWithTypes(new TestObj(), "IsActive");

        Assert.Equal("True", value);
        Assert.Equal(typeof(bool), type);
    }
}
