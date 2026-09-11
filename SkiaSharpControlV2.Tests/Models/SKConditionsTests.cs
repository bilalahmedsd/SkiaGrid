

namespace SkiaSharpControlV2.Tests.Models;

public class SKConditionTests
{
    [Fact]
    public void RequiredProperties_CanBeSet()
    {
        var cond = new SKCondition
        {
            BindingPath = "Price",
            Operator = SKOperation.GreaterThan,
            Value = 100
        };

        Assert.Equal("Price", cond.BindingPath);
        Assert.Equal(SKOperation.GreaterThan, cond.Operator);
        Assert.Equal(100, cond.Value);
    }

    [Fact]
    public void Binding_DP_CanBeSet()
    {
        var cond = new SKCondition
        {
            BindingPath = "X",
            Operator = SKOperation.Equals
        };
        cond.Binding = "BoundValue";

        Assert.Equal("BoundValue", cond.Binding);
    }
}

public class SKGroupConditionTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var cond = new SKGroupCondition();

        Assert.Null(cond.BindingPath); // nullable, unlike SKCondition's required
        Assert.Equal(SkAggregation.None, cond.Aggregation);
    }

    [Fact]
    public void AllPropertiesCanBeSet()
    {
        var cond = new SKGroupCondition
        {
            BindingPath = "Price",
            Aggregation = SkAggregation.Sum,
            Operator = SKOperation.LessThan,
            Value = 0
        };
        cond.Binding = "DirectValue";

        Assert.Equal("Price", cond.BindingPath);
        Assert.Equal(SkAggregation.Sum, cond.Aggregation);
        Assert.Equal(SKOperation.LessThan, cond.Operator);
        Assert.Equal(0, cond.Value);
        Assert.Equal("DirectValue", cond.Binding);
    }
}

public class GroupTriggerTests
{
    [Fact]
    public void SkGroupDataTrigger_PropertiesCanBeSet()
    {
        var trigger = new SkGroupDataTrigger
        {
            BindingPath = "Price",
            Aggregation = SkAggregation.Sum,
            Operator = SKOperation.GreaterThan,
            Value = 0
        };
        trigger.Binding = "DirectBinding";

        Assert.Equal("Price", trigger.BindingPath);
        Assert.Equal(SkAggregation.Sum, trigger.Aggregation);
        Assert.Equal(SKOperation.GreaterThan, trigger.Operator);
        Assert.Equal(0, trigger.Value);
        Assert.Equal("DirectBinding", trigger.Binding);
        Assert.NotNull(trigger.Setters);
        Assert.Empty(trigger.Setters);
    }

    [Fact]
    public void SKGroupMultiTrigger_PropertiesCanBeSet()
    {
        var trigger = new SKGroupMultiTrigger();
        trigger.Conditions.Add(new SKGroupCondition
        {
            BindingPath = "Price",
            Aggregation = SkAggregation.Sum,
            Operator = SKOperation.LessThan,
            Value = 0
        });

        Assert.Single(trigger.Conditions);
        Assert.NotNull(trigger.Setters);
    }

    [Fact]
    public void SkGroupDataTrigger_SettersCanBePopulated()
    {
        var trigger = new SkGroupDataTrigger
        {
            BindingPath = "Price",
            Aggregation = SkAggregation.Sum,
            Operator = SKOperation.GreaterThan,
            Value = 0
        };
        trigger.Setters.Add(new SKSetter { Property = SkStyleProperty.Foreground, Value = "#00FF00" });

        Assert.Single(trigger.Setters);
        Assert.Equal(SkStyleProperty.Foreground, trigger.Setters[0].Property);
    }
}

public class EnumTests
{
    [Theory]
    [InlineData(SkStyleProperty.Background)]
    [InlineData(SkStyleProperty.Foreground)]
    [InlineData(SkStyleProperty.BorderColor)]
    public void SkStyleProperty_AllValuesExist(SkStyleProperty value)
    {
        Assert.True(Enum.IsDefined(value));
    }

    [Theory]
    [InlineData(SkAggregation.None)]
    [InlineData(SkAggregation.Sum)]
    [InlineData(SkAggregation.Avg)]
    [InlineData(SkAggregation.Min)]
    [InlineData(SkAggregation.Max)]
    [InlineData(SkAggregation.Count)]
    [InlineData(SkAggregation.Distinct)]
    public void SkAggregation_AllValuesExist(SkAggregation value)
    {
        Assert.True(Enum.IsDefined(value));
    }

    [Theory]
    [InlineData(SKOperation.GreaterThan)]
    [InlineData(SKOperation.LessThan)]
    [InlineData(SKOperation.Equals)]
    [InlineData(SKOperation.NotEquals)]
    [InlineData(SKOperation.GreaterThanOrEqual)]
    [InlineData(SKOperation.LessThanOrEqual)]
    public void SKOperation_AllValuesExist(SKOperation value)
    {
        Assert.True(Enum.IsDefined(value));
    }
}
