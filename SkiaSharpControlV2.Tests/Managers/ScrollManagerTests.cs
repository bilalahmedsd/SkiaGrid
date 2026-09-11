using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using SkiaSharpControlV2.Managers;

namespace SkiaSharpControlV2.Tests.Managers;

/// <summary>
/// Tests the vertical scroll bar's viewport math — the part the PO reported as "the grid always
/// reserves an empty scrollbar gutter".
/// <para>
/// The bar used to measure its viewport as <c>MainGrid.ActualHeight</c> (which also spans the column
/// header row and the horizontal bar row) and its content as <c>totalRows + 3.3</c> rows. The fudge
/// over-corrected the wrong viewport, so the bar went Visible with no real overflow. Both sides are
/// now measured against the row band alone.
/// </para>
/// </summary>
public class ScrollManagerTests
{
    private const float RowHeight = 14f;      // the default Compact row: font 11 + 3
    private const double RowBand = 140d;      // exactly 10 rows of viewport

    /// <summary>
    /// Runs a test body on an STA thread. Constructing a WPF <see cref="ScrollBar"/> pulls in
    /// InputManager, which throws "The calling thread must be STA" on xUnit's default worker.
    /// (The SkiaSharp pixel harness in ButtonRendererTests needs no such thing — SKSurface and
    /// BitmapSource are apartment-agnostic; it is the WPF *controls* that demand STA.)
    /// </summary>
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

    /// <summary>
    /// Builds a ScrollManager over real WPF scroll bars, with the row band supplied directly so the
    /// test does not depend on a layout pass.
    /// </summary>
    private static (ScrollManager Manager, ScrollBar Vertical, ScrollBar Horizontal) Build(
        int totalRows,
        double rowAreaHeight = RowBand,
        SKScrollBarVisibility vMode = SKScrollBarVisibility.Auto,
        SKScrollBarVisibility hMode = SKScrollBarVisibility.Hidden,
        double mainGridHeight = 158d,   // the 140 px row band + the 18 px column-header row
        double columnsWidth = 100d)
    {
        var vScroll = new ScrollBar { Orientation = Orientation.Vertical };
        var hScroll = new ScrollBar { Orientation = Orientation.Horizontal };
        var mainGrid = new Grid();

        // Give MainGrid a real ActualWidth/ActualHeight. It is TALLER than the row band by the
        // header (and the horizontal bar when shown) — that gap is what the old code measured
        // against, and is why the fudge existed.
        mainGrid.Measure(new Size(500, mainGridHeight));
        mainGrid.Arrange(new Rect(0, 0, 500, mainGridHeight));

        var manager = new ScrollManager(
            () => hScroll,
            () => vScroll,
            () => mainGrid,
            () => rowAreaHeight,
            () => columnsWidth,
            () => totalRows,
            () => RowHeight,
            () => null,
            () => hMode,
            () => vMode,
            () => false,
            () => null,
            () => null,
            () => null,
            () => { });

        return (manager, vScroll, hScroll);
    }

    [Fact]
    public void ContentExactlyFillsTheRowBand_NoScrollBar()
    {
        Sta(() =>
        {
            // 10 rows x 14 px = 140 px = the row band exactly. Nothing to scroll.
            var (manager, vScroll, _) = Build(totalRows: 10);

            manager.UpdateScrollValues();

            Assert.Equal(Visibility.Collapsed, vScroll.Visibility);
            Assert.Equal(0d, vScroll.Maximum);
        });
    }

    [Fact]
    public void ContentShorterThanTheRowBand_NoScrollBar()
    {
        Sta(() =>
        {
            // 8 rows = 112 px in a 140 px band — 28 px of slack, nothing to scroll. This is the
            // reported case: the old math asked (8 + 3.3) * 14 > 158, i.e. 158.2 > 158, so the bar
            // went Visible and reserved a gutter over content that already fits.
            var (manager, vScroll, _) = Build(totalRows: 8);

            manager.UpdateScrollValues();

            Assert.Equal(Visibility.Collapsed, vScroll.Visibility);
        });
    }

    [Fact]
    public void ContentOverflows_ScrollBarAppears_AndRangeIsExact()
    {
        Sta(() =>
        {
            // 15 rows = 210 px in a 140 px band → exactly 70 px of overflow, no more.
            var (manager, vScroll, _) = Build(totalRows: 15);

            manager.UpdateScrollValues();

            Assert.Equal(Visibility.Visible, vScroll.Visibility);
            Assert.Equal(70d, vScroll.Maximum);
            Assert.Equal(RowBand, vScroll.ViewportSize);
        });
    }

    [Fact]
    public void ScrollRangeStopsAtTheLastRow_NoOvershoot()
    {
        Sta(() =>
        {
            // Scrolling to Maximum must land the last row flush with the bottom of the band:
            // offset + viewport == content. The old math added 3.3 rows of blank space past the end.
            const int rows = 40;
            var (manager, vScroll, _) = Build(totalRows: rows);

            manager.UpdateScrollValues();

            Assert.Equal(rows * RowHeight, vScroll.Maximum + RowBand);
        });
    }

    [Fact]
    public void ViewportIsTheRowBand_NotTheWholeControl()
    {
        Sta(() =>
        {
            // MainGrid is 158 px but the rows only get 140 px. Measuring the viewport against the
            // taller number understates the overflow — that mismatch is what the fudge hid.
            var (manager, vScroll, _) = Build(totalRows: 11); // 154 px of content vs a 140 px band

            manager.UpdateScrollValues();

            Assert.Equal(Visibility.Visible, vScroll.Visibility);
            Assert.Equal(14d, vScroll.Maximum);
        });
    }

    [Fact]
    public void RowBandNotLaidOutYet_FallsBackToTheGridHeight()
    {
        Sta(() =>
        {
            // Before the first layout pass skiaContainer.ActualHeight is 0; using it raw would make every
            // grid look overflowing. Fall back to the whole grid's height for that transient.
            var (manager, vScroll, _) = Build(totalRows: 5, rowAreaHeight: 0d);

            manager.UpdateScrollValues();

            Assert.Equal(Visibility.Collapsed, vScroll.Visibility); // 70 px of content vs the 158 px fallback
        });
    }

    [Fact]
    public void BeforeFirstLayout_DoesNotThrow()
    {
        // Regression: the grid is measured before its first layout pass, so MainGrid.ActualWidth is 0
        // while a visible bar already reports its real width — the subtraction went negative and
        // ScrollBar.ViewportSize REJECTS a negative value with an ArgumentException (it does not
        // coerce), which crashed the whole app during startup.
        Sta(() =>
        {
            var vScroll = new ScrollBar { Orientation = Orientation.Vertical, Visibility = Visibility.Visible };
            var hScroll = new ScrollBar { Orientation = Orientation.Horizontal };
            var mainGrid = new Grid();   // never measured/arranged: ActualWidth == ActualHeight == 0

            var manager = new ScrollManager(
                () => hScroll, () => vScroll, () => mainGrid,
                () => 0d, () => 100d, () => 5, () => 14f,
                () => null, () => SKScrollBarVisibility.Auto, () => SKScrollBarVisibility.Auto,
                () => false, () => null, () => null, () => null, () => { });

            manager.UpdateScrollValues();   // must not throw

            Assert.True(hScroll.ViewportSize >= 0);
            Assert.True(vScroll.Maximum >= 0);
        });
    }

    [Fact]
    public void BarWidth_IsKnownEvenBeforeLayoutHasArrangedIt()
    {
        // The canvas is sized in the same pass that makes the bar visible, and ActualWidth is still 0
        // then. Reporting 0 would size the canvas a full bar-width too wide and the bar would draw
        // over the last column, so an un-arranged visible bar must still report a usable width.
        Sta(() =>
        {
            var bar = new ScrollBar { Orientation = Orientation.Vertical, Visibility = Visibility.Visible };

            Assert.Equal(0d, bar.ActualWidth);                                  // never laid out
            Assert.True(ScrollManager.EffectiveVerticalBarWidth(bar) > 0,
                "an un-arranged visible bar reported zero width");
        });
    }

    [Fact]
    public void BarWidth_HonorsAnExplicitWidthBeforeLayout()
    {
        // A consumer that set VerticalScrollBarWidth must get THAT number, not the system metric.
        Sta(() =>
        {
            var bar = new ScrollBar { Orientation = Orientation.Vertical, Visibility = Visibility.Visible, Width = 10 };

            Assert.Equal(10d, ScrollManager.EffectiveVerticalBarWidth(bar));
        });
    }

    [Fact]
    public void BarWidth_IsZeroWhenTheBarIsNotShown()
    {
        Sta(() =>
        {
            var collapsed = new ScrollBar { Orientation = Orientation.Vertical, Visibility = Visibility.Collapsed, Width = 10 };

            Assert.Equal(0d, ScrollManager.EffectiveVerticalBarWidth(collapsed));
        });
    }

    // ── Visibility callback + re-entrancy ───────────────────────────

    [Fact]
    public void VisibilityCallback_FiresOnlyOnARealChange()
    {
        Sta(() =>
        {
            var seen = new List<bool>();
            var vScroll = new ScrollBar { Orientation = Orientation.Vertical, Visibility = Visibility.Collapsed };
            var hScroll = new ScrollBar { Orientation = Orientation.Horizontal };
            var mainGrid = new Grid();
            mainGrid.Measure(new Size(500, 158));
            mainGrid.Arrange(new Rect(0, 0, 500, 158));

            int rows = 20;   // 280 px of content vs a 140 px band -> overflows
            var manager = new ScrollManager(
                () => hScroll, () => vScroll, () => mainGrid,
                () => RowBand, () => 100d, () => rows, () => RowHeight,
                () => null, () => SKScrollBarVisibility.Hidden, () => SKScrollBarVisibility.Auto,
                () => false, () => null, () => null, () => (Action<bool>)(v => seen.Add(v)),
                () => { });

            manager.UpdateScrollValues();          // Collapsed -> Visible
            manager.UpdateScrollValues();          // no change
            manager.UpdateScrollValues();          // no change
            Assert.Equal(new[] { true }, seen);    // exactly one callback, not one per pass

            rows = 2;                              // 28 px -> fits again
            manager.UpdateScrollValues();          // Visible -> Collapsed
            manager.UpdateScrollValues();          // no change
            Assert.Equal(new[] { true, false }, seen);
        });
    }

    [Fact]
    public void VisibilityCallback_ResizingColumnsFromIt_DoesNotRecurse_AndOuterPassSeesTheNewWidths()
    {
        Sta(() =>
        {
            // The supported consumer pattern: re-fit the columns when the bar appears. In the real
            // control setting SKGridViewColumn.Width re-enters UpdateScrollValues, so the nested call
            // must be dropped -- and the OUTER pass must still finish with the new width, or the
            // consumer's fit would land a frame late and the columns would visibly jump.
            var vScroll = new ScrollBar { Orientation = Orientation.Vertical, Visibility = Visibility.Collapsed };
            var hScroll = new ScrollBar { Orientation = Orientation.Horizontal };
            var mainGrid = new Grid();
            mainGrid.Measure(new Size(500, 158));
            mainGrid.Arrange(new Rect(0, 0, 500, 158));

            double columnsWidth = 100;
            int callbacks = 0;
            ScrollManager? manager = null;

            manager = new ScrollManager(
                () => hScroll, () => vScroll, () => mainGrid,
                () => RowBand, () => columnsWidth, () => 20, () => RowHeight,
                () => null, () => SKScrollBarVisibility.Hidden, () => SKScrollBarVisibility.Auto,
                () => false, () => null, () => null,
                () => (Action<bool>)(v =>
                {
                    callbacks++;
                    columnsWidth = 900;             // consumer re-fits its columns...
                    manager!.UpdateScrollValues();  // ...which re-enters. Must no-op, not loop.
                }),
                () => { });

            manager.UpdateScrollValues();

            Assert.Equal(1, callbacks);

            // The outer pass ran on AFTER the callback, so it used 900, not 100.
            double barWidth = ScrollManager.EffectiveVerticalBarWidth(vScroll);
            double viewport = 500d - barWidth;
            Assert.Equal(900d - viewport, hScroll.Maximum);
            Assert.Equal(viewport, hScroll.ViewportSize);
        });
    }

    [Theory]
    [InlineData(SKScrollBarVisibility.Visible)]
    [InlineData(SKScrollBarVisibility.Hidden)]
    public void NonAutoModes_AreLeftAloneByTheAutoLogic(SKScrollBarVisibility mode)
    {
        Sta(() =>
        {
            // Auto-visibility must only act in Auto mode; an explicit choice is the consumer's, and the
            // DP callback owns it.
            var (manager, vScroll, _) = Build(totalRows: 3, vMode: mode);
            vScroll.Visibility = Visibility.Visible;

            manager.UpdateScrollValues();

            Assert.Equal(Visibility.Visible, vScroll.Visibility);
        });
    }
}
