using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Windows;

using SkiaSharpControlV2;
using SkiaSharpControlV2.Input;

namespace SkiaSharpControlV2.Tests.Input;

/// <summary>
/// <see cref="SKRowDragMode.DragOut"/> — dragging a row or a cell OUT of the grid so another window
/// (or another application) can receive it.
/// <para>
/// <c>DragDrop.DoDragDrop</c> itself is not testable: it pumps a nested message loop and blocks
/// until a real user releases a real button. So the two things around it are pinned here instead —
/// that the controller hands the gesture over WITHOUT any of its in-grid tracking, and that the
/// payload the grid builds carries what a drop target needs. The drop itself is verified by driving
/// synthetic mouse input against the demo's drop-target window.
/// </para>
/// </summary>
public class RowDragOutTests
{
    private static void Sta(Action body)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured is not null) ExceptionDispatchInfo.Capture(captured).Throw();
    }

    private sealed class Row
    {
        public string Symbol { get; set; } = "";
        public double Price { get; set; }
    }

    // ── the controller hands over instead of tracking ───────────────────────────

    private sealed class Harness
    {
        public List<object> Rows = new() { "a", "b", "c", "d", "e" };
        public IEnumerable? Selected;
        public bool CancelStart;

        public int Invalidations;
        public int PublishedInsertIndex = -1;
        public List<(List<object> Items, int RowIndex, SKGridViewColumn? Column)> External = new();
        public List<SKRowDroppedEventArgs> Dropped = new();

        public RowDragController Controller { get; }
        public SKRowDragMode Mode { get; set; } = SKRowDragMode.DragOut;

        public Harness()
        {
            Controller = new RowDragController(
                () => Mode,
                () => Rows,
                () => 14,
                () => 0,
                () => 140,
                () => Selected,
                _ => true,
                _ => { },
                () => Invalidations++,
                args => { args.Cancel = CancelStart; return !args.Cancel; },
                args => Dropped.Add(args),
                (insert, _) => PublishedInsertIndex = insert,
                (items, rowIndex, column) => External.Add((items, rowIndex, column)),
                () => true);
        }

        public Point RowCentre(int row) => new(10, row * 14 + 7);
    }

    [Fact]
    public void DragOut_HandsTheGestureOverOnce()
    {
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(1), 1);

        Assert.True(h.Controller.Update(h.RowCentre(4)));

        var handed = Assert.Single(h.External);
        Assert.Equal(new object[] { "b" }, handed.Items);
        Assert.Equal(1, handed.RowIndex);
    }

    [Fact]
    public void DragOut_DoesNotTrackInsideTheGrid()
    {
        // The whole point of the mode: no capture, no insertion indicator, no repaint, and no
        // RowDropped — WPF owns the gesture from here and nothing in this grid is moving.
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(1), 1);
        h.Controller.Update(h.RowCentre(4));

        Assert.False(h.Controller.IsDragging);
        Assert.Equal(-1, h.Controller.InsertIndex);
        Assert.Equal(-1, h.PublishedInsertIndex);
        Assert.Equal(0, h.Invalidations);
        Assert.Empty(h.Dropped);
    }

    [Fact]
    public void DragOut_DisarmsSoFurtherMovesDoNotStartASecondDrag()
    {
        // DoDragDrop blocks for the whole gesture; anything left armed would still be armed on the
        // far side of it and would fire again on the next stray move.
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(1), 1);
        h.Controller.Update(h.RowCentre(4));

        h.Controller.Update(h.RowCentre(0));

        Assert.Single(h.External);
    }

    [Fact]
    public void DragOut_CarriesThePressedColumn()
    {
        var h = new Harness();
        var col = new SKGridViewColumn { Header = "Last", BindingPath = "Price", Width = 80 };
        h.Controller.Arm(null!, h.RowCentre(2), 2, col);

        h.Controller.Update(h.RowCentre(0));

        Assert.Same(col, Assert.Single(h.External).Column);
    }

    [Fact]
    public void DragOut_TakesTheWholeSelectionWhenThePressedRowIsPartOfIt()
    {
        var h = new Harness { Selected = new ObservableCollection<object> { "b", "d" } };
        h.Controller.Arm(null!, h.RowCentre(1), 1);

        h.Controller.Update(h.RowCentre(4));

        Assert.Equal(new object[] { "b", "d" }, Assert.Single(h.External).Items);
    }

    [Fact]
    public void DragOut_ACancelledStartHandsOverNothing()
    {
        var h = new Harness { CancelStart = true };
        h.Controller.Arm(null!, h.RowCentre(1), 1);
        h.Controller.Update(h.RowCentre(4));

        // Cancel is honoured by the grid, which raises RowDragStarting before calling DoDragDrop —
        // the controller's job is only to have handed over exactly one gesture.
        Assert.Single(h.External);
    }

    [Fact]
    public void DragOut_BelowTheThresholdIsStillAClick()
    {
        var h = new Harness();
        h.Controller.Arm(null!, h.RowCentre(1), 1);

        var p = h.RowCentre(1);
        Assert.False(h.Controller.Update(new Point(p.X + 1, p.Y)));
        Assert.Empty(h.External);
    }

    // ── the payload ─────────────────────────────────────────────────────────────

    [Fact]
    public void Format_IsAStableKeyADropTargetCanCheckFor()
    {
        // Changing this string silently breaks every existing drop target.
        Assert.Equal("SkiaGridViewV2.Rows", SKRowDragData.Format);
    }

    [Fact]
    public void StartingArgs_OfferCopyAndMoveByDefault()
    {
        var args = new SKRowDragStartingEventArgs(Array.Empty<object>(), 0);
        Assert.Equal(DragDropEffects.Copy | DragDropEffects.Move, args.AllowedEffects);
    }

    [Fact]
    public void DragText_IsTabSeparatedWithAHeaderRow()
    {
        Sta(() =>
        {
            var rows = new ObservableCollection<Row>
            {
                new() { Symbol = "AAA", Price = 12.5 },
                new() { Symbol = "BBB", Price = 7 },
            };
            var grid = new SkiaGridViewV2 { ItemsSource = rows };
            grid.Columns.Add(new SKGridViewColumn { Header = "Symbol", BindingPath = "Symbol", Width = 90 });
            grid.Columns.Add(new SKGridViewColumn { Header = "Last", BindingPath = "Price", Width = 90, Format = "N2" });

            var text = grid.BuildRowDragText(new List<object> { rows[0], rows[1] });
            var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("Symbol\tLast", lines[0]);
            Assert.Equal("AAA\t12.50", lines[1]);   // the column Format is applied, as on screen
            Assert.Equal("BBB\t7.00", lines[2]);
        });
    }

    [Fact]
    public void DragText_SkipsHiddenColumnsAndFollowsDisplayOrder()
    {
        Sta(() =>
        {
            var rows = new ObservableCollection<Row> { new() { Symbol = "AAA", Price = 1 } };
            var grid = new SkiaGridViewV2 { ItemsSource = rows };
            grid.Columns.Add(new SKGridViewColumn { Header = "Last", BindingPath = "Price", Width = 90, DisplayIndex = 1 });
            grid.Columns.Add(new SKGridViewColumn { Header = "Symbol", BindingPath = "Symbol", Width = 90, DisplayIndex = 0 });
            grid.Columns.Add(new SKGridViewColumn { Header = "Hidden", BindingPath = "Symbol", Width = 90, DisplayIndex = 2, IsVisible = false });

            var lines = grid.BuildRowDragText(new List<object> { rows[0] })
                            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal("Symbol\tLast", lines[0]);
            Assert.DoesNotContain("Hidden", lines[0]);
        });
    }

    [Fact]
    public void DragText_NoColumnsYieldsEmpty()
    {
        Sta(() =>
        {
            var grid = new SkiaGridViewV2 { ItemsSource = new ObservableCollection<Row>() };
            Assert.Equal(string.Empty, grid.BuildRowDragText(new List<object> { new Row() }));
        });
    }

    [Fact]
    public void Payload_ExposesEverythingADropTargetNeeds()
    {
        Sta(() =>
        {
            var grid = new SkiaGridViewV2();
            var col = new SKGridViewColumn { Header = "Symbol", BindingPath = "Symbol", Width = 90 };
            var rows = new object[] { new Row { Symbol = "AAA" } };

            var payload = new SKRowDragData(grid, rows, col, "AAA", "Symbol\tAAA");

            Assert.Same(grid, payload.Source);      // lets a target ignore its own grid's drags
            Assert.Same(col, payload.Column);
            Assert.Equal("AAA", payload.CellText);
            Assert.Single(payload.Rows);
        });
    }

    [Fact]
    public void CompletedArgs_CarryTheEffectTheTargetApplied()
    {
        var args = new SKRowDragCompletedEventArgs(Array.Empty<object>(), DragDropEffects.Move, null, null);
        Assert.Equal(DragDropEffects.Move, args.Effects);
    }
}
