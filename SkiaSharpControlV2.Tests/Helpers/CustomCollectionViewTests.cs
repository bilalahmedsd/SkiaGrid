

using SkiaSharpControlV2.Helpers;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SkiaSharpControlV2.Tests.Helpers;

public class TestItem : INotifyPropertyChanged
{
    private string _name = "";
    private double _price;
    private string _category = "";
    private bool _isActive;

    public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }
    public double Price { get => _price; set { _price = value; OnPropertyChanged(nameof(Price)); } }
    public string Category { get => _category; set { _category = value; OnPropertyChanged(nameof(Category)); } }
    public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(nameof(IsActive)); } }
    public string? Account { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class CustomCollectionViewTests
{
    private ObservableCollection<TestItem> CreateSampleItems(int count = 5)
    {
        var items = new ObservableCollection<TestItem>();
        for (int i = 0; i < count; i++)
        {
            items.Add(new TestItem
            {
                Name = $"Item {i}",
                Price = (i + 1) * 100,
                Category = i % 2 == 0 ? "A" : "B",
                IsActive = i % 2 == 0
            });
        }
        return items;
    }

    #region Construction & Basic Properties

    [Fact]
    public void Constructor_WrapsSourceCollection()
    {
        var source = CreateSampleItems();
        var view = new CustomCollectionView(source, new ReflectionHelper());

        Assert.Equal(5, view.Count);
        Assert.Equal(5, view._viewList.Count);
    }

    [Fact]
    public void Constructor_CopiesAllItems()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        Assert.Equal(3, view._viewList.Count);
    }

    [Fact]
    public void Items_ReturnsCurrentItems()
    {
        var source = CreateSampleItems();
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var items = view.Items.Cast<object>().ToList();
        Assert.Equal(5, items.Count);
    }

    [Fact]
    public void GetEnumerator_IteratesViewList()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        int count = 0;
        foreach (var item in view)
            count++;

        Assert.Equal(3, count);
    }

    #endregion

    #region Source Collection Changes

    [Fact]
    public void SourceAdd_ItemAppearsInView()
    {
        var source = CreateSampleItems(2);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        source.Add(new TestItem { Name = "New", Price = 999 });

        Assert.Equal(3, view.Count);
    }

    [Fact]
    public void SourceRemove_ItemRemovedFromView()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        source.RemoveAt(0);

        Assert.Equal(2, view.Count);
    }

    [Fact]
    public void SourceClear_ViewCleared()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        source.Clear();

        Assert.Equal(0, view.Count);
    }

    [Fact]
    public void SourceAdd_FiresCollectionChanged()
    {
        var source = CreateSampleItems(2);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        bool fired = false;
        view.CollectionChanged += (s, e) => fired = true;

        source.Add(new TestItem { Name = "Trigger" });

        Assert.True(fired);
    }

    [Fact]
    public void SourceRemove_FiresCollectionChanged()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        bool fired = false;
        view.CollectionChanged += (s, e) => fired = true;

        source.RemoveAt(0);

        Assert.True(fired);
    }

    // ── Reset-event de-duplication (Q1) ─────────────────────────────
    // A source Reset/Move/Replace must raise exactly ONE CollectionChanged(Reset), not two.
    // Previously each branch raised its own Reset AND Refresh() raised one → two full re-flattens.

    [Fact]
    public void SourceClear_FiresExactlyOneReset()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        int resets = 0;
        view.CollectionChanged += (s, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) resets++; };

        source.Clear(); // ObservableCollection.Clear raises a source Reset

        Assert.Equal(1, resets);
    }

    [Fact]
    public void SourceMove_FiresExactlyOneReset()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        int resets = 0;
        view.CollectionChanged += (s, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) resets++; };

        source.Move(0, 2);

        Assert.Equal(1, resets);
    }

    [Fact]
    public void SourceReplace_FiresExactlyOneReset()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        int resets = 0;
        view.CollectionChanged += (s, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) resets++; };

        source[1] = new TestItem { Name = "Replaced", Price = 500 };

        Assert.Equal(1, resets);
    }

    [Fact]
    public void SourceReset_UnsubscribesStaleItems_NoLeak()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.IsLiveSort = true;
        view.AddSort("Price", ListSortDirection.Ascending);
        var stale = source[0];

        source.Clear();              // Reset → must unsubscribe all old items; Refresh clears IsDirty
        Assert.False(view.IsDirty);  // sanity: clean after the reset

        stale.Price = 99999;         // mutate a no-longer-viewed item's live-sort field
        Assert.False(view.IsDirty);  // if the stale item were still subscribed, this would flip true
    }

    [Fact]
    public void SourceReset_RebuildsViewFromSource()
    {
        // Guards removal of the explicit _viewList.Clear() in the Reset branch — Refresh() must
        // still rebuild _viewList from _source (empty after Clear, then repopulated).
        var source = CreateSampleItems(4);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        source.Clear();
        Assert.Equal(0, view.Count);
        Assert.Empty(view._viewList);

        source.Add(new TestItem { Name = "X", Price = 1 });
        source.Add(new TestItem { Name = "Y", Price = 2 });
        Assert.Equal(2, view.Count);
    }

    #endregion

    #region Range API (Q5)

    [Fact]
    public void InsertRange_NoSortFilterGroup_RaisesSingleAdd()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var events = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (s, e) => events.Add(e);

        var batch = new List<object> { new TestItem { Name = "N1" }, new TestItem { Name = "N2" } };
        view.InsertRange(1, batch);

        var adds = events.Where(e => e.Action == NotifyCollectionChangedAction.Add).ToList();
        Assert.Single(adds);                       // ONE raise, not two
        Assert.Equal(2, adds[0].NewItems!.Count);   // carrying K items
        Assert.Equal(1, adds[0].NewStartingIndex);  // at the mapped view index
        Assert.Equal(5, view.Count);
        Assert.Equal(5, source.Count);
        Assert.Same(batch[0], view._viewList[1]);
        Assert.Same(batch[1], view._viewList[2]);
    }

    [Fact]
    public void InsertRange_WithActiveSort_DegradesToPerItem()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddSort("Price", ListSortDirection.Ascending);

        int addCount = 0;
        view.CollectionChanged += (s, e) => { if (e.Action == NotifyCollectionChangedAction.Add) addCount++; };

        var batch = new List<object>
        {
            new TestItem { Name = "Mid", Price = 150 },
            new TestItem { Name = "Hi",  Price = 250 },
        };
        view.InsertRange(0, batch);

        Assert.Equal(2, addCount);   // degraded: one Add per item (not a single batch raise)
        Assert.Equal(5, view.Count);
        Assert.Contains(batch[0], view._viewList);
        Assert.Contains(batch[1], view._viewList);
    }

    [Fact]
    public void InsertRange_WithActiveFilter_ExcludesFilteredItemsFromView()
    {
        var source = CreateSampleItems(3); // Prices 100,200,300
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddValueFilter("Price", ">", "250", typeof(double)); // only 300 currently passes

        var batch = new List<object>
        {
            new TestItem { Name = "Pass", Price = 400 },
            new TestItem { Name = "Fail", Price = 50 },
        };
        view.InsertRange(1, batch); // degrade path (filter active)

        Assert.Equal(2, view.Count);   // 300 + 400 (Fail excluded from the view)
        Assert.Equal(5, source.Count); // both items still added to the source
    }

    [Fact]
    public void RemoveRange_ContiguousBlock_RaisesSingleRemove()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var events = new List<NotifyCollectionChangedEventArgs>();
        view.CollectionChanged += (s, e) => events.Add(e);

        var block = new List<object> { source[1], source[2] }; // contiguous in view order
        view.RemoveRange(block);

        var removes = events.Where(e => e.Action == NotifyCollectionChangedAction.Remove).ToList();
        Assert.Single(removes);
        Assert.Equal(2, removes[0].OldItems!.Count);
        Assert.Equal(1, removes[0].OldStartingIndex);
        Assert.Equal(3, view.Count);
        Assert.Equal(3, source.Count);
    }

    [Fact]
    public void RemoveRange_ScatteredSet_DegradesToPerItem()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        int removeCount = 0;
        view.CollectionChanged += (s, e) => { if (e.Action == NotifyCollectionChangedAction.Remove) removeCount++; };

        var scattered = new List<object> { source[0], source[3] }; // non-contiguous
        view.RemoveRange(scattered);

        Assert.Equal(2, removeCount); // degraded: one Remove per item
        Assert.Equal(3, view.Count);
    }

    [Fact]
    public void InsertRange_SourceConsistent_AfterSubsequentRefresh()
    {
        var source = CreateSampleItems(2);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var batch = new List<object> { new TestItem { Name = "A" }, new TestItem { Name = "B" } };
        view.InsertRange(1, batch);
        Assert.Equal(4, view.Count);

        view.Refresh(); // rebuilds _viewList from _source — must neither drop nor duplicate
        Assert.Equal(4, view.Count);
        Assert.Equal(4, source.Count);
    }

    [Fact]
    public void RemoveRange_UnsubscribesRemovedItems_NoLeak()
    {
        var source = CreateSampleItems(3); // Prices 100,200,300
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.IsLiveSort = true;
        view.AddSort("Price", ListSortDirection.Ascending); // view order 100,200,300

        var block = new List<object> { source[0], source[1] }; // 100,200 → contiguous at view 0
        view.RemoveRange(block);
        Assert.False(view.IsDirty); // sanity: clean after removal

        ((TestItem)block[0]).Price = 99999; // mutate a removed item's live-sort field
        Assert.False(view.IsDirty);         // if still subscribed (leak), this would flip true
    }

    #endregion

    #region Refresh Fast Path (Q4)

    [Fact]
    public void Refresh_NoFilterSortGroup_ViewMatchesSourceOrder()
    {
        // Fast path: with nothing applied, _viewList mirrors _source order exactly.
        var source = new ObservableCollection<TestItem>
        {
            new TestItem { Name = "C", Price = 3 },
            new TestItem { Name = "A", Price = 1 },
            new TestItem { Name = "B", Price = 2 },
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var names = view._viewList.Cast<TestItem>().Select(x => x.Name).ToList();
        Assert.Equal(new[] { "C", "A", "B" }, names);
        Assert.Equal(3, view.Count);
    }

    [Fact]
    public void Refresh_FastPath_ClearsStaleSortComparer()
    {
        var source = CreateSampleItems(3); // Prices 100, 200, 300
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddSort("Price", ListSortDirection.Descending); // 300, 200, 100
        view.ClearSortDescriptions();                        // → Refresh fast path nulls the comparer

        // With no active sort, a new arrival must APPEND (source order), not be sorted-placed by a
        // stale cached comparer.
        var newItem = new TestItem { Name = "Zed", Price = 999 };
        source.Add(newItem);

        Assert.Same(newItem, view._viewList.Cast<object>().Last());
    }

    [Fact]
    public void Refresh_WithFilter_StillFilters_FastPathNotTaken()
    {
        var source = CreateSampleItems(5); // Prices 100..500
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddValueFilter("Price", ">", "250", typeof(double)); // 300,400,500 pass

        Assert.Equal(3, view.Count);
    }

    #endregion

    #region Sorting

    [Fact]
    public void AddSort_SortsAscending()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "C", Price = 300 },
            new() { Name = "A", Price = 100 },
            new() { Name = "B", Price = 200 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddSort("Name", ListSortDirection.Ascending);

        var names = view._viewList.Cast<TestItem>().Select(x => x.Name).ToList();
        Assert.Equal(new[] { "A", "B", "C" }, names);
    }

    [Fact]
    public void AddSort_SortsDescending()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 100 },
            new() { Name = "C", Price = 300 },
            new() { Name = "B", Price = 200 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddSort("Price", ListSortDirection.Descending);

        var prices = view._viewList.Cast<TestItem>().Select(x => x.Price).ToList();
        Assert.Equal(new[] { 300.0, 200.0, 100.0 }, prices);
    }

    [Fact]
    public void ClearSortDescriptions_RemovesSort()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "C" },
            new() { Name = "A" },
            new() { Name = "B" }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddSort("Name", ListSortDirection.Ascending);
        view.ClearSortDescriptions();

        // After clearing sort, order depends on source order
        Assert.Equal(3, view.Count);
    }

    [Fact]
    public void LiveSort_InsertsMaintainsOrder()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 100 },
            new() { Name = "C", Price = 300 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.IsLiveSort = true;
        view.AddSort("Price", ListSortDirection.Ascending);

        source.Add(new TestItem { Name = "B", Price = 200 });

        var prices = view._viewList.Cast<TestItem>().Select(x => x.Price).ToList();
        Assert.Equal(new[] { 100.0, 200.0, 300.0 }, prices);
    }

    [Fact]
    public void SortedInsert_DecoupledFromIsLiveSort()
    {
        // With a sort active, NEW inserts must land at the sort position regardless of
        // IsLiveSort. IsLiveSort only controls property-mutation re-sort, not sorted-insert.
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 100 },
            new() { Name = "C", Price = 300 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.IsLiveSort = false;                              // ← live sort OFF
        view.AddSort("Price", ListSortDirection.Ascending);

        source.Add(new TestItem { Name = "B", Price = 200 }); // ← inserted via Add path

        // B (200) must land between A (100) and C (300) — NOT at the end.
        var prices = view._viewList.Cast<TestItem>().Select(x => x.Price).ToList();
        Assert.Equal(new[] { 100.0, 200.0, 300.0 }, prices);
    }

    [Fact]
    public void PSStartupScenario_BindingSetsSort_IncrementalBackendPushesLandAtSortPosition()
    {
        // SMOKE TEST for the user's Position Summary scenario:
        //  1. Open PS window. ItemsSource binds to an (initially small or empty)
        //     ObservableCollection<Account>.
        //  2. Column XAML has GridViewColumnSort="{Binding AcctSort}" -> Ascending.
        //  3. IsLiveSort is left at default false in the VM (the user's setup).
        //  4. Backend pushes accounts asynchronously via Add().
        // EXPECTATION: every pushed account lands at its correct alphabetical position
        // in the view — NOT at the bottom in arrival order.
        //
        // This test exercises the exact CollectionView contract the grid plumbing relies
        // on. It does NOT spin up a WPF window because the test project intentionally has
        // no WPF host infrastructure; the binding pipeline is OS-tested by WPF itself.

        var source = new ObservableCollection<TestItem>();  // backend will push into this
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.IsLiveSort = false;                            // matches user's setup

        // Step 1: sort applied (simulates the binding landing Ascending on the column
        // and chaining through ApplySort -> cv.AddSort).
        view.AddSort("Name", ListSortDirection.Ascending);

        // Step 2: backend pushes accounts in arrival (NOT sorted) order.
        var arrivalOrder = new[] { "Echo", "Alpha", "Delta", "Bravo", "Charlie" };
        foreach (var name in arrivalOrder)
            source.Add(new TestItem { Name = name, Price = 0 });

        // Step 3: every arriving row must have slotted into alphabetical position.
        var actual = view._viewList.Cast<TestItem>().Select(x => x.Name).ToArray();
        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo" }, actual);
    }

    [Fact]
    public void Insert_AtSpecificIndex_RespectsUserIndex_EvenWhenSortActive()
    {
        // The explicit-middle-insert escape hatch: source.Insert(N, item) where N is in
        // the middle of the collection must keep the new row at index N in the view,
        // even when a sort is active. (Without this, a "blank row for editing" pattern
        // breaks — an empty-named item would sort to the top instead of staying where
        // the user put it.)
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 1 },
            new() { Name = "C", Price = 3 },
            new() { Name = "E", Price = 5 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddSort("Name", ListSortDirection.Ascending);

        // Insert a blank-named row at source index 1 (middle).
        source.Insert(1, new TestItem { Name = "", Price = 99 });

        // The blank row must land at index 1 in the view (user's intent), NOT at the top
        // (where the empty string would sort).
        var names = view._viewList.Cast<TestItem>().Select(x => x.Name).ToList();
        Assert.Equal(new[] { "A", "", "C", "E" }, names);
    }

    [Fact]
    public void Insert_AtEndIndex_IsTreatedAsAppend_AndUsesSort()
    {
        // Boundary case: source.Insert(source.Count, item) is semantically the same as
        // Add(item). It must still apply the sort (consistent with the PS-startup
        // scenario where backend feeds drive a sorted view).
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 1 },
            new() { Name = "C", Price = 3 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddSort("Name", ListSortDirection.Ascending);

        // Insert at the end (equivalent to Add) → must sort, not just append.
        source.Insert(source.Count, new TestItem { Name = "B", Price = 2 });

        var names = view._viewList.Cast<TestItem>().Select(x => x.Name).ToList();
        Assert.Equal(new[] { "A", "B", "C" }, names);
    }

    [Fact]
    public void Insert_AtIndexZero_RespectsUserIndex_WhenSortActive()
    {
        // Specific edge case: Insert(0, item) is an explicit "I want this at the top".
        // Even though "top" is also where empty/low values would sort, this should still
        // count as an explicit-middle-insert (because 0 < source.Count - 1 once there
        // are at least 2 items).
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 1 },
            new() { Name = "B", Price = 2 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddSort("Name", ListSortDirection.Descending);   // "B" should be first if sort wins

        source.Insert(0, new TestItem { Name = "Z", Price = 99 });

        // User intent honored: Z lands at index 0 even though sort-desc would put it first
        // anyway. The point is the path is taken, not the position. Verified by inserting
        // an item that would NOT sort to position 0:
        source.Insert(1, new TestItem { Name = "A", Price = 1 });   // would sort to last in desc

        var names = view._viewList.Cast<TestItem>().Select(x => x.Name).ToList();
        // Indices respected → Z at 0, second A at 1, then original B, A in their sort order.
        Assert.Equal("Z", names[0]);
        Assert.Equal("A", names[1]);
    }

    [Fact]
    public void ViewList_IndexOf_FindsItemAfterSortedInsert()
    {
        // Backing contract for SkiaGridViewV2.IndexOfInView in the ungrouped case:
        // when sort is active and an item is appended via Add, _viewList.IndexOf
        // returns its sort position (which the grid uses to compute the visual row index).
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 100 },
            new() { Name = "C", Price = 300 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddSort("Name", ListSortDirection.Ascending);

        var newItem = new TestItem { Name = "B", Price = 200 };
        source.Add(newItem);

        // Source-index: 2 (end). View-index: 1 (between A and C).
        Assert.Equal(2, source.IndexOf(newItem));
        Assert.Equal(1, view._viewList.IndexOf(newItem));
    }

    [Fact]
    public void ViewList_IndexOf_ReturnsMinus1_WhenFilteredOut()
    {
        // Backing contract for SkiaGridViewV2.IndexOfInView when an item is added but
        // filtered out — must return -1 so the grid wrapper can report "not visible."
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 100 },
            new() { Name = "B", Price = 200 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddValueFilter("Price", ">", "150", typeof(double));

        // Filter is Price > 150. Add an item that does NOT pass.
        var newItem = new TestItem { Name = "C", Price = 50 };
        source.Add(newItem);

        // Item exists in source but not in view.
        Assert.Contains(newItem, source);
        Assert.Equal(-1, view._viewList.IndexOf(newItem));
    }

    [Fact]
    public void ExplicitViewIndex_PlacesItemAtRequestedPosition_BypassingSort()
    {
        // Backing contract for SkiaGridViewV2.InsertAtViewIndex when sort is active:
        // RequestExplicitViewIndex(viewIdx) + source.Add(item) must put `item` at view
        // position viewIdx, NOT at sort position.
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 1 },
            new() { Name = "B", Price = 2 },
            new() { Name = "C", Price = 3 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddSort("Name", ListSortDirection.Ascending);

        // Without the explicit override, "Z" with sort asc would land at the END (view idx 3).
        // Request view index 1 instead (between A and B).
        view.RequestExplicitViewIndex(1);
        source.Add(new TestItem { Name = "Z", Price = 99 });

        var names = view._viewList.Cast<TestItem>().Select(x => x.Name).ToList();
        Assert.Equal(new[] { "A", "Z", "B", "C" }, names);
    }

    [Fact]
    public void ExplicitViewIndex_PlacesItemAtRequestedPosition_BypassingFilter()
    {
        // Backing contract: even if the new item would normally be filtered out,
        // the explicit-view-index insert must show it at the requested position.
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 200 },
            new() { Name = "B", Price = 300 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddValueFilter("Price", ">", "150", typeof(double));

        // The new item has Price=50, would fail the filter. Explicit insert forces it.
        view.RequestExplicitViewIndex(0);
        source.Add(new TestItem { Name = "X", Price = 50 });

        var names = view._viewList.Cast<TestItem>().Select(x => x.Name).ToList();
        Assert.Equal(new[] { "X", "A", "B" }, names);
    }

    [Fact]
    public void ExplicitViewIndex_IsConsumedOnce()
    {
        // The override must clear after one use — subsequent inserts take the normal path.
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 1 },
            new() { Name = "C", Price = 3 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddSort("Name", ListSortDirection.Ascending);

        view.RequestExplicitViewIndex(0);
        source.Add(new TestItem { Name = "Z", Price = 99 });   // forced to view idx 0

        // Next add — no override; normal sort path.
        source.Add(new TestItem { Name = "B", Price = 2 });   // sort-asc → between A and C

        var names = view._viewList.Cast<TestItem>().Select(x => x.Name).ToList();
        Assert.Equal(new[] { "Z", "A", "B", "C" }, names);
    }

    // ── M2 contract: granular CollectionChanged events carry the VIEW index ─────
    // The grid uses the index to splice its flat row-model list incrementally
    // (TryIncrementalRowUpdate) instead of re-flattening all N rows per event.

    [Fact]
    public void SourceAdd_EventCarriesViewIndex_AtSortedPosition()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 1 },
            new() { Name = "C", Price = 3 }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddSort("Name", ListSortDirection.Ascending);

        int raisedIndex = -99;
        object? raisedItem = null;
        view.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                raisedIndex = e.NewStartingIndex;
                raisedItem = e.NewItems![0];
            }
        };

        var b = new TestItem { Name = "B", Price = 2 };
        source.Add(b);   // sorts between A and C → view index 1

        Assert.Same(b, raisedItem);
        Assert.Equal(1, raisedIndex);
        Assert.Equal(1, view._viewList.IndexOf(b)); // event index matches actual view position
    }

    [Fact]
    public void SourceRemove_EventCarriesViewIndex()
    {
        var a = new TestItem { Name = "A" };
        var b = new TestItem { Name = "B" };
        var c = new TestItem { Name = "C" };
        var source = new ObservableCollection<TestItem> { a, b, c };
        var view = new CustomCollectionView(source, new ReflectionHelper());

        int raisedIndex = -99;
        view.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
                raisedIndex = e.OldStartingIndex;
        };

        source.Remove(b); // was at view index 1

        Assert.Equal(1, raisedIndex);
        Assert.Equal(new[] { "A", "C" }, view._viewList.Cast<TestItem>().Select(x => x.Name).ToArray());
    }

    [Fact]
    public void FilterPath_AddRemove_EventsCarryViewIndex()
    {
        // Item leaving/entering the view via the incremental filter path must also
        // carry indices, since the grid splices from these events too.
        var a = new TestItem { Name = "A", Price = 200 };
        var b = new TestItem { Name = "B", Price = 300 };
        var source = new ObservableCollection<TestItem> { a, b };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddValueFilter("Price", ">", "150", typeof(double));

        int removeIdx = -99, addIdx = -99;
        view.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Remove) removeIdx = e.OldStartingIndex;
            if (e.Action == NotifyCollectionChangedAction.Add) addIdx = e.NewStartingIndex;
        };

        a.Price = 100;  // A (view index 0) drops out of the filter
        Assert.Equal(0, removeIdx);

        a.Price = 250;  // A passes again → appended at end of view
        Assert.Equal(view._viewList.Count - 1, addIdx);
        Assert.Equal(view._viewList.IndexOf(a), addIdx);
    }

    [Fact]
    public void SortedInsert_DecoupledFromIsLiveSort_DoesNotReorderOnPropertyChange()
    {
        // Counter-test: IsLiveSort=false must still prevent property-mutation reorder
        // even though sorted-insert now works. Sort-key change on an existing row should
        // NOT move the row in the view.
        var a = new TestItem { Name = "A", Price = 100 };
        var b = new TestItem { Name = "B", Price = 200 };
        var c = new TestItem { Name = "C", Price = 300 };
        var source = new ObservableCollection<TestItem> { a, b, c };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.IsLiveSort = false;
        view.AddSort("Price", ListSortDirection.Ascending);

        // Mutate b's Price so it would belong at the END if live-sorted.
        b.Price = 999;
        view.RefreshIfDirty();

        // With IsLiveSort=false, the row order is unchanged from before the mutation.
        var names = view._viewList.Cast<TestItem>().Select(x => x.Name).ToList();
        Assert.Equal(new[] { "A", "B", "C" }, names);
    }

    #endregion

    #region Filtering

    [Fact]
    public void AddOrUpdateFilter_TextFilter_FiltersItems()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddOrUpdateFilter(new Filter
        {
            Column = "Name",
            FilterType = FilterType.Text,
            Text = "Item 0"
        });

        Assert.Equal(1, view.Count);
    }

    [Fact]
    public void AddOrUpdateFilter_ListFilter_FiltersItems()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddOrUpdateFilter(new Filter
        {
            Column = "Category",
            FilterType = FilterType.List,
            List = new List<string> { "A" }
        });

        Assert.True(view.Count > 0);
        Assert.True(view._viewList.Cast<TestItem>().All(x => x.Category == "A"));
    }

    [Fact]
    public void AddOrUpdateFilter_ValueFilter_FiltersItems()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddOrUpdateFilter(new Filter
        {
            Column = "Price",
            FilterType = FilterType.Value,
            Value = (">", "300", typeof(double))
        });

        Assert.True(view._viewList.Cast<TestItem>().All(x => x.Price > 300));
    }

    [Fact]
    public void RemoveFilter_RestoresItems()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var filter = new Filter
        {
            Column = "Category",
            FilterType = FilterType.List,
            List = new List<string> { "A" }
        };

        view.AddOrUpdateFilter(filter);
        var filteredCount = view.Count;

        view.RemoveFilter(filter);
        Assert.Equal(5, view.Count);
        Assert.True(view.Count > filteredCount);
    }

    [Fact]
    public void AddOrUpdateFilter_UpdateExisting_ReplacesFilter()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddOrUpdateFilter(new Filter
        {
            Column = "Category",
            FilterType = FilterType.List,
            List = new List<string> { "A" }
        });

        var firstCount = view.Count;

        view.AddOrUpdateFilter(new Filter
        {
            Column = "Category",
            FilterType = FilterType.List,
            List = new List<string> { "B" }
        });

        var secondCount = view.Count;

        // Should have different counts since A and B are different categories
        Assert.True(view.Count > 0);
    }

    [Fact]
    public void FiltersProperty_ExposesActiveFilters()
    {
        var source = CreateSampleItems();
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddOrUpdateFilter(new Filter { Column = "Name", FilterType = FilterType.Text, Text = "test" });

        Assert.Single(view.Filters);
        Assert.Equal("Name", view.Filters[0].Column);
    }

    // ── Null/empty value filter edge cases ─────────────────────

    [Fact]
    public void TextFilter_NullProperty_ExcludesNullRows()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Account = "ACCT-001" },
            new() { Name = "B", Account = null },
            new() { Name = "C", Account = "ACCT-001" },
            new() { Name = "D", Account = "" },
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddOrUpdateFilter(new Filter { Column = "Account", FilterType = FilterType.Text, Text = "ACCT*" });

        // Only rows with non-null Account matching "ACCT*" should pass
        Assert.Equal(2, view.Count);
    }

    [Fact]
    public void ListFilter_NullProperty_ExcludesNullRows()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Account = "ACCT-001" },
            new() { Name = "B", Account = null },
            new() { Name = "C", Account = "ACCT-002" },
            new() { Name = "D", Account = null },
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddOrUpdateFilter(new Filter
        {
            Column = "Account",
            FilterType = FilterType.List,
            List = new List<string> { "ACCT-001" }
        });

        // Only "A" should pass — B and D are null, C is ACCT-002
        Assert.Equal(1, view.Count);
    }

    [Fact]
    public void ListFilter_NullProperty_IncludedWhenListContainsNull()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Account = "ACCT-001" },
            new() { Name = "B", Account = null },
            new() { Name = "C", Account = "ACCT-002" },
            new() { Name = "D", Account = null },
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddOrUpdateFilter(new Filter
        {
            Column = "Account",
            FilterType = FilterType.List,
            List = new List<string> { "ACCT-001", null! }
        });

        // A (ACCT-001) + B (null) + D (null) should pass
        Assert.Equal(3, view.Count);
    }

    [Fact]
    public void ValueFilter_NullProperty_ExcludesNullRows()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Account = "ACCT-001" },
            new() { Name = "B", Account = null },
            new() { Name = "C", Account = "ACCT-002" },
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddOrUpdateFilter(new Filter
        {
            Column = "Account",
            FilterType = FilterType.Value,
            Value = ("=", "ACCT-001", typeof(string))
        });

        // Only "A" should pass — B is null
        Assert.Equal(1, view.Count);
    }

    // ── Typed filter API tests ─────────────────────────────────

    [Fact]
    public void AddValueFilter_FiltersCorrectly()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddValueFilter("Price", ">", "300", typeof(double));

        Assert.True(view._viewList.Cast<TestItem>().All(x => x.Price > 300));
    }

    [Fact]
    public void AddTextFilter_FiltersCorrectly()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddTextFilter("Name", "Item 0");

        Assert.Equal(1, view.Count);
    }

    [Fact]
    public void AddListFilter_FiltersCorrectly()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddListFilter("Category", new List<string> { "A" });

        Assert.True(view.Count > 0);
        Assert.True(view._viewList.Cast<TestItem>().All(x => x.Category == "A"));
    }

    [Fact]
    public void RemoveFilter_ByColumnName_RestoresItems()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddTextFilter("Name", "Item 0");
        Assert.Equal(1, view.Count);

        view.RemoveFilter("Name");
        Assert.Equal(5, view.Count);
    }

    // ── Boolean filter + operator alias tests ──────────────────

    [Fact]
    public void ValueFilter_BooleanField_EqualsOperator()
    {
        var source = CreateSampleItems(6);
        // Items 0,2,4 have IsActive=true; 1,3,5 have IsActive=false
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddValueFilter("IsActive", "Equals", "True", typeof(bool));

        Assert.True(view.Count > 0);
        Assert.True(view._viewList.Cast<TestItem>().All(x => x.IsActive));
    }

    [Fact]
    public void ValueFilter_BooleanField_SymbolOperator()
    {
        var source = CreateSampleItems(6);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddValueFilter("IsActive", "=", "True", typeof(bool));

        Assert.True(view.Count > 0);
        Assert.True(view._viewList.Cast<TestItem>().All(x => x.IsActive));
    }

    [Fact]
    public void ValueFilter_NotEqualsOperator_Alias()
    {
        var source = CreateSampleItems(6);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddValueFilter("IsActive", "NotEquals", "True", typeof(bool));

        Assert.True(view.Count > 0);
        Assert.True(view._viewList.Cast<TestItem>().All(x => !x.IsActive));
    }

    [Fact]
    public void ValueFilter_GreaterThanOperator_Alias()
    {
        var source = CreateSampleItems(5);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddValueFilter("Price", "GreaterThan", "300", typeof(double));

        Assert.True(view._viewList.Cast<TestItem>().All(x => x.Price > 300));
    }

    [Fact]
    public void TextFilter_EmptyStringProperty_ExcludedByWildcard()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Account = "ACCT-001" },
            new() { Name = "B", Account = "" },
            new() { Name = "C", Account = "ACCT-002" },
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.AddOrUpdateFilter(new Filter { Column = "Account", FilterType = FilterType.Text, Text = "ACCT*" });

        // Only A and C pass — B has empty string
        Assert.Equal(2, view.Count);
    }

    #endregion

    #region Grouping

    [Fact]
    public void ApplyGroup_GroupsByProperty()
    {
        var source = CreateSampleItems(6);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.ApplyGroup("Category");

        Assert.NotNull(view.GroupList);
        Assert.True(view.GroupList.Count() > 0);
    }

    [Fact]
    public void ApplyGroup_GroupsHaveCorrectItems()
    {
        var source = CreateSampleItems(4);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.ApplyGroup("Category");

        var groups = view.GroupList.ToList();
        foreach (var group in groups)
        {
            Assert.True(group.Items.Count > 0);
            Assert.True(group.Items.Cast<TestItem>().All(x => x.Category == group.Key?.ToString()));
        }
    }

    [Fact]
    public void ClearGroup_RemovesGrouping()
    {
        var source = CreateSampleItems(4);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.ApplyGroup("Category");
        Assert.True(view.GroupList.Any());

        view.ClearGroup();
        Assert.False(view.GroupList.Any());
    }

    [Fact]
    public void ApplyGroup_WithSort_GroupItemsSorted()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "C", Price = 300, Category = "X" },
            new() { Name = "A", Price = 100, Category = "X" },
            new() { Name = "B", Price = 200, Category = "X" }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddSort("Name", ListSortDirection.Ascending);
        view.ApplyGroup("Category");

        var group = view.GroupList.First();
        var names = group.Items.Cast<TestItem>().Select(x => x.Name).ToList();
        Assert.Equal(new[] { "A", "B", "C" }, names);
    }

    #endregion

    #region Aggregation

    [Fact]
    public void GroupAggregation_Sum_CalculatesCorrectly()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 100, Category = "X" },
            new() { Name = "B", Price = 200, Category = "X" },
            new() { Name = "C", Price = 300, Category = "X" }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.GroupFields = new List<SKGroupField>
        {
            new() { BindingPath = "Price", TargetColumns = "Price", Aggregation = SkAggregation.Sum }
        };
        view.ApplyGroup("Category");

        var group = view.GroupList.First();
        Assert.True(group.Aggregates.ContainsKey("Price"));

        var sum = Convert.ToDecimal(group.Aggregates["Price"]);
        Assert.Equal(600m, sum);
    }

    [Fact]
    public void GroupAggregation_Count_CountsAllNonNull()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 100, Category = "X" },
            new() { Name = "B", Price = 200, Category = "X" }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.GroupFields = new List<SKGroupField>
        {
            new() { BindingPath = "Price", TargetColumns = "Price", Aggregation = SkAggregation.Count }
        };
        view.ApplyGroup("Category");

        var group = view.GroupList.First();
        Assert.Equal(2, Convert.ToInt32(group.Aggregates["Price"]));
    }

    [Fact]
    public void GroupAggregation_Distinct_JoinsUnique()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 100, Category = "X" },
            new() { Name = "B", Price = 200, Category = "X" },
            new() { Name = "A", Price = 300, Category = "X" }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.GroupFields = new List<SKGroupField>
        {
            new() { BindingPath = "Name", TargetColumns = "Name", Aggregation = SkAggregation.Distinct }
        };
        view.ApplyGroup("Category");

        var group = view.GroupList.First();
        var distinct = group.Aggregates["Name"]?.ToString();
        Assert.Contains("A", distinct!);
        Assert.Contains("B", distinct!);
        Assert.Contains("/", distinct!);
    }

    #endregion

    #region Row Manipulation

    [Fact]
    public void MoveRowUp_MovesItem()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var item = view._viewList[1];
        var result = view.MoveRowUp(item);

        Assert.True(result);
        Assert.Same(item, view._viewList[0]);
    }

    [Fact]
    public void MoveRowUp_FirstItem_ReturnsFalse()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var result = view.MoveRowUp(view._viewList[0]);

        Assert.False(result);
    }

    [Fact]
    public void MoveRowDown_MovesItem()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var item = view._viewList[0];
        var result = view.MoveRowDown(item);

        Assert.True(result);
        Assert.Same(item, view._viewList[1]);
    }

    [Fact]
    public void MoveRowDown_LastItem_ReturnsFalse()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var result = view.MoveRowDown(view._viewList[view.Count - 1]);

        Assert.False(result);
    }

    [Fact]
    public void InsertBlankRow_InsertsAtPosition()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var newItem = new TestItem { Name = "Blank" };
        var result = view.InsertBlankRow(view._viewList[1], newItem);

        Assert.True(result);
        Assert.Equal(4, view.Count);
    }

    [Fact]
    public void DeleteEmptyRow_RemovesItem()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        var item = view._viewList[1];
        var result = view.DeleteEmptyRow(item);

        Assert.True(result);
        Assert.Equal(2, view.Count);
    }

    #endregion

    #region Disposal

    [Fact]
    public void Dispose_ClearsCollections()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.Dispose();

        Assert.Equal(0, view.Count);
        Assert.Empty(view._viewList);
    }

    [Fact]
    public void Dispose_CanBeCalledTwice()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        view.Dispose();
        view.Dispose(); // Should not throw
    }

    #endregion

    #region CollectionChanged Events

    [Fact]
    public void Refresh_FiresResetEvent()
    {
        var source = CreateSampleItems(3);
        var view = new CustomCollectionView(source, new ReflectionHelper());

        NotifyCollectionChangedAction? action = null;
        view.CollectionChanged += (s, e) => action = e.Action;

        view.Refresh();

        Assert.Equal(NotifyCollectionChangedAction.Reset, action);
    }

    #endregion

    #region AddNewRowAtBottomInGroup

    [Fact]
    public void AddNewRowAtBottomInGroup_True_AddsAtBottom()
    {
        var source = new ObservableCollection<TestItem>
        {
            new() { Name = "A", Price = 100, Category = "X" },
            new() { Name = "B", Price = 200, Category = "X" }
        };
        var view = new CustomCollectionView(source, new ReflectionHelper());
        view.AddNewRowAtBottomInGroup = true;
        view.ApplyGroup("Category");

        source.Add(new TestItem { Name = "C", Price = 50, Category = "X" });

        var group = view.GroupList.First();
        var last = group.Items.Cast<TestItem>().Last();
        Assert.Equal("C", last.Name);
    }

    #endregion
}
