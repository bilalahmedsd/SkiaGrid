using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Managers;
using SkiaSharpControlV2.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SkiaSharpControlV2.Tests.Managers;

public class FlattenTestItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public List<FlattenTestItem>? Kids { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Item used for grouped-distinct-subtotal flatten tests (mirrors P1's Global Position
/// Summary shape: grouped by Account, Distinct HeaderField on InstrumentType, an Inst
/// column with ShowSubTotalOnSort=true).
/// </summary>
public class GroupFlattenItem : INotifyPropertyChanged
{
    public string Account { get; set; } = "";
    public string InstrumentType { get; set; } = "";
    public double Position { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// M2 perf-fix contract: FlattenRowItems reuses RowModel instances across flattens
/// (same source item → same wrapper object) so a full rebuild allocates ~nothing at
/// steady state — while producing exactly the same list contents as fresh allocation.
/// </summary>
public class DataFlatteningServiceTests
{
    private static (DataFlatteningService Svc, CustomCollectionView View, ObservableCollection<FlattenTestItem> Source) Setup(int count)
    {
        var source = new ObservableCollection<FlattenTestItem>();
        for (int i = 0; i < count; i++)
            source.Add(new FlattenTestItem { Name = $"Item {i}" });
        var helper = new ReflectionHelper();
        return (new DataFlatteningService(helper), new CustomCollectionView(source, helper), source);
    }

    private static Dictionary<object, (float x, float y, float height, float width)> Toggles() => new();

    [Fact]
    public void FlattenTwice_SameItems_ReusesRowModelInstances()
    {
        var (svc, view, _) = Setup(5);

        var first = svc.FlattenRowItems(view, Toggles());
        var second = svc.FlattenRowItems(view, Toggles());

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.Same(first[i], second[i]);          // pooled instance reused
            Assert.Same(first[i].Item, second[i].Item); // bound to the same source item
            Assert.False(second[i].IsChildRow);
            Assert.False(second[i].HasChild);
        }
    }

    [Fact]
    public void Flatten_AfterSourceAdd_ReusesExistingAndAllocatesOnlyNew()
    {
        var (svc, view, source) = Setup(3);

        var first = svc.FlattenRowItems(view, Toggles());
        var added = new FlattenTestItem { Name = "NEW" };
        source.Add(added);
        var second = svc.FlattenRowItems(view, Toggles());

        Assert.Equal(4, second.Count);
        // The 3 pre-existing rows keep their pooled RowModel instances...
        for (int i = 0; i < 3; i++)
            Assert.Same(first[i], second[i]);
        // ...and only the new row gets a fresh wrapper.
        Assert.Same(added, second[3].Item);
        Assert.DoesNotContain(second[3], first);
    }

    [Fact]
    public void Flatten_AfterSourceRemove_DroppedItemNotInResult()
    {
        var (svc, view, source) = Setup(3);

        svc.FlattenRowItems(view, Toggles());
        var removed = source[1];
        source.Remove(removed);
        var second = svc.FlattenRowItems(view, Toggles());

        Assert.Equal(2, second.Count);
        Assert.DoesNotContain(second, rm => ReferenceEquals(rm.Item, removed));
    }

    [Fact]
    public void Flatten_TreeMode_ChildRowsInterleavedAndReused()
    {
        var child = new FlattenTestItem { Name = "child" };
        var parent = new FlattenTestItem { Name = "parent", Kids = new List<FlattenTestItem> { child } };
        var source = new ObservableCollection<FlattenTestItem> { parent };
        var helper = new ReflectionHelper();
        var svc = new DataFlatteningService(helper);
        var view = new CustomCollectionView(source, helper);
        var expanded = new Dictionary<object, bool> { [parent] = true };

        var first = svc.FlattenRowItems(view, Toggles(), "Kids", expanded);
        var second = svc.FlattenRowItems(view, Toggles(), "Kids", expanded);

        // Parent row + expanded child row, flags intact, instances reused.
        Assert.Equal(2, first.Count);
        Assert.True(first[0].HasChild);
        Assert.False(first[0].IsChildRow);
        Assert.True(first[1].IsChildRow);
        Assert.Same(first[0], second[0]);
        Assert.Same(first[1], second[1]);
    }

    // ── Grouped distinct-subtotal placement (GPS "double subtotal in group 2" bug) ──
    //
    // Mirrors P1's Global Position Summary: grouped by Account, a Distinct HeaderField on
    // InstrumentType, and an Inst column with ShowSubTotalOnSort=true that the user sorts by.
    // Expected layout:
    //   [grand-total (IsHeaderSubTotal) rows — ONCE, at the very top, aggregate of everything]
    //   for each group: group header → its own IsGroupSubTotal rows → data rows
    private static Dictionary<string, (bool IsExpended, float x, float y, float height, float width)> GroupToggles() => new();

    private static (DataFlatteningService Svc, CustomCollectionView View, SKGroupDefinition Group, SkGridColumnCollection Cols) SetupGroupedDistinct()
    {
        var source = new ObservableCollection<GroupFlattenItem>
        {
            new() { Account = "A", InstrumentType = "Equity",  Position = 10 },
            new() { Account = "A", InstrumentType = "Equity",  Position = 20 },
            new() { Account = "B", InstrumentType = "Equity",  Position = 30 },
            new() { Account = "B", InstrumentType = "Options", Position = 40 },
        };
        var helper = new ReflectionHelper();
        var view = new CustomCollectionView(source, helper);
        view.ApplyGroup("Account"); // → 2 groups: A (Equity), B (Equity, Options)

        var group = new SKGroupDefinition { GroupBy = "Account", Target = "GroupColumn" };
        group.HeaderFields.Add(new SKGroupField { BindingPath = "InstrumentType", Aggregation = SkAggregation.Distinct, TargetColumns = "InstCol" });
        group.HeaderFields.Add(new SKGroupField { BindingPath = "Position", Aggregation = SkAggregation.Sum, TargetColumns = "PosCol" });

        var cols = new SkGridColumnCollection();
        cols.Add(new SKGridViewColumn { Name = "GroupColumn", BindingPath = "Account" });
        cols.Add(new SKGridViewColumn { Name = "InstCol", BindingPath = "InstrumentType", ShowSubTotalOnSort = true, GridViewColumnSort = SkGridViewColumnSort.Ascending });
        cols.Add(new SKGridViewColumn { Name = "PosCol", BindingPath = "Position" });

        return (new DataFlatteningService(helper), view, group, cols);
    }

    [Fact]
    public void FlattenGrouped_DistinctSubtotal_GrandTotalsEmittedOnceAtTop_NotPerGroup()
    {
        var (svc, view, group, cols) = SetupGroupedDistinct();

        var result = svc.FlattenGroupedItems(view, GroupToggles(), group, cols);

        int firstGroupHeaderIdx = result.FindIndex(r => r.IsGroupHeader);
        Assert.True(firstGroupHeaderIdx >= 0, "expected at least one group header");

        var headerSubtotals = result
            .Select((r, i) => (Row: r, Index: i))
            .Where(t => t.Row.IsHeaderSubTotal)
            .ToList();

        // Grand totals = one row per distinct instrument value across ALL data (Equity, Options).
        Assert.Equal(2, headerSubtotals.Count);
        Assert.Equal(new[] { "Equity", "Options" },
            headerSubtotals.Select(t => t.Row.GroupName).OrderBy(x => x).ToArray());

        // Every grand-total row must sit BEFORE the first group header. Before the fix the
        // per-group loop re-emitted them at the top of group 2+, landing after group 1's header.
        Assert.All(headerSubtotals, t =>
            Assert.True(t.Index < firstGroupHeaderIdx,
                "grand-total row appeared after a group header (per-group duplication bug)"));
    }

    [Fact]
    public void FlattenGrouped_DistinctSubtotal_EachGroupHasExactlyOneSubtotalPerValue()
    {
        var (svc, view, group, cols) = SetupGroupedDistinct();

        var result = svc.FlattenGroupedItems(view, GroupToggles(), group, cols);

        // No grand-total (IsHeaderSubTotal) rows may appear once the group section starts.
        int firstGroupHeaderIdx = result.FindIndex(r => r.IsGroupHeader);
        Assert.DoesNotContain(result.Skip(firstGroupHeaderIdx), r => r.IsHeaderSubTotal);

        // Group A: distinct { Equity } → exactly one subtotal row.
        var aSubtotals = result.Where(r => r.IsGroupSubTotal && r.GroupName == "A").ToList();
        Assert.Single(aSubtotals);
        Assert.Equal("Equity", aSubtotals[0].SubTotalGroupName);

        // Group B: distinct { Equity, Options } → exactly one subtotal row per value.
        var bSubtotals = result.Where(r => r.IsGroupSubTotal && r.GroupName == "B").ToList();
        Assert.Equal(2, bSubtotals.Count);
        Assert.Equal(new[] { "Equity", "Options" },
            bSubtotals.Select(r => r.SubTotalGroupName).OrderBy(x => x).ToArray());
    }

    // ── ShowSubtotalsWhenCollapsed (opt-in) ─────────────────────────────────────────
    //
    // A group is "collapsed" at flatten time when its GroupToggleDetails entry is IsExpended=false
    // (DataFlatteningService reads that into the per-group isExpanded). The single shared visibility
    // predicate is "IsGroupHeader || IsExpanded", so a subtotal row with IsExpanded=true stays visible
    // (and index-counted) even while the group is collapsed; a data row with IsExpanded=false hides.

    [Fact]
    public void FlattenGrouped_ShowSubtotalsWhenCollapsed_KeepsSubtotalsVisible_HidesDataRows_WhenCollapsed()
    {
        var (svc, view, group, cols) = SetupGroupedDistinct();
        group.ShowSubtotalsWhenCollapsed = true; // opt-in

        // Collapse both groups via the toggle-details dictionary the flatten reads.
        var toggles = GroupToggles();
        toggles["A"] = (false, 0, 0, 0, 0);
        toggles["B"] = (false, 0, 0, 0, 0);

        var result = svc.FlattenGroupedItems(view, toggles, group, cols);

        // Group A's per-group subtotal(s) stay visible (IsExpanded=true) despite the collapse.
        var aSubtotals = result.Where(r => r.IsGroupSubTotal && r.GroupName == "A").ToList();
        Assert.Single(aSubtotals);
        Assert.All(aSubtotals, r =>
            Assert.True(r.IsExpanded, "subtotal must stay visible when ShowSubtotalsWhenCollapsed=true"));

        // ...but Group A's data rows are hidden (IsExpanded=false).
        var aDataRows = result.Where(r => r.Item != null && r.GroupName == "A").ToList();
        Assert.NotEmpty(aDataRows);
        Assert.All(aDataRows, r =>
            Assert.False(r.IsExpanded, "data rows must hide when the group is collapsed"));

        // Same for Group B (two instrument subtotals, both visible; data rows hidden).
        var bSubtotals = result.Where(r => r.IsGroupSubTotal && r.GroupName == "B").ToList();
        Assert.Equal(2, bSubtotals.Count);
        Assert.All(bSubtotals, r => Assert.True(r.IsExpanded));
        Assert.All(result.Where(r => r.Item != null && r.GroupName == "B"), r => Assert.False(r.IsExpanded));
    }

    [Fact]
    public void FlattenGrouped_Default_SubtotalsCollapseAwayWithDataRows()
    {
        var (svc, view, group, cols) = SetupGroupedDistinct();
        // group.ShowSubtotalsWhenCollapsed defaults to false → old behavior.

        var toggles = GroupToggles();
        toggles["A"] = (false, 0, 0, 0, 0);
        toggles["B"] = (false, 0, 0, 0, 0);

        var result = svc.FlattenGroupedItems(view, toggles, group, cols);

        // Default: a collapsed group hides BOTH its subtotal rows AND its data rows (IsExpanded=false).
        var aSubtotals = result.Where(r => r.IsGroupSubTotal && r.GroupName == "A").ToList();
        Assert.Single(aSubtotals);
        Assert.All(aSubtotals, r =>
            Assert.False(r.IsExpanded, "default: subtotal must collapse away with the data rows"));
        Assert.All(result.Where(r => r.Item != null && r.GroupName == "A"), r => Assert.False(r.IsExpanded));
    }
}
