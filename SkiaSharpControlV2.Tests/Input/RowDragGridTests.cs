using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Windows;

using SkiaSharpControlV2;

namespace SkiaSharpControlV2.Tests.Input;

/// <summary>
/// The grid half of row drag, on a realized off-screen grid: whether a drop is allowed to touch the
/// consumer's collection, and what it does to it. This is the only part that mutates caller data, so
/// it is asserted against a real <see cref="SkiaGridViewV2"/> rather than a fake.
/// <para>
/// A drop is raised directly instead of dragging, because there is no physically pressed mouse
/// button in a test run — the gesture that produces these arguments is covered by
/// <c>RowDragStateMachineTests</c>.
/// </para>
/// </summary>
public class RowDragGridTests
{
    /// <summary>WPF controls require STA; xUnit's worker is MTA.</summary>
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
        public override string ToString() => Symbol;
    }

    private static void WithGrid(
        SKRowDragMode mode,
        Action<SkiaGridViewV2, ObservableCollection<Row>, Window> body,
        bool sorted = false)
    {
        var rows = new ObservableCollection<Row>();
        foreach (var sym in new[] { "AAA", "BBB", "CCC", "DDD" })
            rows.Add(new Row { Symbol = sym, Price = 10 });

        var grid = new SkiaGridViewV2
        {
            ItemsSource = rows,
            RowDragMode = mode,
            VerticalScrollBarVisible = SKScrollBarVisibility.Auto,
            HorizontalScrollBarVisible = SKScrollBarVisibility.Hidden,
        };
        grid.Columns.Add(new SKGridViewColumn { Header = "Symbol", BindingPath = "Symbol", Width = 120 });
        grid.Columns.Add(new SKGridViewColumn { Header = "Price", BindingPath = "Price", Width = 80 });
        if (sorted) grid.Columns[0].GridViewColumnSort = SkGridViewColumnSort.Ascending;

        var window = new Window { Width = 320, Height = 160, Left = -4000, Top = -4000, Content = grid };
        window.Show();
        window.UpdateLayout();
        try { body(grid, rows, window); }
        finally { window.Close(); }
    }

    // ── the default-off guarantee ───────────────────────────────────────────────

    [Fact]
    public void RowDragMode_DefaultsToNone()
    {
        // Rule 1 of this repo: new capability, default-off, existing grids unchanged.
        Sta(() => Assert.Equal(SKRowDragMode.None, new SkiaGridViewV2().RowDragMode));
    }

    [Fact]
    public void RowDragIndicatorColor_DefaultsToNull()
    {
        Sta(() => Assert.Null(new SkiaGridViewV2().RowDragIndicatorColor));
    }

    [Fact]
    public void ModeNone_ADropDoesNotTouchTheSource()
    {
        Sta(() => WithGrid(SKRowDragMode.None, (grid, rows, _) =>
        {
            grid.RaiseRowDropped(new SKRowDroppedEventArgs(new object[] { rows[0] }, 3, rows[3]));

            Assert.Equal(new[] { "AAA", "BBB", "CCC", "DDD" }, rows.Select(r => r.Symbol));
        }));
    }

    // ── CanReorderRows ──────────────────────────────────────────────────────────

    [Fact]
    public void CanReorderRows_TrueForAPlainObservableCollection()
    {
        Sta(() => WithGrid(SKRowDragMode.Reorder, (grid, _, _) => Assert.True(grid.CanReorderRows)));
    }

    [Fact]
    public void CanReorderRows_FalseWhileSorted()
    {
        // View order is not source order under a sort, so a move would be undone by the next
        // refresh. The grid says so up front rather than doing something surprising.
        Sta(() => WithGrid(SKRowDragMode.Reorder, (grid, _, _) => Assert.False(grid.CanReorderRows), sorted: true));
    }

    [Fact]
    public void CanReorderRows_FalseForAFixedSizeSource()
    {
        Sta(() =>
        {
            var grid = new SkiaGridViewV2 { ItemsSource = new[] { new Row { Symbol = "A" } } };
            Assert.False(grid.CanReorderRows);
        });
    }

    // ── the reorder itself ──────────────────────────────────────────────────────

    [Fact]
    public void ReorderMode_MovesTheRowInTheSourceCollection()
    {
        Sta(() => WithGrid(SKRowDragMode.Reorder, (grid, rows, _) =>
        {
            // Drop AAA into the gap above DDD.
            grid.RaiseRowDropped(new SKRowDroppedEventArgs(new object[] { rows[0] }, 3, rows[3]));

            Assert.Equal(new[] { "BBB", "CCC", "AAA", "DDD" }, rows.Select(r => r.Symbol));
        }));
    }

    [Fact]
    public void ReorderMode_NullTargetAppends()
    {
        Sta(() => WithGrid(SKRowDragMode.Reorder, (grid, rows, _) =>
        {
            grid.RaiseRowDropped(new SKRowDroppedEventArgs(new object[] { rows[0] }, 4, null));

            Assert.Equal(new[] { "BBB", "CCC", "DDD", "AAA" }, rows.Select(r => r.Symbol));
        }));
    }

    [Fact]
    public void ReorderMode_MovesAMultiRowBlockTogether()
    {
        Sta(() => WithGrid(SKRowDragMode.Reorder, (grid, rows, _) =>
        {
            var block = new object[] { rows[0], rows[2] };   // AAA + CCC
            grid.RaiseRowDropped(new SKRowDroppedEventArgs(block, 4, null));

            Assert.Equal(new[] { "BBB", "DDD", "AAA", "CCC" }, rows.Select(r => r.Symbol));
        }));
    }

    [Fact]
    public void ReorderMode_DoesNothingWhileSorted()
    {
        Sta(() => WithGrid(SKRowDragMode.Reorder, (grid, rows, _) =>
        {
            grid.RaiseRowDropped(new SKRowDroppedEventArgs(new object[] { rows[0] }, 3, rows[3]));

            Assert.Equal(new[] { "AAA", "BBB", "CCC", "DDD" }, rows.Select(r => r.Symbol));
        }, sorted: true));
    }

    [Fact]
    public void NotifyMode_RaisesTheEventButNeverMutates()
    {
        Sta(() => WithGrid(SKRowDragMode.Notify, (grid, rows, _) =>
        {
            SKRowDroppedEventArgs? seen = null;
            grid.RowDropped += (_, e) => seen = e;

            grid.RaiseRowDropped(new SKRowDroppedEventArgs(new object[] { rows[0] }, 3, rows[3]));

            Assert.NotNull(seen);
            Assert.Equal(3, seen!.InsertIndex);
            Assert.Equal(new[] { "AAA", "BBB", "CCC", "DDD" }, rows.Select(r => r.Symbol));
        }));
    }

    [Fact]
    public void HandledSuppressesTheBuiltInMove()
    {
        Sta(() => WithGrid(SKRowDragMode.Reorder, (grid, rows, _) =>
        {
            grid.RowDropped += (_, e) => e.Handled = true;

            grid.RaiseRowDropped(new SKRowDroppedEventArgs(new object[] { rows[0] }, 3, rows[3]));

            Assert.Equal(new[] { "AAA", "BBB", "CCC", "DDD" }, rows.Select(r => r.Symbol));
        }));
    }

    [Fact]
    public void RowDroppedCommand_FiresWithTheSameArgs()
    {
        Sta(() => WithGrid(SKRowDragMode.Reorder, (grid, rows, _) =>
        {
            SKRowDroppedEventArgs? seen = null;
            grid.RowDroppedCommand = new DelegateCommand(p => seen = p as SKRowDroppedEventArgs);

            grid.RaiseRowDropped(new SKRowDroppedEventArgs(new object[] { rows[0] }, 2, rows[2]));

            Assert.NotNull(seen);
            Assert.Equal(2, seen!.InsertIndex);
            Assert.Same(rows[2], seen.TargetItem);
        }));
    }

    private sealed class DelegateCommand : System.Windows.Input.ICommand
    {
        private readonly Action<object?> _execute;
        public DelegateCommand(Action<object?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged;
    }
}
