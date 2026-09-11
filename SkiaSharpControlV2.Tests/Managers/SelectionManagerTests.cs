using SkiaSharpControlV2.Managers;
using System.Collections.ObjectModel;

namespace SkiaSharpControlV2.Tests.Managers;

public class SelectionManagerTests
{
    private SelectionManager CreateManager()
    {
        return new SelectionManager(
            () => { }, // invalidateVisual no-op
            (items) => { }); // updateRenderer no-op
    }

    private List<dynamic> MakeItems(params string[] names)
        => names.Select(n => (dynamic)n).ToList();

    // ── IsPartOfMultiSelection: the gate that makes a multi-row DRAG possible ───
    //
    // Without it a plain press collapsed the selection to the pressed row BEFORE the drag set was
    // built, so a multi-row drag could never carry more than one row (found by driving a real
    // drag with synthetic input; the press-then-drag ordering itself is not unit-testable).

    [Fact]
    public void IsPartOfMultiSelection_TrueForAMemberOfAMultiRowSelection()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");
        mgr.HandleSingleClick(items, 0);
        mgr.HandleCtrlClick(items, 2);

        Assert.True(mgr.IsPartOfMultiSelection("A"));
        Assert.True(mgr.IsPartOfMultiSelection("C"));
    }

    [Fact]
    public void IsPartOfMultiSelection_FalseWhenOnlyOneRowIsSelected()
    {
        // A single-row selection must still collapse on PRESS, exactly as before — deferring it
        // there would change click behaviour for no benefit.
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");
        mgr.HandleSingleClick(items, 1);

        Assert.False(mgr.IsPartOfMultiSelection("B"));
    }

    [Fact]
    public void IsPartOfMultiSelection_FalseForARowOutsideTheSelection()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");
        mgr.HandleSingleClick(items, 0);
        mgr.HandleCtrlClick(items, 1);

        Assert.False(mgr.IsPartOfMultiSelection("C"));
    }

    [Fact]
    public void IsPartOfMultiSelection_FalseForNullAndForNoSelection()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();

        Assert.False(mgr.IsPartOfMultiSelection(null));
        Assert.False(mgr.IsPartOfMultiSelection("A"));
    }

    [Fact]
    public void HandleSingleClick_SelectsOne()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");

        mgr.HandleSingleClick(items, 1);

        Assert.Single(mgr.SelectedItems!);
        Assert.Equal("B", mgr.SelectedItems![0]);
        Assert.Equal(1, mgr.LastSelectedRowIndex);
    }

    [Fact]
    public void HandleSingleClick_ClearsPrevious()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");

        mgr.HandleSingleClick(items, 0);
        mgr.HandleSingleClick(items, 2);

        Assert.Single(mgr.SelectedItems!);
        Assert.Equal("C", mgr.SelectedItems![0]);
    }

    [Fact]
    public void HandleCtrlClick_TogglesIn()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");

        mgr.HandleCtrlClick(items, 0);
        mgr.HandleCtrlClick(items, 2);

        Assert.Equal(2, mgr.SelectedItems!.Count);
        Assert.Contains("A", mgr.SelectedItems);
        Assert.Contains("C", mgr.SelectedItems);
    }

    [Fact]
    public void HandleCtrlClick_TogglesOut()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");

        mgr.HandleCtrlClick(items, 0);
        mgr.HandleCtrlClick(items, 0); // toggle off

        Assert.Empty(mgr.SelectedItems!);
    }

    [Fact]
    public void HandleShiftClick_SelectsRange()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C", "D", "E");

        mgr.HandleSingleClick(items, 1); // anchor at B
        mgr.HandleShiftClick(items, 3);  // range B-D

        Assert.Equal(3, mgr.SelectedItems!.Count);
        Assert.Contains("B", mgr.SelectedItems);
        Assert.Contains("C", mgr.SelectedItems);
        Assert.Contains("D", mgr.SelectedItems);
    }

    [Fact]
    public void HandleShiftClick_ReverseRange()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C", "D", "E");

        mgr.HandleSingleClick(items, 3); // anchor at D
        mgr.HandleShiftClick(items, 1);  // range B-D

        Assert.Equal(3, mgr.SelectedItems!.Count);
    }

    [Fact]
    public void AddToSelection_AddsWithoutToggling()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");

        var first = mgr.AddToSelection(items, 0);
        var second = mgr.AddToSelection(items, 2);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(2, mgr.SelectedItems!.Count);
        Assert.Contains("A", mgr.SelectedItems);
        Assert.Contains("C", mgr.SelectedItems);
        Assert.Equal(2, mgr.LastSelectedRowIndex);
    }

    [Fact]
    public void AddToSelection_Idempotent_DoesNotDuplicateOrToggle()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");

        var first = mgr.AddToSelection(items, 1);
        var second = mgr.AddToSelection(items, 1); // already present — must NOT remove

        Assert.True(first);
        Assert.False(second);
        Assert.Single(mgr.SelectedItems!);
        Assert.Contains("B", mgr.SelectedItems);
    }

    [Fact]
    public void RemoveFromSelection_RemovesWhenPresent()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");

        mgr.AddToSelection(items, 0);
        mgr.AddToSelection(items, 1);
        var removed = mgr.RemoveFromSelection(items, 0);

        Assert.True(removed);
        Assert.Single(mgr.SelectedItems!);
        Assert.Contains("B", mgr.SelectedItems);
        Assert.DoesNotContain("A", mgr.SelectedItems);
    }

    [Fact]
    public void RemoveFromSelection_NotPresent_ReturnsFalse()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");

        var removed = mgr.RemoveFromSelection(items, 0);

        Assert.False(removed);
        Assert.Empty(mgr.SelectedItems!);
    }

    [Fact]
    public void AddToSelection_OutOfBounds_DoesNotThrow()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A");

        Assert.False(mgr.AddToSelection(items, 5));
        Assert.False(mgr.RemoveFromSelection(items, -1));
        Assert.Empty(mgr.SelectedItems!);
    }

    [Fact]
    public void HandleRightClick_SelectsIfNotAlreadySelected()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");

        mgr.HandleRightClick(items, 1);

        Assert.Single(mgr.SelectedItems!);
        Assert.Equal("B", mgr.SelectedItems![0]);
    }

    [Fact]
    public void HandleRightClick_KeepsExistingIfAlreadySelected()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B", "C");

        mgr.HandleSingleClick(items, 1); // select B
        mgr.HandleRightClick(items, 1);  // right-click B

        Assert.Single(mgr.SelectedItems!);
        Assert.Equal("B", mgr.SelectedItems![0]);
    }

    [Fact]
    public void SelectAll_SelectsAllItems()
    {
        var mgr = CreateManager();
        var items = new List<object> { "A", "B", "C" };

        mgr.SelectAll(items);

        Assert.Equal(3, mgr.SelectedItems!.Count);
        Assert.Equal(2, mgr.LastSelectedRowIndex);
        Assert.Equal(0, mgr.SelectionAnchorIndex);
    }

    [Fact]
    public void Clear_RemovesAllSelection()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B");

        mgr.HandleSingleClick(items, 0);
        mgr.Clear();

        Assert.Empty(mgr.SelectedItems!);
        Assert.Equal(0, mgr.LastSelectedRowIndex);
        Assert.Null(mgr.SelectionAnchorIndex);
    }

    [Fact]
    public void IsSelected_ReturnsTrueForSelectedItem()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A", "B");

        mgr.HandleSingleClick(items, 0);

        Assert.True(mgr.IsSelected("A"));
        Assert.False(mgr.IsSelected("B"));
    }

    [Fact]
    public void IsSelected_NullItem_ReturnsFalse()
    {
        var mgr = CreateManager();
        Assert.False(mgr.IsSelected(null));
    }

    [Fact]
    public void HasSelection_ReflectsState()
    {
        var mgr = CreateManager();
        Assert.False(mgr.HasSelection);

        mgr.EnsureInitialized();
        Assert.False(mgr.HasSelection);

        mgr.HandleSingleClick(MakeItems("A"), 0);
        Assert.True(mgr.HasSelection);

        mgr.Clear();
        Assert.False(mgr.HasSelection);
    }

    [Fact]
    public void EnsureInitialized_CreatesSelectedItems()
    {
        var mgr = CreateManager();
        Assert.Null(mgr.SelectedItems);

        mgr.EnsureInitialized();
        Assert.NotNull(mgr.SelectedItems);
    }

    [Fact]
    public void HandleSingleClick_OutOfBounds_DoesNotThrow()
    {
        var mgr = CreateManager();
        mgr.EnsureInitialized();
        var items = MakeItems("A");

        mgr.HandleSingleClick(items, 5); // out of bounds
        Assert.Empty(mgr.SelectedItems!);
    }

    [Fact]
    public void ClearAndRefresh_ClearsAndInvokesVisual()
    {
        bool refreshed = false;
        var mgr = new SelectionManager(() => refreshed = true, (items) => { });
        mgr.EnsureInitialized();
        mgr.HandleSingleClick(MakeItems("A"), 0);

        mgr.ClearAndRefresh();

        Assert.Empty(mgr.SelectedItems!);
        Assert.True(refreshed);
    }
}
