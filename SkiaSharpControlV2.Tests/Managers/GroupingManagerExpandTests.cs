using SkiaSharpControlV2.Helpers;
using SkiaSharpControlV2.Managers;
using SkiaSharpControlV2.Model;
using SkiaSharpControlV2.Renderer;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SkiaSharpControlV2.Tests.Managers;

public class ExpandTestItem : INotifyPropertyChanged
{
    public string Account { get; set; } = "";
    public string Symbol { get; set; } = "";
    public List<ExpandTestItem>? Legs { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Contract for the expand/collapse notification API (ExpandChanged / ExpandChangedCommand):
/// every state change reports the new state, the cause, and exactly the data rows that became
/// visible or hidden — the payload a per-row market-data subscription needs. Also covers the
/// tree-mode programmatic toggles (ToggleRow / SetAllRowsExpanded) and the state read APIs.
/// </summary>
public class GroupingManagerExpandTests
{
    private sealed class Harness
    {
        public GroupingManager Manager = null!;
        public SkiaRenderer Renderer = null!;
        public CustomCollectionView View = null!;
        public List<SkExpandChangedEventArgs> Events = new();
        public bool ListenersActive = true;
        public int RefreshCount;
    }

    private static Harness SetupGrouped()
    {
        var source = new ObservableCollection<ExpandTestItem>
        {
            new() { Account = "A", Symbol = "A1" },
            new() { Account = "A", Symbol = "A2" },
            new() { Account = "B", Symbol = "B1" },
        };
        var helper = new ReflectionHelper();
        var view = new CustomCollectionView(source, helper);
        view.ApplyGroup("Account");

        var h = new Harness { Renderer = new SkiaRenderer(helper), View = view };
        var group = new SKGroupDefinition { GroupBy = "Account", Target = "GroupColumn" };

        h.Manager = new GroupingManager(
            new DataFlatteningService(helper),
            () => h.View,
            () => group,
            () => null,
            () => h.Renderer,
            () => null,
            () => { },
            () => h.RefreshCount++);
        h.Manager.ExpandChangedCallback = e => h.Events.Add(e);
        h.Manager.ExpandListenersActive = () => h.ListenersActive;

        h.Manager.UpdateCollection(); // seeds GroupToggleDetails + GroupItemSource
        return h;
    }

    private static (Harness H, ExpandTestItem Parent, ExpandTestItem Leg1, ExpandTestItem Leg2) SetupTree()
    {
        var leg1 = new ExpandTestItem { Symbol = "LEG1" };
        var leg2 = new ExpandTestItem { Symbol = "LEG2" };
        var parent = new ExpandTestItem { Symbol = "SPREAD", Legs = new() { leg1, leg2 } };
        var source = new ObservableCollection<ExpandTestItem> { parent, new() { Symbol = "PLAIN" } };

        var helper = new ReflectionHelper();
        var h = new Harness { Renderer = new SkiaRenderer(helper), View = new CustomCollectionView(source, helper) };

        h.Manager = new GroupingManager(
            new DataFlatteningService(helper),
            () => h.View,
            () => null,          // tree mode → no GroupSettings
            () => null,
            () => h.Renderer,
            () => "Legs",
            () => { },
            () => h.RefreshCount++);
        h.Manager.ExpandChangedCallback = e => h.Events.Add(e);
        h.Manager.ExpandListenersActive = () => h.ListenersActive;

        h.Manager.UpdateCollection();
        return (h, parent, leg1, leg2);
    }

    // ── Grouped mode: read APIs ──────────────────────────────────────

    [Fact]
    public void GroupedMode_AfterFirstFlatten_AllGroupsReportExpanded()
    {
        var h = SetupGrouped();

        var states = h.Manager.GetGroupExpandStates();

        Assert.Equal(new[] { "A", "B" }, states.Keys.OrderBy(k => k).ToArray());
        Assert.All(states.Values, Assert.True);
        Assert.True(h.Manager.IsGroupExpanded("A"));
    }

    [Fact]
    public void IsGroupExpanded_UnknownGroupName_ReportsSeededDefaultTrue()
    {
        var h = SetupGrouped();

        Assert.True(h.Manager.IsGroupExpanded("NOT-A-GROUP"));
    }

    // ── Grouped mode: notifications ──────────────────────────────────

    [Fact]
    public void ToggleGroup_Collapse_NotifiesWithOnlyThatGroupsDataRows()
    {
        var h = SetupGrouped();

        h.Manager.ToggleGroup("A", false);

        var e = Assert.Single(h.Events);
        Assert.False(e.IsExpanded);
        Assert.Equal(SkExpandChangeReason.Api, e.Reason);
        Assert.Equal("A", e.GroupName);
        Assert.Null(e.ParentItem);
        Assert.False(e.IsBulk);
        // Group A's two data rows — never the header/subtotal wrappers, and nothing from group B.
        Assert.Equal(new[] { "A1", "A2" },
            e.AffectedItems.Cast<ExpandTestItem>().Select(i => i.Symbol).OrderBy(s => s).ToArray());
        Assert.False(h.Manager.IsGroupExpanded("A"));
        Assert.True(h.Manager.IsGroupExpanded("B"));
    }

    [Fact]
    public void ToggleGroup_ToStateItIsAlreadyIn_DoesNotNotify()
    {
        var h = SetupGrouped();

        h.Manager.ToggleGroup("A", true); // already expanded

        Assert.Empty(h.Events);
    }

    [Fact]
    public void ToggleGroup_CollapseThenExpand_NotifiesOncePerRealChange()
    {
        var h = SetupGrouped();

        h.Manager.ToggleGroup("A", false);
        h.Manager.ToggleGroup("A", false); // redundant
        h.Manager.ToggleGroup("A", true);

        Assert.Equal(2, h.Events.Count);
        Assert.False(h.Events[0].IsExpanded);
        Assert.True(h.Events[1].IsExpanded);
    }

    [Fact]
    public void UpdateGroupToggle_GlyphClick_NotifiesWithUserToggleReason()
    {
        var h = SetupGrouped();
        // Simulate the toggle glyph's hit-test rect the renderer would have published for group A.
        h.Manager.GroupToggleDetails["A"] = (true, 0, 0, 10, 10);

        h.Manager.UpdateGroupToggle(5, 5, totalRows: 10);

        var e = Assert.Single(h.Events);
        Assert.Equal(SkExpandChangeReason.UserToggle, e.Reason);
        Assert.Equal("A", e.GroupName);
        Assert.False(e.IsExpanded);
        Assert.Equal(2, e.AffectedItems.Count);
        Assert.False(h.Manager.IsGroupExpanded("A"));
    }

    [Fact]
    public void CollapseAll_RaisesOneBulkNotificationCoveringEveryDataRow()
    {
        var h = SetupGrouped();

        h.Manager.CollapseAll();

        var e = Assert.Single(h.Events); // ONE event, not one per group
        Assert.True(e.IsBulk);
        Assert.Equal(SkExpandChangeReason.CollapseAll, e.Reason);
        Assert.False(e.IsExpanded);
        Assert.Null(e.GroupName);
        Assert.Equal(3, e.AffectedItems.Count);
        Assert.All(h.Manager.GetGroupExpandStates().Values, Assert.False);
    }

    [Fact]
    public void ExpandAll_RaisesOneBulkNotification()
    {
        var h = SetupGrouped();
        h.Manager.CollapseAll();
        h.Events.Clear();

        h.Manager.ExpandAll();

        var e = Assert.Single(h.Events);
        Assert.True(e.IsBulk);
        Assert.Equal(SkExpandChangeReason.ExpandAll, e.Reason);
        Assert.True(e.IsExpanded);
        Assert.All(h.Manager.GetGroupExpandStates().Values, Assert.True);
    }

    [Fact]
    public void NoListener_NeverNotifies_AndStillTogglesState()
    {
        var h = SetupGrouped();
        h.ListenersActive = false;

        h.Manager.ToggleGroup("A", false);
        h.Manager.CollapseAll();

        Assert.Empty(h.Events);                        // no payload materialized
        Assert.False(h.Manager.IsGroupExpanded("A"));  // behavior unchanged
    }

    [Fact]
    public void GetVisibleDataItems_GroupedMode_ExcludesCollapsedGroupsRows()
    {
        var h = SetupGrouped();

        Assert.Equal(3, h.Manager.GetVisibleDataItems().Count);

        h.Manager.ToggleGroup("A", false);

        var visible = h.Manager.GetVisibleDataItems().Cast<ExpandTestItem>().Select(i => i.Symbol).ToList();
        Assert.Equal(new[] { "B1" }, visible);
    }

    // ── Tree mode (ChildProperty) ────────────────────────────────────

    [Fact]
    public void ToggleRow_Expand_NotifiesWithParentAndItsChildren()
    {
        var (h, parent, leg1, leg2) = SetupTree();

        h.Manager.ToggleRow(parent, true);

        var e = Assert.Single(h.Events);
        Assert.True(e.IsExpanded);
        Assert.Equal(SkExpandChangeReason.Api, e.Reason);
        Assert.Same(parent, e.ParentItem);
        Assert.Null(e.GroupName);
        Assert.Equal(new object[] { leg1, leg2 }, e.AffectedItems);
        Assert.True(h.Manager.IsRowExpanded(parent));
    }

    [Fact]
    public void ToggleRow_Expand_MakesChildRowsVisible()
    {
        var (h, parent, leg1, leg2) = SetupTree();

        Assert.Equal(2, h.Manager.GetVisibleDataItems().Count); // parent + PLAIN

        h.Manager.ToggleRow(parent, true);

        var visible = h.Manager.GetVisibleDataItems();
        Assert.Equal(4, visible.Count);
        Assert.Contains(leg1, visible);
        Assert.Contains(leg2, visible);
    }

    [Fact]
    public void ToggleRow_ToStateItIsAlreadyIn_DoesNotNotifyOrReflatten()
    {
        var (h, parent, _, _) = SetupTree();
        int refreshesBefore = h.RefreshCount;

        h.Manager.ToggleRow(parent, false); // already collapsed

        Assert.Empty(h.Events);
        Assert.Equal(refreshesBefore, h.RefreshCount);
    }

    [Fact]
    public void ToggleRow_Collapse_NotifiesWithHiddenChildren()
    {
        var (h, parent, leg1, leg2) = SetupTree();
        h.Manager.ToggleRow(parent, true);
        h.Events.Clear();

        h.Manager.ToggleRow(parent, false);

        var e = Assert.Single(h.Events);
        Assert.False(e.IsExpanded);
        Assert.Equal(new object[] { leg1, leg2 }, e.AffectedItems);
        Assert.False(h.Manager.IsRowExpanded(parent));
    }

    [Fact]
    public void SetAllRowsExpanded_RaisesOneBulkNotificationWithEveryChildRow()
    {
        var (h, parent, leg1, leg2) = SetupTree();

        h.Manager.SetAllRowsExpanded(true);

        var e = Assert.Single(h.Events);
        Assert.True(e.IsBulk);
        Assert.Equal(SkExpandChangeReason.ExpandAll, e.Reason);
        Assert.Null(e.ParentItem);
        Assert.Equal(new object[] { leg1, leg2 }, e.AffectedItems);
        Assert.Equal(new object[] { parent }, h.Manager.GetExpandedRowItems()); // PLAIN has no children
    }

    [Fact]
    public void SetAllRowsExpanded_False_CollapsesEverythingAndReportsCollapseAll()
    {
        var (h, parent, _, _) = SetupTree();
        h.Manager.SetAllRowsExpanded(true);
        h.Events.Clear();

        h.Manager.SetAllRowsExpanded(false);

        var e = Assert.Single(h.Events);
        Assert.Equal(SkExpandChangeReason.CollapseAll, e.Reason);
        Assert.False(e.IsExpanded);
        Assert.Empty(h.Manager.GetExpandedRowItems());
        Assert.Equal(2, h.Manager.GetVisibleDataItems().Count);
    }

    [Fact]
    public void UpdateRowToggle_GlyphClick_NotifiesWithUserToggleReason()
    {
        var (h, parent, leg1, leg2) = SetupTree();
        // Simulate the parent row's toggle glyph hit-test rect published by the renderer.
        h.Manager.RowToggleDetails[parent] = (0, 0, 10, 10);

        h.Manager.UpdateRowToggle(5, 5, totalRows: 10);

        var e = Assert.Single(h.Events);
        Assert.Equal(SkExpandChangeReason.UserToggle, e.Reason);
        Assert.True(e.IsExpanded);
        Assert.Same(parent, e.ParentItem);
        Assert.Equal(new object[] { leg1, leg2 }, e.AffectedItems);
    }

    [Fact]
    public void ToggleRow_InGroupedMode_IsNoOp()
    {
        var h = SetupGrouped();
        var stray = new ExpandTestItem { Symbol = "X" };

        h.Manager.ToggleRow(stray, true);

        Assert.Empty(h.Events);
        Assert.False(h.Manager.IsRowExpanded(stray));
    }

    [Fact]
    public void ToggleGroup_InTreeMode_IsNoOp()
    {
        var (h, _, _, _) = SetupTree();

        h.Manager.ToggleGroup("anything", false);

        Assert.Empty(h.Events);
    }
}
