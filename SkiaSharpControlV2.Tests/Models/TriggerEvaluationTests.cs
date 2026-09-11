
using SkiaSharpControlV2.Helpers;

namespace SkiaSharpControlV2.Tests.Models;

public class TriggerEvaluationTests
{
    private readonly ReflectionHelper _helper = new();

    private class TestData
    {
        public double Price { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public int Quantity { get; set; }
    }

    #region SKTrigger.EvaluateCondition (static)

    [Fact]
    public void EvaluateCondition_DoubleGreaterThan_True()
    {
        Assert.True(SKTrigger.EvaluateCondition("150", typeof(double), 100.0, SKOperation.GreaterThan));
    }

    [Fact]
    public void EvaluateCondition_DoubleGreaterThan_False()
    {
        Assert.False(SKTrigger.EvaluateCondition("50", typeof(double), 100.0, SKOperation.GreaterThan));
    }

    [Fact]
    public void EvaluateCondition_DoubleLessThan_True()
    {
        Assert.True(SKTrigger.EvaluateCondition("50", typeof(double), 100.0, SKOperation.LessThan));
    }

    [Fact]
    public void EvaluateCondition_DoubleEquals_True()
    {
        Assert.True(SKTrigger.EvaluateCondition("100", typeof(double), 100.0, SKOperation.Equals));
    }

    [Fact]
    public void EvaluateCondition_DoubleNotEquals_True()
    {
        Assert.True(SKTrigger.EvaluateCondition("50", typeof(double), 100.0, SKOperation.NotEquals));
    }

    [Fact]
    public void EvaluateCondition_DoubleGreaterThanOrEqual_Equal()
    {
        Assert.True(SKTrigger.EvaluateCondition("100", typeof(double), 100.0, SKOperation.GreaterThanOrEqual));
    }

    [Fact]
    public void EvaluateCondition_DoubleLessThanOrEqual_Equal()
    {
        Assert.True(SKTrigger.EvaluateCondition("100", typeof(double), 100.0, SKOperation.LessThanOrEqual));
    }

    [Fact]
    public void EvaluateCondition_NullLeftStr_ReturnsFalse()
    {
        Assert.False(SKTrigger.EvaluateCondition(null, typeof(double), 100.0, SKOperation.Equals));
    }

    [Fact]
    public void EvaluateCondition_NullType_ReturnsFalse()
    {
        Assert.False(SKTrigger.EvaluateCondition("100", null, 100.0, SKOperation.Equals));
    }

    [Fact]
    public void EvaluateCondition_BoolEquals_True()
    {
        Assert.True(SKTrigger.EvaluateCondition("True", typeof(bool), true, SKOperation.Equals));
    }

    [Fact]
    public void EvaluateCondition_StringEquals_True()
    {
        Assert.True(SKTrigger.EvaluateCondition("hello", typeof(string), "hello", SKOperation.Equals));
    }

    [Fact]
    public void EvaluateCondition_InvalidConversion_ReturnsFalse()
    {
        Assert.False(SKTrigger.EvaluateCondition("not_a_number", typeof(double), 100.0, SKOperation.Equals));
    }

    #endregion

    #region SKDataTrigger.Evaluate

    [Fact]
    public void SKDataTrigger_Evaluate_BindingPath_GreaterThan_True()
    {
        var trigger = new SKDataTrigger
        {
            BindingPath = "Price",
            Operator = SKOperation.GreaterThan,
            Value = 0
        };
        var data = new TestData { Price = 150 };

        Assert.True(trigger.Evaluate(data, _helper));
    }

    [Fact]
    public void SKDataTrigger_Evaluate_BindingPath_GreaterThan_False()
    {
        var trigger = new SKDataTrigger
        {
            BindingPath = "Price",
            Operator = SKOperation.GreaterThan,
            Value = 200
        };
        var data = new TestData { Price = 100 };

        Assert.False(trigger.Evaluate(data, _helper));
    }

    [Fact]
    public void SKDataTrigger_Evaluate_BoolEquals_True()
    {
        var trigger = new SKDataTrigger
        {
            BindingPath = "IsActive",
            Operator = SKOperation.Equals,
            Value = "True"
        };
        var data = new TestData { IsActive = true };

        Assert.True(trigger.Evaluate(data, _helper));
    }

    [Fact]
    public void SKDataTrigger_Evaluate_NullValue_ReturnsFalse()
    {
        var trigger = new SKDataTrigger
        {
            BindingPath = "Price",
            Operator = SKOperation.Equals,
            Value = null!
        };
        var data = new TestData { Price = 100 };

        Assert.False(trigger.Evaluate(data, _helper));
    }

    [Fact]
    public void SKDataTrigger_Evaluate_NonExistentPath_ReturnsFalse()
    {
        var trigger = new SKDataTrigger
        {
            BindingPath = "NonExistent",
            Operator = SKOperation.Equals,
            Value = "test"
        };
        var data = new TestData();

        Assert.False(trigger.Evaluate(data, _helper));
    }

    [Fact]
    public void SKDataTrigger_Evaluate_WithBinding_UsesBindingValue()
    {
        var trigger = new SKDataTrigger
        {
            BindingPath = null!,
            Operator = SKOperation.Equals,
            Value = "42"
        };
        trigger.Binding = 42;

        var data = new TestData();

        Assert.True(trigger.Evaluate(data, _helper));
    }

    #endregion

    #region SKMultiTrigger.Evaluate

    [Fact]
    public void SKMultiTrigger_AllConditionsTrue_ReturnsTrue()
    {
        var trigger = new SKMultiTrigger();
        trigger.Conditions.Add(new SKCondition
        {
            BindingPath = "Price",
            Operator = SKOperation.GreaterThan,
            Value = 50
        });
        trigger.Conditions.Add(new SKCondition
        {
            BindingPath = "IsActive",
            Operator = SKOperation.Equals,
            Value = "True"
        });

        var data = new TestData { Price = 100, IsActive = true };
        Assert.True(trigger.Evaluate(data, _helper));
    }

    [Fact]
    public void SKMultiTrigger_OneConditionFalse_ReturnsFalse()
    {
        var trigger = new SKMultiTrigger();
        trigger.Conditions.Add(new SKCondition
        {
            BindingPath = "Price",
            Operator = SKOperation.GreaterThan,
            Value = 200
        });
        trigger.Conditions.Add(new SKCondition
        {
            BindingPath = "IsActive",
            Operator = SKOperation.Equals,
            Value = "True"
        });

        var data = new TestData { Price = 100, IsActive = true };
        Assert.False(trigger.Evaluate(data, _helper));
    }

    [Fact]
    public void SKMultiTrigger_EmptyConditions_ReturnsTrue()
    {
        var trigger = new SKMultiTrigger();
        var data = new TestData();
        Assert.True(trigger.Evaluate(data, _helper));
    }

    [Fact]
    public void SKMultiTrigger_WithBinding_UsesBindingValue()
    {
        var trigger = new SKMultiTrigger();
        var condition = new SKCondition
        {
            BindingPath = null!,
            Operator = SKOperation.Equals,
            Value = "100"
        };
        condition.Binding = 100;
        trigger.Conditions.Add(condition);

        var data = new TestData();
        Assert.True(trigger.Evaluate(data, _helper));
    }

    [Fact]
    public void SKMultiTrigger_NullConditionValue_ReturnsFalse()
    {
        var trigger = new SKMultiTrigger();
        trigger.Conditions.Add(new SKCondition
        {
            BindingPath = "Price",
            Operator = SKOperation.Equals,
            Value = null!
        });

        var data = new TestData { Price = 100 };
        Assert.False(trigger.Evaluate(data, _helper));
    }

    #endregion
}
