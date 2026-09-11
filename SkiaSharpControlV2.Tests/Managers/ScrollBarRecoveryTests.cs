using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls.Primitives;

using SkiaSharpControlV2;

namespace SkiaSharpControlV2.Tests.Managers;

/// <summary>
/// Grow-then-shrink recovery for an <c>Auto</c> vertical scroll bar.
/// <para>
/// Regression reported against 2.16.1–2.17.0: once the grid was grown until every row fitted, the bar
/// hid correctly but never came back when the grid was shrunk again — permanently, not one pass. The
/// whole defect lives in measured layout, so it is invisible to any test that does not realize a
/// window.
/// </para>
/// </summary>
public class ScrollBarRecoveryTests
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

    /// <summary>
    /// 14 rows in a window that starts too short for them, grows past them, then shrinks back.
    /// Mirrors the reported repro: height 150 → 620 → 150.
    /// </summary>
    [Fact]
    public void AutoBar_HiddenByGrowing_ComesBackWhenShrunk()
    {
        Sta(() =>
        {
            var rows = new List<Row>();
            for (int i = 0; i < 14; i++) rows.Add(new Row { Symbol = "SYM" + i, Price = 100 + i });

            var grid = new SkiaGridViewV2
            {
                ItemsSource = rows,
                VerticalScrollBarVisible = SKScrollBarVisibility.Auto,
                HorizontalScrollBarVisible = SKScrollBarVisibility.Hidden,
            };
            grid.Columns.Add(new SKGridViewColumn { Header = "Symbol", BindingPath = "Symbol", Width = 100 });
            grid.Columns.Add(new SKGridViewColumn { Header = "Price", BindingPath = "Price", Width = 100 });

            var window = new Window { Width = 320, Height = 150, Left = -4000, Top = -4000, Content = grid };
            window.Show();
            Settle(window);

            var bar = (ScrollBar)grid.FindName("VerticalScrollViewer")!;

            var start = bar.Visibility;

            window.Height = 620;      // grow past the rows -> bar should hide
            Settle(window);
            var grown = bar.Visibility;

            window.Height = 150;      // shrink back -> bar MUST come back
            Settle(window);
            var shrunk = bar.Visibility;

            Assert.Equal(Visibility.Visible, start);
            Assert.Equal(Visibility.Collapsed, grown);
            Assert.Equal(Visibility.Visible, shrunk);
        });
    }

    /// <summary>Runs layout plus the control's deferred (DispatcherPriority.Loaded) metrics pass.</summary>
    private static void Settle(Window window)
    {
        for (int i = 0; i < 3; i++)
        {
            window.UpdateLayout();
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
    }
}
