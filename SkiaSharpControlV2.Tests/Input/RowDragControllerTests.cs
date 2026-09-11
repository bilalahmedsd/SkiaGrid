using System.Collections.ObjectModel;

using SkiaSharpControlV2.Input;

namespace SkiaSharpControlV2.Tests.Input;

/// <summary>
/// Row drag (2.18.0). The controller's WPF half — capture, the auto-scroll DispatcherTimer, the
/// cursor — needs a live input stack and a pressed physical mouse button, so the parts that decide
/// WHAT happens (which gap, which rows, which source move) are static methods and are tested here
/// directly. That is deliberate: a wrong gap or a wrong block move is silent data corruption in the
/// consumer's collection, while wrong capture handling is visible the instant you drag once.
/// </summary>
public class RowDragControllerTests
{
    private static bool AllDraggable(object _) => true;

    // ── gap math ────────────────────────────────────────────────────────────────

    [Theory]
    // Top edge of row 0 → gap 0 (drop above everything).
    [InlineData(0, 0)]
    // Upper half of row 0 → still gap 0.
    [InlineData(5, 0)]
    // Lower half of row 0 → gap 1 (drop between row 0 and row 1). This rounding is what makes the
    // indicator snap to whichever half of the row the pointer is in.
    [InlineData(9, 1)]
    [InlineData(14, 1)]
    [InlineData(20, 1)]
    [InlineData(22, 2)]
    public void GapIndexFromContentY_SnapsToTheNearestRowBoundary(double contentY, int expected)
    {
        // 14 px rows — the default Compact metric.
        Assert.Equal(expected, RowDragController.GapIndexFromContentY(contentY, rowHeight: 14, rowCount: 10));
    }

    [Fact]
    public void GapIndexFromContentY_ClampsPastTheLastRowToTheEndGap()
    {
        // Dragging well below the last row must mean "put it last", not an out-of-range index:
        // gap == rowCount is the legal end position.
        Assert.Equal(10, RowDragController.GapIndexFromContentY(9_999, 14, 10));
    }

    [Fact]
    public void GapIndexFromContentY_ClampsNegativeToZero()
    {
        Assert.Equal(0, RowDragController.GapIndexFromContentY(-50, 14, 10));
    }

    [Theory]
    [InlineData(0)]     // no rows
    [InlineData(-1)]
    public void GapIndexFromContentY_EmptyGridIsGapZero(int rowCount)
    {
        Assert.Equal(0, RowDragController.GapIndexFromContentY(100, 14, rowCount));
    }

    [Fact]
    public void GapIndexFromContentY_ZeroRowHeightDoesNotDivideByZero()
    {
        Assert.Equal(0, RowDragController.GapIndexFromContentY(100, rowHeight: 0, rowCount: 10));
    }

    // ── auto-scroll ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2, -1)]      // inside the top zone → scroll up
    [InlineData(13, -1)]
    [InlineData(70, 0)]      // middle → no scroll
    [InlineData(139, 1)]     // inside the bottom zone → scroll down
    public void AutoScrollDirection_OnlyFiresInsideTheEdgeZones(double pointerY, int expected)
    {
        Assert.Equal(expected, RowDragController.AutoScrollDirection(pointerY, viewportHeight: 140));
    }

    [Fact]
    public void AutoScrollDirection_TinyViewportNeverAutoScrolls()
    {
        // A band shorter than both zones would otherwise be entirely "edge", so any drag inside it
        // would scroll forever in one direction.
        Assert.Equal(0, RowDragController.AutoScrollDirection(pointerY: 1, viewportHeight: 20));
        Assert.Equal(0, RowDragController.AutoScrollDirection(pointerY: 19, viewportHeight: 20));
    }

    // ── which rows get dragged ──────────────────────────────────────────────────

    [Fact]
    public void BuildDragSet_UnselectedRow_DragsThatRowAlone()
    {
        var rows = new List<object> { "a", "b", "c" };

        var set = RowDragController.BuildDragSet(rows, 1, selected: null, AllDraggable);

        Assert.Equal(new object[] { "b" }, set);
    }

    [Fact]
    public void BuildDragSet_PressOnAMultiSelection_DragsTheWholeSelectionInViewOrder()
    {
        var rows = new List<object> { "a", "b", "c", "d" };
        // Selection order is deliberately scrambled: the block must come back in VIEW order, since
        // that is the order it will be re-inserted in.
        var selected = new ObservableCollection<object> { "d", "a" };

        var set = RowDragController.BuildDragSet(rows, 0, selected, AllDraggable);

        Assert.Equal(new object[] { "a", "d" }, set);
    }

    [Fact]
    public void BuildDragSet_PressOutsideTheSelection_DragsOnlyThePressedRow()
    {
        var rows = new List<object> { "a", "b", "c" };
        var selected = new ObservableCollection<object> { "a", "b" };

        // Pressing "c" while a/b are selected is a new single-row gesture, not a block drag.
        var set = RowDragController.BuildDragSet(rows, 2, selected, AllDraggable);

        Assert.Equal(new object[] { "c" }, set);
    }

    [Fact]
    public void BuildDragSet_SingleSelection_IsNotTreatedAsABlock()
    {
        var rows = new List<object> { "a", "b" };
        var selected = new ObservableCollection<object> { "b" };

        Assert.Equal(new object[] { "b" }, RowDragController.BuildDragSet(rows, 1, selected, AllDraggable));
    }

    [Fact]
    public void BuildDragSet_UndraggablePressedRow_DragsNothing()
    {
        // Group headers and tree children report undraggable — pressing one must not start a drag.
        var rows = new List<object> { "group", "a" };

        var set = RowDragController.BuildDragSet(rows, 0, selected: null, item => !Equals(item, "group"));

        Assert.Empty(set);
    }

    [Fact]
    public void BuildDragSet_UndraggableRowsAreDroppedFromABlock()
    {
        var rows = new List<object> { "a", "group", "b" };
        var selected = new ObservableCollection<object> { "a", "group", "b" };

        var set = RowDragController.BuildDragSet(rows, 0, selected, item => !Equals(item, "group"));

        Assert.Equal(new object[] { "a", "b" }, set);
    }

    // ── the source move ─────────────────────────────────────────────────────────

    [Fact]
    public void MoveBlock_DragDown_LandsAboveTheTarget()
    {
        var source = new ObservableCollection<string> { "a", "b", "c", "d" };

        // Drag "a" onto the gap above "d".
        Assert.True(RowDragController.MoveBlock(source, new object[] { "a" }, "d"));

        Assert.Equal(new[] { "b", "c", "a", "d" }, source);
    }

    [Fact]
    public void MoveBlock_DragUp_LandsAboveTheTarget()
    {
        var source = new ObservableCollection<string> { "a", "b", "c", "d" };

        Assert.True(RowDragController.MoveBlock(source, new object[] { "d" }, "b"));

        Assert.Equal(new[] { "a", "d", "b", "c" }, source);
    }

    [Fact]
    public void MoveBlock_NullTarget_MovesToTheEnd()
    {
        var source = new ObservableCollection<string> { "a", "b", "c" };

        Assert.True(RowDragController.MoveBlock(source, new object[] { "a" }, null));

        Assert.Equal(new[] { "b", "c", "a" }, source);
    }

    [Fact]
    public void MoveBlock_DropWhereItAlreadyIs_ReportsNoChange()
    {
        var source = new ObservableCollection<string> { "a", "b", "c" };

        // "a" is already immediately above "b" — the caller can skip its Refresh().
        Assert.False(RowDragController.MoveBlock(source, new object[] { "a" }, "b"));

        Assert.Equal(new[] { "a", "b", "c" }, source);
    }

    [Fact]
    public void MoveBlock_UsesObservableCollectionMove_SoOneNotificationIsRaised()
    {
        var source = new ObservableCollection<string> { "a", "b", "c" };
        var actions = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        source.CollectionChanged += (_, e) => actions.Add(e.Action);

        RowDragController.MoveBlock(source, new object[] { "a" }, null);

        // Remove+Insert would raise two, and the collection view rebuilds on each one.
        Assert.Equal(new[] { System.Collections.Specialized.NotifyCollectionChangedAction.Move }, actions);
    }

    [Fact]
    public void MoveBlock_MultiRow_KeepsTheBlocksRelativeOrder()
    {
        var source = new ObservableCollection<string> { "a", "b", "c", "d", "e" };

        // Move the non-contiguous pair (a, c) above "e".
        Assert.True(RowDragController.MoveBlock(source, new object[] { "a", "c" }, "e"));

        Assert.Equal(new[] { "b", "d", "a", "c", "e" }, source);
    }

    [Fact]
    public void MoveBlock_MultiRow_ToTheEnd()
    {
        var source = new ObservableCollection<string> { "a", "b", "c" };

        Assert.True(RowDragController.MoveBlock(source, new object[] { "a", "b" }, null));

        Assert.Equal(new[] { "c", "a", "b" }, source);
    }

    [Fact]
    public void MoveBlock_ItemMissingFromTheSource_DoesNothing()
    {
        // The source can change under a drag (a live market-data feed removed the row mid-gesture).
        var source = new ObservableCollection<string> { "a", "b" };

        Assert.False(RowDragController.MoveBlock(source, new object[] { "gone" }, "b"));

        Assert.Equal(new[] { "a", "b" }, source);
    }

    [Fact]
    public void MoveBlock_TargetMissingFromTheSource_FallsBackToTheEnd()
    {
        var source = new ObservableCollection<string> { "a", "b", "c" };

        Assert.True(RowDragController.MoveBlock(source, new object[] { "a" }, "not-in-source"));

        Assert.Equal(new[] { "b", "c", "a" }, source);
    }

    [Fact]
    public void MoveBlock_FixedSizeSource_IsRefusedRatherThanThrowing()
    {
        // An array bound straight to ItemsSource: IList, but Insert throws. Refuse it instead.
        var source = new[] { "a", "b", "c" };

        Assert.False(RowDragController.MoveBlock(source, new object[] { "a" }, "c"));

        Assert.Equal(new[] { "a", "b", "c" }, source);
    }

    [Fact]
    public void MoveBlock_EmptyDragSet_DoesNothing()
    {
        var source = new ObservableCollection<string> { "a" };

        Assert.False(RowDragController.MoveBlock(source, Array.Empty<object>(), null));
    }
}
