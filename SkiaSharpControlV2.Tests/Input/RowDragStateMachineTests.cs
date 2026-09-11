using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;

using SkiaSharpControlV2.Input;

namespace SkiaSharpControlV2.Tests.Input;

/// <summary>
/// Drives a whole drag through <see cref="RowDragController"/> — arm, cross the threshold, track,
/// drop — with every collaborator faked. This is the layer the pure-math tests cannot reach: that a
/// press followed by a small move is still a CLICK, that the drop target excludes the rows being
/// dragged, and that a cancelled RowDragStarting really stops everything.
///
/// The physical mouse button is injected (there is no pressed button in a test run) and mouse
/// capture is a no-op without a live input session, which is harmless here: the controller ignores a
/// failed capture and only releases one it actually holds.
/// </summary>
public class RowDragStateMachineTests
{
    private sealed class Harness
    {
        public List<object> Rows = new() { "a", "b", "c", "d", "e" };
        public double RowHeight = 14;
        public double ScrollOffsetY;
        public double ViewportHeight = 140;
        public IEnumerable? Selected;
        public Func<object, bool> IsDraggable = _ => true;
        public bool LeftButtonDown = true;
        public bool CancelStart;

        public int Invalidations;
        public int ScrollTicks;
        public List<SKRowDragStartingEventArgs> Started = new();
        public List<SKRowDroppedEventArgs> Dropped = new();
        public int PublishedInsertIndex = -1;
        public int ExternalHandoffs;
        public List<int>? PublishedGhostRows;

        public RowDragController Controller { get; }
        public SKRowDragMode Mode { get; set; } = SKRowDragMode.Reorder;

        public Harness()
        {
            Controller = new RowDragController(
                () => Mode,
                () => Rows,
                () => RowHeight,
                () => ScrollOffsetY,
                () => ViewportHeight,
                () => Selected,
                item => IsDraggable(item),
                _ => ScrollTicks++,
                () => Invalidations++,
                args => { Started.Add(args); args.Cancel = CancelStart; return !args.Cancel; },
                args => Dropped.Add(args),
                (insert, ghosts) => { PublishedInsertIndex = insert; PublishedGhostRows = ghosts; },
                // DragOut is covered by RowDragOutTests; an in-grid drag must never reach this.
                (_, _, _) => ExternalHandoffs++,
                () => LeftButtonDown);
        }

        /// <summary>Content-space Y for the centre of a row.</summary>
        public Point RowCentre(int row) => new(10, row * RowHeight + RowHeight / 2 - ScrollOffsetY);

        /// <summary>Y that lands just below a row boundary, i.e. the gap above <paramref name="gap"/>.</summary>
        public Point Gap(int gap) => new(10, gap * RowHeight - ScrollOffsetY);
    }

    // ── click vs drag ───────────────────────────────────────────────────────────

    [Fact]
    public void APressAndTinyMove_IsStillAClick()
    {
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(1), 1);

        // 1 px of travel — under the system drag threshold (4 px by default).
        var p = h.RowCentre(1);
        bool consumed = h.Controller.Update(new Point(p.X + 1, p.Y));

        Assert.False(consumed);
        Assert.False(h.Controller.IsDragging);
        Assert.Empty(h.Started);
    }

    [Fact]
    public void PassingTheThreshold_StartsTheDrag()
    {
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(1), 1);

        bool consumed = h.Controller.Update(h.RowCentre(3));

        Assert.True(consumed);
        Assert.True(h.Controller.IsDragging);
        var started = Assert.Single(h.Started);
        Assert.Equal(1, started.RowIndex);
        Assert.Equal(new object[] { "b" }, started.Items);
    }

    [Fact]
    public void ModeNone_NeverArmsAndNeverTracks()
    {
        // The default-off guarantee at the state-machine level.
        var h = new Harness { Mode = SKRowDragMode.None };

        h.Controller.Arm(null!, h.RowCentre(1), 1);
        Assert.False(h.Controller.Update(h.RowCentre(4)));
        Assert.False(h.Controller.IsDragging);
        Assert.Empty(h.Started);
        Assert.Equal(0, h.Invalidations);
    }

    [Fact]
    public void PressingAnUndraggableRow_NeverArms()
    {
        var h = new Harness { IsDraggable = item => !Equals(item, "a") };

        h.Controller.Arm(null!, h.RowCentre(0), 0);
        Assert.False(h.Controller.Update(h.RowCentre(3)));
        Assert.False(h.Controller.IsDragging);
    }

    [Fact]
    public void ButtonReleasedOutsideTheCanvas_Disarms()
    {
        // The release happened over another window, so no MouseLeftButtonUp ever reaches us.
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(1), 1);
        h.LeftButtonDown = false;

        Assert.False(h.Controller.Update(h.RowCentre(4)));
        Assert.False(h.Controller.IsDragging);

        // And the stale arm must not resurrect on a later move.
        h.LeftButtonDown = true;
        Assert.False(h.Controller.Update(h.RowCentre(4)));
        Assert.False(h.Controller.IsDragging);
    }

    [Fact]
    public void CancellingRowDragStarting_StopsEverything()
    {
        var h = new Harness { CancelStart = true };
        h.Controller.Arm(null!, h.RowCentre(1), 1);

        Assert.False(h.Controller.Update(h.RowCentre(4)));
        Assert.False(h.Controller.IsDragging);
        Assert.Single(h.Started);          // it was asked
        Assert.Equal(-1, h.PublishedInsertIndex);  // but nothing was painted
    }

    // ── tracking ────────────────────────────────────────────────────────────────

    [Fact]
    public void TrackingPublishesTheGapAndTheGhostedRows()
    {
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(0), 0);

        h.Controller.Update(h.Gap(3));

        Assert.Equal(3, h.PublishedInsertIndex);
        Assert.Equal(new[] { 0 }, h.PublishedGhostRows);
        Assert.True(h.Invalidations > 0, "a gap change must repaint");
    }

    [Fact]
    public void MovingWithinTheSameGap_DoesNotRepaint()
    {
        // Guards the paint budget: a drag produces a MouseMove per pixel, and only a gap CHANGE is
        // worth a frame.
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(0), 0);
        h.Controller.Update(h.Gap(3));
        int after = h.Invalidations;

        var p = h.Gap(3);
        h.Controller.Update(new Point(p.X + 2, p.Y + 1));

        Assert.Equal(after, h.Invalidations);
    }

    [Fact]
    public void TrackingAccountsForTheScrollOffset()
    {
        var h = new Harness { ScrollOffsetY = 28 };   // scrolled down two rows
        h.Controller.Arm(null!, h.RowCentre(2), 2);

        // Pointer at viewport y = 14 → content y = 42 → gap 3.
        h.Controller.Update(new Point(10, 14));

        Assert.Equal(3, h.PublishedInsertIndex);
    }

    [Fact]
    public void EscapeCancels_WithoutRaisingADrop()
    {
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(0), 0);
        h.Controller.Update(h.Gap(3));

        h.Controller.Cancel();

        Assert.False(h.Controller.IsDragging);
        Assert.Empty(h.Dropped);
        Assert.Equal(-1, h.PublishedInsertIndex);   // indicator cleared
    }

    // ── the drop ────────────────────────────────────────────────────────────────

    [Fact]
    public void DroppingReportsTheTargetRowTheBlockLandsAbove()
    {
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(0), 0);
        h.Controller.Update(h.Gap(3));

        Assert.True(h.Controller.Complete(h.Gap(3)));

        var drop = Assert.Single(h.Dropped);
        Assert.Equal(3, drop.InsertIndex);
        Assert.Equal("d", drop.TargetItem);
        Assert.Equal(new object[] { "a" }, drop.Items);
    }

    [Fact]
    public void DroppingPastTheLastRow_ReportsANullTarget()
    {
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(0), 0);
        h.Controller.Update(h.Gap(5));   // 5 rows → gap 5 is the end

        h.Controller.Complete(h.Gap(5));

        var drop = Assert.Single(h.Dropped);
        Assert.Equal(5, drop.InsertIndex);
        Assert.Null(drop.TargetItem);
    }

    [Fact]
    public void TheTargetSkipsPastRowsThatAreThemselvesBeingDragged()
    {
        // Dragging the a+b block onto the gap above "b" would name a dragged row as the target, and
        // "land above b" is meaningless when b is moving too. The target walks on to the first row
        // that is staying put.
        var h = new Harness { Selected = new ObservableCollection<object> { "a", "b" } };
        h.Controller.Arm(null!, h.RowCentre(0), 0);
        h.Controller.Update(h.Gap(1));

        h.Controller.Complete(h.Gap(1));

        var drop = Assert.Single(h.Dropped);
        Assert.Equal(new object[] { "a", "b" }, drop.Items);
        Assert.Equal("c", drop.TargetItem);
    }

    [Fact]
    public void DroppingABlockThatIncludesEveryTrailingRow_StillReportsAnInRangeGap()
    {
        // The "skip past dragged rows" walk runs off the end here: d and e are both moving, so there
        // is no row below the gap that is staying put. InsertIndex must stay <= RowCount.
        var h = new Harness { Selected = new ObservableCollection<object> { "d", "e" } };
        h.Controller.Arm(null!, h.RowCentre(3), 3);
        h.Controller.Update(h.Gap(4));
        h.Controller.Complete(h.Gap(4));

        var drop = Assert.Single(h.Dropped);
        Assert.Equal(h.Rows.Count, drop.InsertIndex);
        Assert.Null(drop.TargetItem);
    }

    [Fact]
    public void CompleteWithoutADrag_IsANoOp()
    {
        var h = new Harness();

        Assert.False(h.Controller.Complete(h.RowCentre(2)));
        Assert.Empty(h.Dropped);
    }

    [Fact]
    public void DroppingClearsTheIndicator()
    {
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(0), 0);
        h.Controller.Update(h.Gap(3));
        h.Controller.Complete(h.Gap(3));

        Assert.False(h.Controller.IsDragging);
        Assert.Equal(-1, h.Controller.InsertIndex);
        Assert.Equal(-1, h.PublishedInsertIndex);
        Assert.Null(h.PublishedGhostRows);
    }

    [Fact]
    public void ASecondDragAfterADrop_StartsClean()
    {
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(0), 0);
        h.Controller.Update(h.Gap(3));
        h.Controller.Complete(h.Gap(3));

        h.Controller.Arm(null!, h.RowCentre(4), 4);
        h.Controller.Update(h.Gap(1));

        Assert.True(h.Controller.IsDragging);
        Assert.Equal(2, h.Started.Count);
        Assert.Equal(new object[] { "e" }, h.Started[1].Items);
    }

    [Fact]
    public void InGridModes_NeverHandTheGestureToWpfDragDrop()
    {
        // Reorder and Notify keep the gesture; only DragOut hands it over. Mixing the two would
        // mean an insertion indicator AND a shell drag running at once.
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(0), 0);
        h.Controller.Update(h.Gap(3));
        h.Controller.Complete(h.Gap(3));

        Assert.Equal(0, h.ExternalHandoffs);
    }

    [Fact]
    public void NotifyMode_TracksAndReportsExactlyLikeReorder()
    {
        // The difference between the two modes lives in the grid, not here — the controller must
        // behave identically so a Notify consumer gets the same indicator and the same drop info.
        var h = new Harness { Mode = SKRowDragMode.Notify };
        h.Controller.Arm(null!, h.RowCentre(0), 0);
        h.Controller.Update(h.Gap(2));
        h.Controller.Complete(h.Gap(2));

        var drop = Assert.Single(h.Dropped);
        Assert.Equal(2, drop.InsertIndex);
        Assert.Equal("c", drop.TargetItem);
    }
}
