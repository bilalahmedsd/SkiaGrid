
using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Managers;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SkiaSharpControlV2.Tests.Managers;

public class SortTestItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public double Price { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public class SortingManagerTests
{
    [Fact]
    public void ApplySort_DelegatesToCollectionView()
    {
        var source = new ObservableCollection<SortTestItem>
        {
            new() { Name = "C", Price = 300 },
            new() { Name = "A", Price = 100 },
            new() { Name = "B", Price = 200 }
        };
        var cv = new CustomCollectionView(source, new ReflectionHelper());
        bool resetCalled = false;

        var manager = CreateManager(cv, resetGroupToggle: () => resetCalled = true);
        manager.ApplySort("Name", ListSortDirection.Ascending);

        Assert.True(resetCalled);
        var names = cv._viewList.Cast<SortTestItem>().Select(x => x.Name).ToList();
        Assert.Equal(new[] { "A", "B", "C" }, names);
    }

    [Fact]
    public void ApplySort_Descending()
    {
        var source = new ObservableCollection<SortTestItem>
        {
            new() { Name = "A", Price = 100 },
            new() { Name = "C", Price = 300 },
            new() { Name = "B", Price = 200 }
        };
        var cv = new CustomCollectionView(source, new ReflectionHelper());
        var manager = CreateManager(cv);

        manager.ApplySort("Price", ListSortDirection.Descending);

        var prices = cv._viewList.Cast<SortTestItem>().Select(x => x.Price).ToList();
        Assert.Equal(new[] { 300.0, 200.0, 100.0 }, prices);
    }

    [Fact]
    public void ApplySort_ReEntrant_Prevented_B36()
    {
        var source = new ObservableCollection<SortTestItem> { new() { Name = "A" } };
        var cv = new CustomCollectionView(source, new ReflectionHelper());
        int sortCount = 0;

        // Create a manager where resetGroupToggle tries to re-enter ApplySort
        SkiaSharpControlV2.Managers.SortingManager mgr = null!;
        mgr = new SkiaSharpControlV2.Managers.SortingManager(
            () => cv,
            () => new SkGridColumnCollection(),
            () => null!,
            () => { sortCount++; /* simulate re-entrant call */ mgr.ApplySort("Name", ListSortDirection.Ascending); },
            () => null,
            () => null);

        mgr.ApplySort("Name", ListSortDirection.Ascending);

        // Should only have been called once (re-entrant call blocked)
        Assert.Equal(1, sortCount);
    }

    [Fact]
    public void ApplySort_NullCollectionView_DoesNotThrow()
    {
        var manager = CreateManager(null);
        manager.ApplySort("Name", ListSortDirection.Ascending);
        // Should not throw
    }

    [Fact]
    public void HandleSortEveryChanged_WithValue_StartsTimer()
    {
        var manager = CreateManager(null);
        // Just verifying no exception — timer behavior requires WPF dispatcher
        manager.HandleSortEveryChanged(5);
    }

    [Fact]
    public void HandleSortEveryChanged_NullValue_StopsTimer()
    {
        var manager = CreateManager(null);
        manager.HandleSortEveryChanged(null);
        // No exception
    }

    [Fact]
    public void HandleSortEveryChanged_ZeroValue_StopsTimer()
    {
        var manager = CreateManager(null);
        manager.HandleSortEveryChanged(0);
        // No exception
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var manager = CreateManager(null);
        manager.Dispose();
        manager.Dispose(); // Double dispose should not throw
    }

    // ── 3-state cycle: None tests ────────────────────────────────────────

    [Fact]
    public void ClearSort_RestoresSourceCollectionOrder()
    {
        var source = new ObservableCollection<SortTestItem>
        {
            new() { Name = "C", Price = 300 },
            new() { Name = "A", Price = 100 },
            new() { Name = "B", Price = 200 }
        };
        var cv = new CustomCollectionView(source, new ReflectionHelper());
        var manager = CreateManager(cv);

        manager.ApplySort("Name", ListSortDirection.Ascending);
        Assert.Equal(new[] { "A", "B", "C" },
            cv._viewList.Cast<SortTestItem>().Select(x => x.Name).ToArray());

        manager.ClearSort();

        // After ClearSort the view should be back in source insertion order.
        Assert.Equal(new[] { "C", "A", "B" },
            cv._viewList.Cast<SortTestItem>().Select(x => x.Name).ToArray());
    }

    [Fact]
    public void ClearSort_CallsResetGroupToggle()
    {
        var source = new ObservableCollection<SortTestItem> { new() { Name = "A" } };
        var cv = new CustomCollectionView(source, new ReflectionHelper());
        bool resetCalled = false;

        var manager = CreateManager(cv, resetGroupToggle: () => resetCalled = true);
        manager.ApplySort("Name", ListSortDirection.Ascending);
        resetCalled = false; // reset for second call observation

        manager.ClearSort();

        Assert.True(resetCalled);
    }

    [Fact]
    public void ClearSort_NullCollectionView_DoesNotThrow()
    {
        var manager = CreateManager(null);
        manager.ClearSort();
    }

    [Fact]
    public void ClearSort_AfterClear_LiveSortNoOpOnPropertyChange()
    {
        // With sort=None, _sorts is empty, so live-sort property notifications
        // must not reorder rows (the dirty-flag path checks _sorts.Any).
        var a = new SortTestItem { Name = "C", Price = 300 };
        var b = new SortTestItem { Name = "A", Price = 100 };
        var c = new SortTestItem { Name = "B", Price = 200 };
        var source = new ObservableCollection<SortTestItem> { a, b, c };
        var cv = new CustomCollectionView(source, new ReflectionHelper()) { IsLiveSort = true };
        var manager = CreateManager(cv);

        manager.ApplySort("Price", ListSortDirection.Ascending);
        manager.ClearSort();

        var orderBeforeMutation = cv._viewList.Cast<SortTestItem>().Select(x => x.Name).ToArray();
        // Even mutating the column we previously sorted on must not reorder.
        a.Price = -999;
        cv.RefreshIfDirty();
        var orderAfterMutation = cv._viewList.Cast<SortTestItem>().Select(x => x.Name).ToArray();

        Assert.Equal(orderBeforeMutation, orderAfterMutation);
    }

    [Fact]
    public void ClearSort_AfterClear_TimerRefreshKeepsSourceOrder()
    {
        // Timer-based sort tick calls cv.Refresh(). With _sorts empty, refresh
        // must produce source-collection order (no comparer applied).
        var source = new ObservableCollection<SortTestItem>
        {
            new() { Name = "C" }, new() { Name = "A" }, new() { Name = "B" }
        };
        var cv = new CustomCollectionView(source, new ReflectionHelper());
        var manager = CreateManager(cv);

        manager.ApplySort("Name", ListSortDirection.Ascending);
        manager.ClearSort();

        // Simulate a timer tick on a grid with no active sort.
        cv.Refresh();

        Assert.Equal(new[] { "C", "A", "B" },
            cv._viewList.Cast<SortTestItem>().Select(x => x.Name).ToArray());
    }

    [Fact]
    public void ClearSort_ReEntrant_Prevented()
    {
        var source = new ObservableCollection<SortTestItem> { new() { Name = "A" } };
        var cv = new CustomCollectionView(source, new ReflectionHelper());
        int clearCount = 0;

        SkiaSharpControlV2.Managers.SortingManager mgr = null!;
        mgr = new SkiaSharpControlV2.Managers.SortingManager(
            () => cv,
            () => new SkGridColumnCollection(),
            () => null!,
            () => { clearCount++; mgr.ClearSort(); /* try to re-enter */ },
            () => null,
            () => null);

        mgr.ClearSort();

        Assert.Equal(1, clearCount);
    }

    [Fact]
    public void SubscribeUnsubscribeTimer_DoesNotThrow()
    {
        var manager = CreateManager(null);
        manager.SubscribeTimer();
        manager.UnsubscribeTimer();
    }

    private static SortingManager CreateManager(
        ICustomCollectionView? cv,
        Action? resetGroupToggle = null)
    {
        return new SortingManager(
            () => cv,
            () => new SkGridColumnCollection(),
            () => null!,
            resetGroupToggle ?? (() => { }),
            () => null,
            () => null);
    }
}
