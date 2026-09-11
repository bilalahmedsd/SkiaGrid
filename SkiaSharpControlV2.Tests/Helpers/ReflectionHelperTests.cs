using SkiaSharpControlV2.Helpers;

namespace SkiaSharpControlV2.Tests.Helpers;

public class ReflectionHelperTests
{
    private readonly ReflectionHelper _helper = new();

    private class TestItem
    {
        public string Name { get; set; } = "TestName";
        public double Price { get; set; } = 123.45;
        public int Quantity { get; set; } = 10;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = new DateTime(2024, 1, 15);
        public double? Score { get; set; }
        public NestedOrder? Order { get; set; }
    }

    private class NestedOrder
    {
        public string Symbol { get; set; } = "AAPL";
        public double Price { get; set; } = 150.0;
        public NestedDetail? Detail { get; set; }
    }

    private class NestedDetail
    {
        public string Exchange { get; set; } = "NYSE";
        public int Volume { get; set; } = 5000;
    }

    [Fact]
    public void ReadCurrentItemWithTypes_StringProperty_ReturnsValueAndType()
    {
        var item = new TestItem();
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "Name");

        Assert.Equal("TestName", value);
        Assert.Equal(typeof(string), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_DoubleProperty_ReturnsValueAndType()
    {
        var item = new TestItem();
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "Price");

        Assert.Equal("123.45", value);
        Assert.Equal(typeof(double), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_IntProperty_ReturnsValueAndType()
    {
        var item = new TestItem();
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "Quantity");

        Assert.Equal("10", value);
        Assert.Equal(typeof(int), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_BoolProperty_ReturnsValueAndType()
    {
        var item = new TestItem();
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "IsActive");

        Assert.Equal("True", value);
        Assert.Equal(typeof(bool), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_DateTimeProperty_ReturnsValueAndType()
    {
        var item = new TestItem();
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "CreatedAt");

        Assert.NotNull(value);
        Assert.Equal(typeof(DateTime), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_NullItem_ReturnsNulls()
    {
        var (value, type) = _helper.ReadCurrentItemWithTypes(null, "Name");

        Assert.Null(value);
        Assert.Null(type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_NonExistentProperty_ReturnsNulls()
    {
        var item = new TestItem();
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "NonExistent");

        Assert.Null(value);
        Assert.Null(type);
    }

    [Fact]
    public void GetPropValue_ReturnsRawValue()
    {
        var item = new TestItem();
        var value = _helper.GetPropValue(item, "Price");

        Assert.Equal(123.45, value);
    }

    [Fact]
    public void GetPropValue_StringProperty_ReturnsString()
    {
        var item = new TestItem();
        var value = _helper.GetPropValue(item, "Name");

        Assert.Equal("TestName", value);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_CachesGetter_SecondCallFaster()
    {
        var item = new TestItem();

        // First call compiles and caches the getter
        _helper.ReadCurrentItemWithTypes(item, "Price");

        // Second call should use cached getter
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "Price");

        Assert.Equal("123.45", value);
        Assert.Equal(typeof(double), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_DifferentTypes_CachesSeparately()
    {
        var item1 = new TestItem { Name = "A" };

        var (v1, _) = _helper.ReadCurrentItemWithTypes(item1, "Name");
        var (v2, _) = _helper.ReadCurrentItemWithTypes(item1, "Price");

        Assert.Equal("A", v1);
        Assert.Equal("123.45", v2);
    }

    // ── Dot-notation (nested property path) tests ───────────────────

    [Fact]
    public void ReadCurrentItemWithTypes_DotNotation_OneLevel()
    {
        var item = new TestItem { Order = new NestedOrder { Symbol = "MSFT", Price = 400.50 } };
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "Order.Symbol");

        Assert.Equal("MSFT", value);
        Assert.Equal(typeof(string), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_DotNotation_TwoLevels()
    {
        var item = new TestItem
        {
            Order = new NestedOrder { Detail = new NestedDetail { Exchange = "NASDAQ", Volume = 12000 } }
        };
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "Order.Detail.Exchange");

        Assert.Equal("NASDAQ", value);
        Assert.Equal(typeof(string), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_DotNotation_NumericProperty()
    {
        var item = new TestItem { Order = new NestedOrder { Price = 275.99 } };
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "Order.Price");

        Assert.Equal("275.99", value);
        Assert.Equal(typeof(double), type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_DotNotation_NullIntermediate_ReturnsNull()
    {
        var item = new TestItem { Order = null };
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "Order.Price");

        Assert.Null(value);
        Assert.Null(type);
    }

    [Fact]
    public void ReadCurrentItemWithTypes_DotNotation_InvalidSegment_ReturnsNull()
    {
        var item = new TestItem { Order = new NestedOrder() };
        var (value, type) = _helper.ReadCurrentItemWithTypes(item, "Order.NonExistent");

        Assert.Null(value);
        Assert.Null(type);
    }

    [Fact]
    public void GetPropValue_DotNotation_OneLevel()
    {
        var item = new TestItem { Order = new NestedOrder { Price = 99.50 } };
        var value = _helper.GetPropValue(item, "Order.Price");

        Assert.Equal(99.50, value);
    }

    [Fact]
    public void GetPropValue_DotNotation_TwoLevels()
    {
        var item = new TestItem
        {
            Order = new NestedOrder { Detail = new NestedDetail { Volume = 7500 } }
        };
        var value = _helper.GetPropValue(item, "Order.Detail.Volume");

        Assert.Equal(7500, value);
    }

    [Fact]
    public void GetPropValue_DotNotation_NullIntermediate_ReturnsNull()
    {
        var item = new TestItem { Order = null };
        var value = _helper.GetPropValue(item, "Order.Price");

        Assert.Null(value);
    }

    // ── Dotted-path segment cache (Q6) ──────────────────────────────

    [Fact]
    public void GetPropValue_DotNotation_RepeatedCalls_Consistent()
    {
        // Guards the path-segment cache: splitting the path once and reusing the array
        // must yield identical results across repeated resolutions.
        var item = new TestItem { Order = new NestedOrder { Detail = new NestedDetail { Volume = 4200 } } };

        for (int i = 0; i < 5; i++)
            Assert.Equal(4200, _helper.GetPropValue(item, "Order.Detail.Volume"));
    }

    [Fact]
    public void SetPropValue_DotNotation_SetsLeaf_AndDoesNotCorruptSegmentCache()
    {
        var item = new TestItem { Order = new NestedOrder { Symbol = "AAPL" } };

        _helper.SetPropValue(item, "Order.Symbol", "TSLA");
        Assert.Equal("TSLA", item.Order!.Symbol);

        // A subsequent read on the SAME path must still resolve correctly — proves the cached
        // segment array was not mutated by the setter's parts[..^1] / parts[^1] slicing.
        Assert.Equal("TSLA", _helper.GetPropValue(item, "Order.Symbol"));
    }

    // ── Typed comparison for sorting (Q3a) ─────────────────────────

    [Fact]
    public void GetTypedComparison_IntProperty_OrdersAscending()
    {
        var cmp = _helper.GetTypedComparison(typeof(TestItem), "Quantity");
        Assert.NotNull(cmp);
        var a = new TestItem { Quantity = 5 };
        var b = new TestItem { Quantity = 10 };
        Assert.True(cmp!(a, b) < 0);
        Assert.True(cmp(b, a) > 0);
        Assert.Equal(0, cmp(a, a));
    }

    [Fact]
    public void GetTypedComparison_DoubleProperty_Works()
    {
        var cmp = _helper.GetTypedComparison(typeof(TestItem), "Price")!;
        Assert.True(cmp(new TestItem { Price = 1.5 }, new TestItem { Price = 2.5 }) < 0);
    }

    [Fact]
    public void GetTypedComparison_StringProperty_Works()
    {
        var cmp = _helper.GetTypedComparison(typeof(TestItem), "Name")!;
        Assert.True(cmp(new TestItem { Name = "Apple" }, new TestItem { Name = "Banana" }) < 0);
    }

    [Fact]
    public void GetTypedComparison_DateTimeProperty_Works()
    {
        var cmp = _helper.GetTypedComparison(typeof(TestItem), "CreatedAt")!;
        var earlier = new TestItem { CreatedAt = new DateTime(2020, 1, 1) };
        var later = new TestItem { CreatedAt = new DateTime(2021, 1, 1) };
        Assert.True(cmp(earlier, later) < 0);
    }

    [Fact]
    public void GetTypedComparison_NullableProperty_NullsSortFirst()
    {
        var cmp = _helper.GetTypedComparison(typeof(TestItem), "Score")!;
        var withNull = new TestItem { Score = null };
        var withVal = new TestItem { Score = 1.0 };
        // Comparer<double?>.Default orders null before any value.
        Assert.True(cmp(withNull, withVal) < 0);
        Assert.True(cmp(withVal, withNull) > 0);
    }

    [Fact]
    public void GetTypedComparison_DotPath_ReturnsNull()
    {
        Assert.Null(_helper.GetTypedComparison(typeof(TestItem), "Order.Price"));
    }

    [Fact]
    public void GetTypedComparison_MissingProperty_ReturnsNull()
    {
        Assert.Null(_helper.GetTypedComparison(typeof(TestItem), "DoesNotExist"));
    }

    [Fact]
    public void ClearCache_ResetsCaches_ResolutionStillWorks()
    {
        var item = new TestItem { Order = new NestedOrder { Price = 12.5 } };
        Assert.Equal(12.5, _helper.GetPropValue(item, "Order.Price"));
        Assert.True(_helper.CacheCount > 0);

        _helper.ClearCache();
        Assert.Equal(0, _helper.CacheCount);

        // Re-resolution after clearing the getter + segment caches must still succeed.
        Assert.Equal(12.5, _helper.GetPropValue(item, "Order.Price"));
    }
}
