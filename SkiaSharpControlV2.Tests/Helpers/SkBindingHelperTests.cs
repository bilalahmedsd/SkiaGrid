using SkiaSharpControlV2.Helpers;

namespace SkiaSharpControlV2.Tests.Helpers;

public class SkBindingHelperTests
{
    private class Inner
    {
        public string Value { get; set; } = "InnerValue";
    }

    private class Outer
    {
        public Inner Child { get; set; } = new();
        public string Name { get; set; } = "OuterName";
    }

    [Fact]
    public void GetNestedPropertyValue_SimpleProperty_ReturnsValue()
    {
        var obj = new Outer();
        var result = SkBindingHelper.GetNestedPropertyValue(obj, "Name");

        Assert.Equal("OuterName", result);
    }

    [Fact]
    public void GetNestedPropertyValue_NestedProperty_ReturnsValue()
    {
        var obj = new Outer();
        var result = SkBindingHelper.GetNestedPropertyValue(obj, "Child.Value");

        Assert.Equal("InnerValue", result);
    }

    [Fact]
    public void GetNestedPropertyValue_NullObject_ReturnsNull()
    {
        var result = SkBindingHelper.GetNestedPropertyValue(null!, "Name");

        Assert.Null(result);
    }

    [Fact]
    public void GetNestedPropertyValue_NonExistentProperty_ReturnsNull()
    {
        var obj = new Outer();
        var result = SkBindingHelper.GetNestedPropertyValue(obj, "Missing");

        Assert.Null(result);
    }

    [Fact]
    public void GetNestedPropertyValue_NullIntermediateProperty_ReturnsNull()
    {
        var obj = new Outer { Child = null! };
        var result = SkBindingHelper.GetNestedPropertyValue(obj, "Child.Value");

        Assert.Null(result);
    }

    [Fact]
    public void GetNestedPropertyValue_EmptyPath_ReturnsNull()
    {
        var obj = new Outer();
        var result = SkBindingHelper.GetNestedPropertyValue(obj, "");

        Assert.Null(result);
    }
}
