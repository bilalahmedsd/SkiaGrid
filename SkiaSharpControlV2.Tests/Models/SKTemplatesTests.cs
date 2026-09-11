

namespace SkiaSharpControlV2.Tests.Models;

public class SKSetterTests
{
    [Fact]
    public void Properties_CanBeSet()
    {
        var setter = new SKSetter
        {
            Property = SkStyleProperty.Background,
            Value = "#FF0000",
            ValuePath = "ChangeColor"
        };

        Assert.Equal(SkStyleProperty.Background, setter.Property);
        Assert.Equal("#FF0000", setter.Value);
        Assert.Equal("ChangeColor", setter.ValuePath);
    }

    [Fact]
    public void ValuePath_DefaultNull()
    {
        var setter = new SKSetter { Property = SkStyleProperty.Foreground };
        Assert.Null(setter.ValuePath);
    }
}

public class SKCellTemplateTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var template = new SKCellTemplate();

        Assert.Null(template.SkButton);
        Assert.NotNull(template.SkButtons);
        Assert.Empty(template.SkButtons);
        Assert.NotNull(template.Setters);
        Assert.Empty(template.Setters);
        Assert.NotNull(template.Triggers);
        Assert.Empty(template.Triggers);
    }

    [Fact]
    public void DrawButton_CanBeSet()
    {
        var template = new SKCellTemplate();
        Func<object, List<SkButton>> factory = obj => new List<SkButton>();
        template.DrawButton = factory;

        Assert.Same(factory, template.DrawButton);
    }

    [Fact]
    public void Setters_CanBePopulated()
    {
        var template = new SKCellTemplate();
        template.Setters.Add(new SKSetter { Property = SkStyleProperty.Background, Value = "#000" });
        template.Setters.Add(new SKSetter { Property = SkStyleProperty.Foreground, Value = "#FFF" });

        Assert.Equal(2, template.Setters.Count);
    }

    [Fact]
    public void Triggers_CanBePopulated()
    {
        var template = new SKCellTemplate();
        template.Triggers.Add(new SKDataTrigger
        {
            BindingPath = "Price",
            Operator = SKOperation.GreaterThan,
            Value = 0
        });

        Assert.Single(template.Triggers);
    }
}

public class SKRowTemplateTests
{
    [Fact]
    public void DefaultValues_HaveEmptyCollections()
    {
        var template = new SKRowTemplate();

        Assert.NotNull(template.Setters);
        Assert.Empty(template.Setters);
        Assert.NotNull(template.Triggers);
        Assert.Empty(template.Triggers);
    }
}

public class SKGroupCellTemplateTests
{
    [Fact]
    public void DefaultValues_HaveEmptyCollections()
    {
        var template = new SKGroupCellTemplate();

        Assert.NotNull(template.Setters);
        Assert.Empty(template.Setters);
        Assert.NotNull(template.Triggers);
        Assert.Empty(template.Triggers);
    }

    [Fact]
    public void Triggers_CanAddGroupTriggers()
    {
        var template = new SKGroupCellTemplate();
        template.Triggers.Add(new SkGroupDataTrigger
        {
            BindingPath = "Price",
            Aggregation = SkAggregation.Sum,
            Operator = SKOperation.GreaterThan,
            Value = 0
        });

        Assert.Single(template.Triggers);
    }
}
