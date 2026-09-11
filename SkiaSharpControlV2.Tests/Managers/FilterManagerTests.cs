

using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Managers;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SkiaSharpControlV2.Tests.Managers;

public class FilterTestItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public double Price { get; set; }
    public string Category { get; set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
}

public class FilterManagerTests
{
    [Fact]
    public void ColorConstants_Defined()
    {
        Assert.Equal("#0072C6", FilterManager.FILTER_COLOR);
        // #343434 since v2.14.0 (reference-terminal header background; was #3F3F3F).
        Assert.Equal("#FF343434", FilterManager.NORMAL_GRID_COLUMN_COLOR);
        Assert.Equal("#008040", FilterManager.TIMERBASED_SORTING_COLOR);
    }

    [Fact]
    public void AddOrUpdateFilter_NullCollectionView_DoesNotThrow()
    {
        var manager = CreateManager(collectionView: null);
        manager.AddOrUpdateFilter(new Filter { Column = "Name", FilterType = FilterType.Text, Text = "test" });
        // Should not throw
    }

    [Fact]
    public void RemoveFilter_NullCollectionView_DoesNotThrow()
    {
        var manager = CreateManager(collectionView: null);
        manager.RemoveFilter(new Filter { Column = "Name" });
        // Should not throw
    }

    [Fact]
    public void AddOrUpdateFilter_DelegatesToCollectionView()
    {
        var source = new ObservableCollection<FilterTestItem>
        {
            new() { Name = "A", Price = 100, Category = "Tech" },
            new() { Name = "B", Price = 200, Category = "Finance" },
            new() { Name = "C", Price = 300, Category = "Tech" }
        };
        var cv = new CustomCollectionView(source, new ReflectionHelper());

        bool scrollCalled = false;
        var manager = CreateManager(
            collectionView: cv,
            scrollToTop: () => scrollCalled = true);

        manager.AddOrUpdateFilter(new Filter
        {
            Column = "Category",
            FilterType = FilterType.List,
            List = new List<string> { "Tech" }
        });

        // CollectionView should now be filtered
        Assert.Equal(2, cv.Count);
        Assert.True(scrollCalled);
    }

    [Fact]
    public void RemoveFilter_RestoresAllItems()
    {
        var source = new ObservableCollection<FilterTestItem>
        {
            new() { Name = "A", Category = "Tech" },
            new() { Name = "B", Category = "Finance" }
        };
        var cv = new CustomCollectionView(source, new ReflectionHelper());
        var manager = CreateManager(collectionView: cv);

        var filter = new Filter { Column = "Category", FilterType = FilterType.List, List = new List<string> { "Tech" } };
        manager.AddOrUpdateFilter(filter);
        Assert.Equal(1, cv.Count);

        manager.RemoveFilter(filter);
        Assert.Equal(2, cv.Count);
    }

    [Fact]
    public void AddOrUpdateFilter_WithTimerSort_CallsUpdateTimerSortColor()
    {
        var source = new ObservableCollection<FilterTestItem> { new() { Name = "A" } };
        var cv = new CustomCollectionView(source, new ReflectionHelper());

        bool timerColorCalled = false;
        var manager = CreateManager(
            collectionView: cv,
            sortEvery: 5,
            updateTimerSortColor: (color, col) => timerColorCalled = true);

        manager.AddOrUpdateFilter(new Filter { Column = "Name", FilterType = FilterType.Text, Text = "A" });

        Assert.True(timerColorCalled);
    }

    [Fact]
    public void AddOrUpdateFilter_WithoutTimerSort_DoesNotCallTimerColor()
    {
        var source = new ObservableCollection<FilterTestItem> { new() { Name = "A" } };
        var cv = new CustomCollectionView(source, new ReflectionHelper());

        bool timerColorCalled = false;
        var manager = CreateManager(
            collectionView: cv,
            sortEvery: null,
            updateTimerSortColor: (color, col) => timerColorCalled = true);

        manager.AddOrUpdateFilter(new Filter { Column = "Name", FilterType = FilterType.Text, Text = "A" });

        Assert.False(timerColorCalled);
    }

    // Helper to create FilterManager without WPF dependencies
    // Column and DataListView params use null-returning lambdas since we can't test DataGrid in unit tests
    private static FilterManager CreateManager(
        ICustomCollectionView? collectionView = null,
        Action? scrollToTop = null,
        int? sortEvery = null,
        Action<string, SKGridViewColumn?>? updateTimerSortColor = null)
    {
        return new FilterManager(
            () => collectionView,
            () => new SkGridColumnCollection(), // empty columns — DataGrid header tests need WPF runtime
            () => null!, // DataGrid not available in unit tests
            scrollToTop ?? (() => { }),
            () => sortEvery,
            updateTimerSortColor ?? ((c, col) => { }));
    }
}
