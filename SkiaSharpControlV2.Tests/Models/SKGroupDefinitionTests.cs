

namespace SkiaSharpControlV2.Tests.Models;

public class SKGroupDefinitionTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var def = new SKGroupDefinition();

        Assert.Null(def.GroupBy);
        Assert.Null(def.Target);
        Assert.NotNull(def.HeaderFields);
        Assert.Null(def.ToggleSymbol);
        Assert.Null(def.GroupCellTemplate);
    }

    [Fact]
    public void PropertyChanged_FiresOnForegroundColorSet()
    {
        var def = new SKGroupDefinition();
        bool fired = false;
        def.PropertyChanged += (s, e) => fired = true;

        def.ForegroundColor = "#FFFFFF";

        Assert.True(fired);
    }

    [Fact]
    public void PropertyChanged_FiresOnRowBackgroundSet()
    {
        var def = new SKGroupDefinition();
        bool fired = false;
        def.PropertyChanged += (s, e) => fired = true;

        def.RowBackground = "#000000";

        Assert.True(fired);
    }

    [Fact]
    public void AllPropertiesCanBeSet()
    {
        var template = new SKGroupCellTemplate();
        var toggle = new SKGroupToggleSymbol { Expand = "[-]", Collapse = "[+]" };

        var def = new SKGroupDefinition
        {
            GroupBy = "Category",
            Target = "GroupColumn",
            ForegroundColor = "#AAAAAA",
            RowBackground = "#111111",
            GroupCellTemplate = template,
            ToggleSymbol = toggle
        };

        Assert.Equal("Category", def.GroupBy);
        Assert.Equal("GroupColumn", def.Target);
        Assert.Equal("#AAAAAA", def.ForegroundColor);
        Assert.Equal("#111111", def.RowBackground);
        Assert.Same(template, def.GroupCellTemplate);
        Assert.Same(toggle, def.ToggleSymbol);
    }
}

public class SKGroupFieldTests
{
    [Fact]
    public void RequiredProperties_CanBeSet()
    {
        var field = new SKGroupField
        {
            BindingPath = "Price",
            TargetColumns = "PriceCol",
            Aggregation = SkAggregation.Sum
        };

        Assert.Equal("Price", field.BindingPath);
        Assert.Equal("PriceCol", field.TargetColumns);
        Assert.Equal(SkAggregation.Sum, field.Aggregation);
    }

    [Fact]
    public void GroupCellTemplate_DefaultNull()
    {
        var field = new SKGroupField { BindingPath = "X", TargetColumns = "Y" };
        Assert.Null(field.GroupCellTemplate);
    }
}

public class SKGroupToggleSymbolTests
{
    [Fact]
    public void DefaultValues_AreNull()
    {
        var sym = new SKGroupToggleSymbol();

        Assert.Null(sym.TargetColumns);
        Assert.Null(sym.Expand);
        Assert.Null(sym.Collapse);
        Assert.Null(sym.ShowGroupDetail);
        Assert.Null(sym.BackgroundColor);
        Assert.Null(sym.ForegroundColor);
    }

    [Fact]
    public void AllPropertiesCanBeSet()
    {
        var sym = new SKGroupToggleSymbol
        {
            TargetColumns = "Toggle",
            Expand = "[-]",
            Collapse = "[+]",
            ShowGroupDetail = true,
            BackgroundColor = "#333333",
            ForegroundColor = "#FFFFFF"
        };

        Assert.Equal("Toggle", sym.TargetColumns);
        Assert.Equal("[-]", sym.Expand);
        Assert.Equal("[+]", sym.Collapse);
        Assert.True(sym.ShowGroupDetail);
        Assert.Equal("#333333", sym.BackgroundColor);
        Assert.Equal("#FFFFFF", sym.ForegroundColor);
    }
}
