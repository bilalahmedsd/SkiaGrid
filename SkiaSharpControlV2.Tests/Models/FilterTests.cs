


namespace SkiaSharpControlV2.Tests.Models;

public class FilterTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var filter = new Filter();

        Assert.Null(filter.Column);
        Assert.Equal(FilterType.Value, filter.FilterType); // Default enum value is 0 = Value
        Assert.Null(filter.Text);
        Assert.Null(filter.List);
    }

    [Fact]
    public void ValueTuple_CanBeSet()
    {
        var filter = new Filter
        {
            Column = "Price",
            FilterType = FilterType.Value,
            Value = (">", "100", typeof(double))
        };

        Assert.Equal("Price", filter.Column);
        Assert.Equal(FilterType.Value, filter.FilterType);
        Assert.Equal(">", filter.Value.Operator);
        Assert.Equal("100", filter.Value.Value);
        Assert.Equal(typeof(double), filter.Value.DataType);
    }

    [Fact]
    public void TextFilter_CanBeSet()
    {
        var filter = new Filter
        {
            Column = "Name",
            FilterType = FilterType.Text,
            Text = "Item*"
        };

        Assert.Equal("Name", filter.Column);
        Assert.Equal(FilterType.Text, filter.FilterType);
        Assert.Equal("Item*", filter.Text);
    }

    [Fact]
    public void ListFilter_CanBeSet()
    {
        var filter = new Filter
        {
            Column = "Status",
            FilterType = FilterType.List,
            List = new List<string> { "Open", "Part Fill" }
        };

        Assert.Equal("Status", filter.Column);
        Assert.Equal(FilterType.List, filter.FilterType);
        Assert.Equal(2, filter.List!.Count);
        Assert.Contains("Open", filter.List);
        Assert.Contains("Part Fill", filter.List);
    }

    [Fact]
    public void ToString_ReturnsNonNull()
    {
        var filter = new Filter { Column = "Price", FilterType = FilterType.Value };
        var result = filter.ToString();
        Assert.NotNull(result);
    }
}
