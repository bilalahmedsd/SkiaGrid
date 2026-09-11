
using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Model;
using SkiaSharpControlV2.Services;
using System.ComponentModel;

namespace SkiaSharpControlV2.Tests.Services;

public class ExportTestItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public double Price { get; set; }
    public string Category { get; set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
}

public class GridExportServiceTests
{
    private readonly GridExportService _service = new();
    private readonly ReflectionHelper _reflection = new();

    private List<SKGridViewColumn> CreateColumns()
    {
        return new List<SKGridViewColumn>
        {
            new() { Name = "Name", Header = "Name", BindingPath = "Name", Width = 100, IsVisible = true, DisplayIndex = 0 },
            new() { Name = "Price", Header = "Price", BindingPath = "Price", Width = 80, IsVisible = true, DisplayIndex = 1, Format = "N2" },
            new() { Name = "Category", Header = "Category", BindingPath = "Category", Width = 80, IsVisible = true, DisplayIndex = 2 }
        };
    }

    private List<RowModel> CreateFlatItems()
    {
        return new List<RowModel>
        {
            new() { Item = new ExportTestItem { Name = "AAPL", Price = 178.50, Category = "Tech" } },
            new() { Item = new ExportTestItem { Name = "MSFT", Price = 420.30, Category = "Tech" } },
            new() { Item = new ExportTestItem { Name = "JPM", Price = 198.00, Category = "Finance" } }
        };
    }

    #region Flat (non-grouped) export

    [Fact]
    public void Export_FlatItems_All_ReturnsHeaderAndAllRows()
    {
        var result = _service.Export(
            SKExportType.All, CreateColumns(), CreateFlatItems(),
            null, null, null, _reflection);

        Assert.Contains("Name\tPrice\tCategory", result);
        Assert.Contains("AAPL", result);
        Assert.Contains("MSFT", result);
        Assert.Contains("JPM", result);
    }

    [Fact]
    public void Export_FlatItems_Selected_ReturnsOnlySelected()
    {
        var items = CreateFlatItems();
        var selected = new List<object> { items[0].Item! }; // AAPL only

        var result = _service.Export(
            SKExportType.Selected, CreateColumns(), items,
            null, null, selected, _reflection);

        Assert.Contains("AAPL", result);
        Assert.DoesNotContain("MSFT", result);
        Assert.DoesNotContain("JPM", result);
    }

    [Fact]
    public void Export_FlatItems_IncludesFormattedPrices()
    {
        var result = _service.Export(
            SKExportType.All, CreateColumns(), CreateFlatItems(),
            null, null, null, _reflection);

        Assert.Contains("178.50", result);
        Assert.Contains("420.30", result);
    }

    [Fact]
    public void Export_NullColumns_ReturnsEmpty()
    {
        var result = _service.Export(
            SKExportType.All, null, CreateFlatItems(),
            null, null, null, _reflection);

        Assert.Equal("", result);
    }

    [Fact]
    public void Export_NullItems_ReturnsEmpty()
    {
        var result = _service.Export(
            SKExportType.All, CreateColumns(), null,
            null, null, null, _reflection);

        Assert.Equal("", result);
    }

    [Fact]
    public void Export_HiddenColumns_AreExcluded()
    {
        var columns = CreateColumns();
        columns[2].IsVisible = false; // Hide Category

        var result = _service.Export(
            SKExportType.All, columns, CreateFlatItems(),
            null, null, null, _reflection);

        Assert.Contains("Name\tPrice", result);
        Assert.DoesNotContain("Category", result);
    }

    [Fact]
    public void Export_ColumnsOrderedByDisplayIndex()
    {
        var columns = CreateColumns();
        columns[0].DisplayIndex = 2;
        columns[1].DisplayIndex = 0;
        columns[2].DisplayIndex = 1;

        var result = _service.Export(
            SKExportType.All, columns, CreateFlatItems(),
            null, null, null, _reflection);

        var headerLine = result.Split(Environment.NewLine)[0];
        Assert.Equal("Price\tCategory\tName", headerLine);
    }

    #endregion

    #region Grouped export

    [Fact]
    public void Export_GroupedItems_IncludesGroupHeaders()
    {
        var groupItems = new List<GroupModel>
        {
            new() { IsGroupHeader = true, GroupName = "Tech", IsExpanded = true },
            new() { Item = new ExportTestItem { Name = "AAPL", Price = 178.50, Category = "Tech" }, GroupName = "Tech", IsExpanded = true },
            new() { Item = new ExportTestItem { Name = "MSFT", Price = 420.30, Category = "Tech" }, GroupName = "Tech", IsExpanded = true }
        };

        var group = new SKGroupDefinition { GroupBy = "Category", Target = "Category" };

        var result = _service.Export(
            SKExportType.All, CreateColumns(), null,
            groupItems, group, null, _reflection);

        Assert.Contains("Tech", result);
        Assert.Contains("AAPL", result);
        Assert.Contains("MSFT", result);
    }

    [Fact]
    public void Export_GroupedItems_NormalRows_RespectDataVisible()
    {
        var columns = CreateColumns();
        columns[1].DataVisible = false; // Hide Price data

        var groupItems = new List<GroupModel>
        {
            new() { Item = new ExportTestItem { Name = "AAPL", Price = 178.50, Category = "Tech" }, GroupName = "Tech", IsExpanded = true }
        };

        var result = _service.Export(
            SKExportType.All, columns, null,
            groupItems, null, null, _reflection);

        Assert.Contains("AAPL", result);
        // Price column data should be empty since DataVisible is false
        var dataLine = result.Split(Environment.NewLine).Skip(1).First();
        var cells = dataLine.Split('\t');
        Assert.Equal("", cells[1]); // Price column is empty
    }

    [Fact]
    public void Export_GroupedItems_HeaderSubTotal_ShowsTotalPrefix()
    {
        var groupItems = new List<GroupModel>
        {
            new() { IsHeaderSubTotal = true, GroupName = "SomeTotal", BindingPath = "Category" }
        };

        var columns = CreateColumns();
        var group = new SKGroupDefinition { GroupBy = "Category", Target = "Category" };

        var result = _service.Export(
            SKExportType.All, columns, null,
            groupItems, group, null, _reflection);

        Assert.Contains("Total SomeTotal", result);
    }

    #endregion

    #region CalculateGroupAggregation

    [Fact]
    public void CalculateGroupAggregation_Sum_ReturnsCorrectValue()
    {
        var items = new List<GroupModel>
        {
            new() { Item = new ExportTestItem { Price = 100 } },
            new() { Item = new ExportTestItem { Price = 200 } },
            new() { Item = new ExportTestItem { Price = 300 } }
        };

        var result = GridExportService.CalculateGroupAggregation(items, _reflection, "Price", SkAggregation.Sum);
        Assert.Equal(600.0, result);
    }

    [Fact]
    public void CalculateGroupAggregation_Count_ReturnsCount()
    {
        var items = new List<GroupModel>
        {
            new() { Item = new ExportTestItem { Price = 100 } },
            new() { Item = new ExportTestItem { Price = 200 } }
        };

        var result = GridExportService.CalculateGroupAggregation(items, _reflection, "Price", SkAggregation.Count);
        Assert.Equal(2, result);
    }

    [Fact]
    public void CalculateGroupAggregation_Avg_ReturnsAverage()
    {
        var items = new List<GroupModel>
        {
            new() { Item = new ExportTestItem { Price = 100 } },
            new() { Item = new ExportTestItem { Price = 300 } }
        };

        var result = GridExportService.CalculateGroupAggregation(items, _reflection, "Price", SkAggregation.Avg);
        Assert.Equal(200.0, result);
    }

    [Fact]
    public void CalculateGroupAggregation_Min_ReturnsMin()
    {
        var items = new List<GroupModel>
        {
            new() { Item = new ExportTestItem { Price = 300 } },
            new() { Item = new ExportTestItem { Price = 100 } },
            new() { Item = new ExportTestItem { Price = 200 } }
        };

        var result = GridExportService.CalculateGroupAggregation(items, _reflection, "Price", SkAggregation.Min);
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void CalculateGroupAggregation_Max_ReturnsMax()
    {
        var items = new List<GroupModel>
        {
            new() { Item = new ExportTestItem { Price = 100 } },
            new() { Item = new ExportTestItem { Price = 300 } }
        };

        var result = GridExportService.CalculateGroupAggregation(items, _reflection, "Price", SkAggregation.Max);
        Assert.Equal(300.0, result);
    }

    [Fact]
    public void CalculateGroupAggregation_Distinct_JoinsUnique()
    {
        var items = new List<GroupModel>
        {
            new() { Item = new ExportTestItem { Name = "A" } },
            new() { Item = new ExportTestItem { Name = "B" } },
            new() { Item = new ExportTestItem { Name = "A" } }
        };

        var result = GridExportService.CalculateGroupAggregation(items, _reflection, "Name", SkAggregation.Distinct);
        Assert.Equal("A/B", result?.ToString());
    }

    [Fact]
    public void CalculateGroupAggregation_EmptyItems_ReturnsEmptyString()
    {
        var items = new List<GroupModel>();
        var result = GridExportService.CalculateGroupAggregation(items, _reflection, "Price", SkAggregation.Sum);
        Assert.Equal("", result);
    }

    [Fact]
    public void CalculateGroupAggregation_None_ReturnsNull()
    {
        var items = new List<GroupModel>
        {
            new() { Item = new ExportTestItem { Price = 100 } }
        };

        var result = GridExportService.CalculateGroupAggregation(items, _reflection, "Price", SkAggregation.None);
        Assert.Null(result);
    }

    #endregion
}
