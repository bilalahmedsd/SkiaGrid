namespace SkiaSharpControlV2.Tests.Models;

using SkiaSharpControlV2.Model;

public class GroupModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var model = new GroupModel();

        Assert.False(model.IsHeaderSubTotal);
        Assert.False(model.IsGroupSubTotal);
        Assert.False(model.IsGroupHeader);
        Assert.True(model.IsExpanded);
        Assert.Null(model.GroupName);
        Assert.Null(model.SubTotalGroupName);
        Assert.Null(model.BindingPath);
        Assert.Null(model.Item);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var item = new object();
        var model = new GroupModel
        {
            IsHeaderSubTotal = true,
            IsGroupSubTotal = true,
            IsGroupHeader = true,
            IsExpanded = false,
            GroupName = "TestGroup",
            SubTotalGroupName = "SubTotal",
            BindingPath = "Price",
            Item = item
        };

        Assert.True(model.IsHeaderSubTotal);
        Assert.True(model.IsGroupSubTotal);
        Assert.True(model.IsGroupHeader);
        Assert.False(model.IsExpanded);
        Assert.Equal("TestGroup", model.GroupName);
        Assert.Equal("SubTotal", model.SubTotalGroupName);
        Assert.Equal("Price", model.BindingPath);
        Assert.Same(item, model.Item);
    }
}

public class RowModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var model = new RowModel();

        Assert.False(model.IsChildRow);
        Assert.Null(model.Item);
        Assert.False(model.HasChild);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var item = new object();
        var model = new RowModel
        {
            IsChildRow = true,
            HasChild = true,
            Item = item
        };

        Assert.True(model.IsChildRow);
        Assert.True(model.HasChild);
        Assert.Same(item, model.Item);
    }
}
