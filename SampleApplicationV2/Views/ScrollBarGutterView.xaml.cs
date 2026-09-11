using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

using SkiaSharpControlV2;

namespace SampleApplicationV2.Views
{
    /// <summary>
    /// Demonstrates the vertical scroll bar's gutter behaviour — the PO-reported "the grid always
    /// reserves an empty scrollbar gutter".
    /// <para>
    /// Two things are made observable. First the auto-visibility threshold: sweep the row count and
    /// the bar must appear only once the rows genuinely overflow the row band, not ~3 rows early.
    /// Second the reserved gutter itself: the grid sits on a magenta backdrop, so any strip of
    /// magenta showing to the right of the columns is space the grid took and did not use.
    /// </para>
    /// <para>
    /// The readout reads the control's own scroll bar and canvas through <c>FindName</c> — those
    /// x:Name fields are internal, but the namescope lookup is public, so a consumer can measure the
    /// real numbers instead of trusting the picture.
    /// </para>
    /// </summary>
    public partial class ScrollBarGutterView : UserControl
    {
        private readonly ObservableCollection<GutterRow> _rows = new();
        private readonly DispatcherTimer _readoutTimer;
        private bool _initialized;

        public ScrollBarGutterView()
        {
            DataContext = _rows;
            InitializeComponent();

            SetRowCount(8);
            _initialized = true;

            // ActualWidth / ActualHeight only settle after a layout pass, and the control's own
            // metrics recalculation is deferred to DispatcherPriority.Loaded — so poll instead of
            // reading straight after a change.
            _readoutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _readoutTimer.Tick += (s, e) => UpdateReadout();
            _readoutTimer.Start();

            Unloaded += (s, e) => _readoutTimer.Stop();
        }

        // ── Row count ───────────────────────────────────────────────────

        private void SetRowCount(int count)
        {
            while (_rows.Count > count) _rows.RemoveAt(_rows.Count - 1);
            while (_rows.Count < count)
            {
                int i = _rows.Count + 1;
                _rows.Add(new GutterRow { Index = i, Symbol = "SYM" + i.ToString("D3"), Price = 100 + (i * 0.25) });
            }

            skiaGrid.Refresh();
            if (RowCountText != null) RowCountText.Text = count + " rows";
        }

        private void RowSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_initialized) return;
            SetRowCount((int)e.NewValue);
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && int.TryParse(b.Tag?.ToString(), out var n))
                RowSlider.Value = n;
        }

        /// <summary>Exactly as many rows as the band holds — the boundary case that must NOT scroll.</summary>
        private void FillExactly_Click(object sender, RoutedEventArgs e) => RowSlider.Value = RowsThatFit();

        /// <summary>One row past the band — the first count that genuinely needs a scroll bar.</summary>
        private void OverflowByOne_Click(object sender, RoutedEventArgs e) => RowSlider.Value = RowsThatFit() + 1;

        private int RowsThatFit()
        {
            var band = RowBandHeight();
            var rowHeight = EffectiveRowHeight();
            return (band > 0 && rowHeight > 0) ? (int)Math.Floor(band / rowHeight) : 10;
        }

        /// <summary>
        /// The row height the control is actually using, derived from PUBLIC API only — the control's
        /// own <c>RowHeight</c> is internal, so a consumer has to reproduce the rule: the explicit
        /// <c>SKRowHeight</c> when one is set, otherwise the font size plus the density's padding
        /// (Compact +3, Normal +6).
        /// </summary>
        private double EffectiveRowHeight()
        {
            if (skiaGrid.SKRowHeight > 0) return skiaGrid.SKRowHeight;
            double padding = skiaGrid.Density == SKGridDensity.Compact ? 3 : 6;
            return skiaGrid.SKFontSize + padding;
        }

        // ── Scroll bar modes (runtime changes exercise the DP callbacks) ──

        private void VerticalMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            skiaGrid.VerticalScrollBarVisible = SelectedMode(VerticalMode);
        }

        private void HorizontalMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            skiaGrid.HorizontalScrollBarVisible = SelectedMode(HorizontalMode);
        }

        private static SKScrollBarVisibility SelectedMode(ComboBox box) =>
            (box.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
            {
                "Visible" => SKScrollBarVisibility.Visible,
                "Hidden" => SKScrollBarVisibility.Hidden,
                _ => SKScrollBarVisibility.Auto,
            };

        /// <summary>
        /// Widens the columns past the grid's own width. That is the case where the canvas is sized by
        /// the AVAILABLE width rather than by the columns — so if the control mis-measures the scroll
        /// bar, the bar ends up drawn over the last column instead of beside it.
        /// </summary>
        private void WideColumns_Changed(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            bool wide = WideColumns.IsChecked == true;
            var cols = skiaGrid.Columns;
            if (cols == null || cols.Count < 3) return;

            ((SKGridViewColumn)cols[1]).Width = wide ? 190 : 110;
            ((SKGridViewColumn)cols[2]).Width = wide ? 190 : 110;
            skiaGrid.Refresh();
        }

        // ── Live measurement ────────────────────────────────────────────

        private ScrollBar? VerticalBar() => skiaGrid.FindName("VerticalScrollViewer") as ScrollBar;
        private FrameworkElement? RowBand() => skiaGrid.FindName("skiaContainer") as FrameworkElement;
        private FrameworkElement? GridCanvas() => skiaGrid.FindName("SkiaCanvas") as FrameworkElement;
        private Grid? ControlGrid() => skiaGrid.FindName("MainGrid") as Grid;

        /// <summary>
        /// The row band = MainGrid's star row. Read it from the ROW DEFINITION, not from
        /// skiaContainer: that container is a vertical StackPanel sized by its only child, the canvas,
        /// whose height the control derives from this very band. Reading the container would show the
        /// canvas height wearing the band's label — and would go stale exactly when the bar is hidden.
        /// </summary>
        private double RowBandHeight()
        {
            var grid = ControlGrid();
            if (grid != null && grid.RowDefinitions.Count > 1)
            {
                var band = grid.RowDefinitions[1].ActualHeight;
                if (band > 0) return band;
            }
            return grid?.ActualHeight ?? 0;
        }

        private double ColumnsWidth()
        {
            double sum = 0;
            foreach (var c in skiaGrid.Columns) if (c is SKGridViewColumn col && col.IsVisible) sum += col.Width;
            return sum;
        }

        private void UpdateReadout()
        {
            var vBar = VerticalBar();
            var mainGrid = ControlGrid();
            var canvas = GridCanvas();
            if (vBar == null || mainGrid == null || canvas == null) return;

            bool barShown = vBar.Visibility == Visibility.Visible;
            double barWidth = barShown ? vBar.ActualWidth : 0;
            double band = RowBandHeight();
            double contentHeight = _rows.Count * EffectiveRowHeight();

            // The gutter is the scroll bar's own grid column — measure it directly. Deriving it as
            // "control width minus canvas width" is WRONG: it also counts the ordinary empty area
            // when the columns are simply narrower than the control, which has nothing to do with
            // the scroll bar.
            double gutter = mainGrid.ColumnDefinitions.Count > 1
                ? mainGrid.ColumnDefinitions[1].ActualWidth
                : 0;

            Readout.Text =
                "rows            " + _rows.Count + "\n" +
                "row height      " + EffectiveRowHeight().ToString("F0") + " px\n" +
                "content height  " + contentHeight.ToString("F0") + " px\n" +
                "row band        " + band.ToString("F0") + " px\n" +
                "overflow        " + (contentHeight - band).ToString("F0") + " px\n" +
                "\n" +
                "vertical bar    " + vBar.Visibility + "\n" +
                "bar width       " + barWidth.ToString("F0") + " px\n" +
                "scroll range    " + vBar.Maximum.ToString("F0") + " px\n" +
                "\n" +
                "columns total   " + ColumnsWidth().ToString("F0") + " px\n" +
                "control width   " + mainGrid.ActualWidth.ToString("F0") + " px\n" +
                "canvas width    " + canvas.ActualWidth.ToString("F0") + " px\n" +
                "gutter column  " + gutter.ToString("F0") + " px";

            bool overflows = contentHeight > band;
            bool auto = skiaGrid.VerticalScrollBarVisible == SKScrollBarVisibility.Auto;

            if (auto && !overflows && barShown)
                Fail("REGRESSION: bar shown with nothing to scroll");
            else if (auto && overflows && !barShown)
                Fail("REGRESSION: content overflows but no bar");
            else if (!barShown && gutter > 0.5)
                Fail("REGRESSION: " + gutter.ToString("F0") + " px gutter with no bar in it");
            else
                Pass(barShown ? "OK — bar shown, and it is scrollable" : "OK — no bar, no gutter");
        }

        private void Fail(string text)
        {
            Verdict.Text = text;
            Verdict.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }

        private void Pass(string text)
        {
            Verdict.Text = text;
            Verdict.Foreground = System.Windows.Media.Brushes.LimeGreen;
        }
    }

    public class GutterRow : INotifyPropertyChanged
    {
        public int Index { get; set; }
        public string Symbol { get; set; } = "";
        public double Price { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
