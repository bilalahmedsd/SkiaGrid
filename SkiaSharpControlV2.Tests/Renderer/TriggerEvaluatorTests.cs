
using SkiaSharpControlV2.Renderer;

namespace SkiaSharpControlV2.Tests.Renderer;

public class CompareValuesTests
{
    [Theory]
    [InlineData(100.0, 50.0, SKOperation.GreaterThan, true)]
    [InlineData(50.0, 100.0, SKOperation.GreaterThan, false)]
    [InlineData(100.0, 100.0, SKOperation.Equals, true)]
    [InlineData(100.0, 50.0, SKOperation.NotEquals, true)]
    [InlineData(50.0, 100.0, SKOperation.LessThan, true)]
    [InlineData(100.0, 100.0, SKOperation.GreaterThanOrEqual, true)]
    [InlineData(100.0, 100.0, SKOperation.LessThanOrEqual, true)]
    public void CompareValues_Numeric(object left, object right, SKOperation op, bool expected)
    {
        Assert.Equal(expected, TriggerEvaluator.CompareValues(left, right, op));
    }

    [Theory]
    [InlineData("True", "True", SKOperation.Equals, true)]
    [InlineData("True", "False", SKOperation.NotEquals, true)]
    [InlineData("True", "False", SKOperation.Equals, false)]
    [InlineData("False", "False", SKOperation.Equals, true)]
    public void CompareValues_Bool(object left, object right, SKOperation op, bool expected)
    {
        Assert.Equal(expected, TriggerEvaluator.CompareValues(left, right, op));
    }

    [Theory]
    [InlineData("abc", "abc", SKOperation.Equals, true)]
    [InlineData("ABC", "abc", SKOperation.Equals, true)] // case insensitive
    [InlineData("abc", "def", SKOperation.NotEquals, true)]
    [InlineData("b", "a", SKOperation.GreaterThan, true)]
    [InlineData("a", "b", SKOperation.LessThan, true)]
    public void CompareValues_String(object left, object right, SKOperation op, bool expected)
    {
        Assert.Equal(expected, TriggerEvaluator.CompareValues(left, right, op));
    }

    [Fact]
    public void CompareValues_NullLeft_ReturnsFalse()
    {
        Assert.False(TriggerEvaluator.CompareValues(null!, "100", SKOperation.Equals));
    }

    [Fact]
    public void CompareValues_NullRight_ReturnsFalse()
    {
        Assert.False(TriggerEvaluator.CompareValues("100", null!, SKOperation.Equals));
    }

    [Fact]
    public void CompareValues_BoolOperatorGreaterThan_ReturnsFalse()
    {
        // Bool only supports Equals/NotEquals
        Assert.False(TriggerEvaluator.CompareValues("True", "False", SKOperation.GreaterThan));
    }

    [Fact]
    public void CompareValues_MixedNumericStrings()
    {
        Assert.True(TriggerEvaluator.CompareValues("150", 100, SKOperation.GreaterThan));
        Assert.True(TriggerEvaluator.CompareValues(50, "100", SKOperation.LessThan));
    }
}

public class SetterResolverAggregationTests
{
    private readonly SkiaSharpControlV2.Helpers.ReflectionHelper _reflection = new();
    private readonly SetterResolver _resolver = new();

    private class TestItem { public double Price { get; set; } public string Name { get; set; } = ""; }

    private List<SkiaSharpControlV2.Model.GroupModel> MakeGroup(params TestItem[] items)
    {
        return items.Select(i => new SkiaSharpControlV2.Model.GroupModel { Item = i }).ToList();
    }

    [Fact]
    public void Sum() => Assert.Equal(600.0, _resolver.CalculateGroupAggregation(
        MakeGroup(new TestItem { Price = 100 }, new TestItem { Price = 200 }, new TestItem { Price = 300 }),
        _reflection, "Price", SkAggregation.Sum));

    [Fact]
    public void Avg() => Assert.Equal(200.0, _resolver.CalculateGroupAggregation(
        MakeGroup(new TestItem { Price = 100 }, new TestItem { Price = 300 }),
        _reflection, "Price", SkAggregation.Avg));

    [Fact]
    public void Min() => Assert.Equal(100.0, _resolver.CalculateGroupAggregation(
        MakeGroup(new TestItem { Price = 300 }, new TestItem { Price = 100 }),
        _reflection, "Price", SkAggregation.Min));

    [Fact]
    public void Max() => Assert.Equal(300.0, _resolver.CalculateGroupAggregation(
        MakeGroup(new TestItem { Price = 100 }, new TestItem { Price = 300 }),
        _reflection, "Price", SkAggregation.Max));

    [Fact]
    public void Count() => Assert.Equal(3, _resolver.CalculateGroupAggregation(
        MakeGroup(new TestItem { Price = 1 }, new TestItem { Price = 2 }, new TestItem { Price = 3 }),
        _reflection, "Price", SkAggregation.Count));

    [Fact]
    public void Distinct()
    {
        var result = _resolver.CalculateGroupAggregation(
            MakeGroup(new TestItem { Name = "A" }, new TestItem { Name = "B" }, new TestItem { Name = "A" }),
            _reflection, "Name", SkAggregation.Distinct);
        Assert.Equal("A/B", result?.ToString());
    }

    [Fact]
    public void None_ReturnsNull()
    {
        var result = _resolver.CalculateGroupAggregation(
            MakeGroup(new TestItem { Price = 100 }), _reflection, "Price", SkAggregation.None);
        Assert.Null(result);
    }

    [Fact]
    public void EmptyItems_ReturnsEmptyString()
    {
        var result = _resolver.CalculateGroupAggregation(
            new List<SkiaSharpControlV2.Model.GroupModel>(), _reflection, "Price", SkAggregation.Sum);
        Assert.Equal("", result);
    }
}
