

namespace SkiaSharpControlV2.Tests.Models;

public class SKBaseColumnTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var col = new SKGridViewColumn();

        Assert.Null(col.Name);
        Assert.Null(col.BindingPath);
        Assert.Equal(CellContentAlignment.Left, col.ContentAlignment);
        Assert.Null(col.CellTemplate);
        Assert.Null(col.Format);
        Assert.False(col.ShowBracketOnNegative);
        Assert.False(col.FormatWithAcronym);
        Assert.True(col.DataVisible);
    }

    [Fact]
    public void PropertyChanged_FiresOnNameSet()
    {
        var col = new SKGridViewColumn();
        string? changedProp = null;
        col.PropertyChanged += (s, e) => changedProp = e.PropertyName;

        col.Name = "TestCol";

        Assert.Equal("Name", changedProp);
        Assert.Equal("TestCol", col.Name);
    }

    [Fact]
    public void TriggerChanged_FiresPropertyChanged()
    {
        var col = new SKGridViewColumn();
        bool fired = false;
        col.PropertyChanged += (s, e) => fired = true;

        col.Width = 200;

        Assert.True(fired);
        Assert.Equal(200, col.Width);
    }
}

public class SKGridViewColumnTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var col = new SKGridViewColumn();

        Assert.Null(col.Header);
        Assert.True(col.IsVisible);
        Assert.True(col.CanUserResize);
        Assert.True(col.CanUserReorder);
        Assert.True(col.CanUserSort);
        Assert.Equal(SkGridViewColumnSort.None, col.GridViewColumnSort);
        Assert.Null(col.DisplayHeader);
        Assert.Equal(100.0, col.Width);
        Assert.Null(col.BackColor);
        Assert.Null(col.DisplayIndex);
        Assert.False(col.ShowSubTotalOnSort);
        Assert.True(col.ShowGroupAggregateData);
    }

    [Fact]
    public void Header_Set_FiresPropertyChanged()
    {
        var col = new SKGridViewColumn();
        string? changedProp = null;
        col.PropertyChanged += (s, e) => changedProp = e.PropertyName;

        col.Header = "Price";

        Assert.Equal("Header", changedProp);
    }

    [Fact]
    public void IsVisible_Set_FiresTriggerChanged()
    {
        var col = new SKGridViewColumn();
        bool fired = false;
        col.PropertyChanged += (s, e) => fired = true;

        col.IsVisible = false;

        Assert.True(fired);
        Assert.False(col.IsVisible);
    }

    [Fact]
    public void GridViewColumnSort_Set_FiresTriggerChanged()
    {
        var col = new SKGridViewColumn();
        bool fired = false;
        col.PropertyChanged += (s, e) => fired = true;

        col.GridViewColumnSort = SkGridViewColumnSort.Ascending;

        Assert.True(fired);
        Assert.Equal(SkGridViewColumnSort.Ascending, col.GridViewColumnSort);
    }

    [Fact]
    public void BackColor_Set_FiresTriggerChanged()
    {
        var col = new SKGridViewColumn();
        bool fired = false;
        col.PropertyChanged += (s, e) => fired = true;

        col.BackColor = "#FF0000";

        Assert.True(fired);
        Assert.Equal("#FF0000", col.BackColor);
    }

    [Fact]
    public void DisplayIndex_Set_FiresTriggerChanged()
    {
        var col = new SKGridViewColumn();
        bool fired = false;
        col.PropertyChanged += (s, e) => fired = true;

        col.DisplayIndex = 3;

        Assert.True(fired);
        Assert.Equal(3, col.DisplayIndex);
    }

    [Fact]
    public void AllPropertiesCanBeSet()
    {
        var col = new SKGridViewColumn
        {
            Header = "Test",
            BindingPath = "Price",
            Width = 150,
            ContentAlignment = CellContentAlignment.Right,
            Format = "N2",
            ShowBracketOnNegative = true,
            FormatWithAcronym = true,
            DataVisible = false,
            IsVisible = false,
            CanUserResize = false,
            CanUserReorder = false,
            CanUserSort = false,
            GridViewColumnSort = SkGridViewColumnSort.Descending,
            DisplayHeader = "Custom",
            BackColor = "#00FF00",
            DisplayIndex = 5,
            IsExpandableColumnForChildRows = true,
            ShowSubTotalOnSort = true,
            ShowGroupAggregateData = false
        };

        Assert.Equal("Test", col.Header);
        Assert.Equal("Price", col.BindingPath);
        Assert.Equal(150, col.Width);
        Assert.Equal(CellContentAlignment.Right, col.ContentAlignment);
        Assert.Equal("N2", col.Format);
        Assert.True(col.ShowBracketOnNegative);
        Assert.True(col.FormatWithAcronym);
        Assert.False(col.DataVisible);
        Assert.False(col.IsVisible);
        Assert.False(col.CanUserResize);
        Assert.False(col.CanUserReorder);
        Assert.False(col.CanUserSort);
        Assert.Equal(SkGridViewColumnSort.Descending, col.GridViewColumnSort);
        Assert.Equal("Custom", col.DisplayHeader);
        Assert.Equal("#00FF00", col.BackColor);
        Assert.Equal(5, col.DisplayIndex);
        Assert.True(col.IsExpandableColumnForChildRows);
        Assert.True(col.ShowSubTotalOnSort);
        Assert.False(col.ShowGroupAggregateData);
    }
}

public class SkGridColumnCollectionTests
{
    [Fact]
    public void IsListOfSKGridViewColumn()
    {
        var collection = new SkGridColumnCollection();
        collection.Add(new SKGridViewColumn { Header = "A" });
        collection.Add(new SKGridViewColumn { Header = "B" });

        Assert.Equal(2, collection.Count);
        Assert.Equal("A", collection[0].Header);
    }
}
