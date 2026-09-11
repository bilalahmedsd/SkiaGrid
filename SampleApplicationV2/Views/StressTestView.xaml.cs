using SkiaSharpControlV2.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    /// <summary>
    /// The benchmark the project is actually about: load N rows, scroll, and watch frame time
    /// stay flat. Deliberately plain - no grouping, no triggers, no in-cell buttons - so the
    /// number on screen is the cost of the render pipeline itself and nothing else.
    ///
    /// Rows are plain POCOs with no INotifyPropertyChanged. At 100k rows wiring up change
    /// notification per row would dominate the measurement, and it is not needed: the renderer
    /// re-reads every visible cell each frame, so mutating a value and asking for a repaint is
    /// enough.
    /// </summary>
    public partial class StressTestView : UserControl
    {
        private static readonly string[] Sectors =
        {
            "Technology", "Financials", "Energy", "Healthcare",
            "Industrials", "Consumer", "Utilities", "Materials"
        };
        private static readonly string[] Exchanges = { "NYSE", "NSDQ", "ARCA", "BATS", "IEX" };

        private readonly Random _rand = new(20260911);
        // Re-assigned (not mutated in place) on every load: the grid only rebuilds its view when
        // the ItemsSource REFERENCE changes, and SkiaGridViewV2 rejects a null ItemsSource
        // ("Items must be IList"), so clearing via null is not an option.
        private List<StressRow> _rows = new();
        private readonly DispatcherTimer _tickTimer;   // mutates data
        private readonly DispatcherTimer _uiTimer;     // refreshes the headline numbers
        private DispatcherTimer? _autoScrollTimer;
        private long _lastPaintCount;

        public StressTestView()
        {
            InitializeComponent();

            GridMetrics.IsEnabled = true;
            GridMetrics.PercentilesEnabled = true;

            _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _tickTimer.Tick += (s, e) => Tick();

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _uiTimer.Tick += (s, e) => UpdateReadout();
            _uiTimer.Start();

            Unloaded += (s, e) =>
            {
                _tickTimer.Stop();
                _uiTimer.Stop();
                StopAutoScroll();
            };
        }

        // ── data ──────────────────────────────────────────────────────────
        private void Load_Click(object sender, RoutedEventArgs e)
        {
            int count = int.Parse((string)((Button)sender).Tag);

            // Park every timer and the scroll position BEFORE swapping the dataset. A tick or an
            // auto-scroll step that lands against a half-built list, or a scroll offset left over
            // from the previous (differently sized) dataset, is the one way this view can fault.
            bool ticking = _tickTimer.IsEnabled;
            bool scrolling = _autoScrollTimer != null;
            _tickTimer.Stop();
            StopAutoScroll();
            skiaGrid.ScrollToVerticalOffset(0);

            var sw = Stopwatch.StartNew();
            var rows = new List<StressRow>(count);
            for (int i = 0; i < count; i++)
                rows.Add(NewRow(i + 1));
            _rows = rows;

            // A brand-new list instance, so this is a real DP change and the grid rebuilds.
            skiaGrid.ItemsSource = _rows;
            sw.Stop();

            if (ticking) _tickTimer.Start();
            if (scrolling) StartAutoScroll();

            GridMetrics.Reset();
            _lastPaintCount = 0;
            txtRows.Text = count.ToString("N0");
            StatusText.Text =
                $"Generated and bound {count:N0} rows in {sw.ElapsedMilliseconds:N0} ms. " +
                $"Now scroll - watch the frame time stay flat.";
            UpdateReadout();
        }

        private StressRow NewRow(int id)
        {
            double bid = 5 + _rand.NextDouble() * 900;
            return new StressRow
            {
                Id = id,
                Symbol = $"{(char)('A' + _rand.Next(26))}{(char)('A' + _rand.Next(26))}" +
                         $"{(char)('A' + _rand.Next(26))}{_rand.Next(10)}",
                Bid = bid,
                Ask = bid + _rand.NextDouble(),
                Last = bid + _rand.NextDouble() * 0.5,
                ChangePercent = (_rand.NextDouble() - 0.5) * 8,
                Volume = _rand.Next(1_000, 90_000_000),
                Exchange = Exchanges[_rand.Next(Exchanges.Length)],
                Sector = Sectors[_rand.Next(Sectors.Length)],
                UpdatedText = DateTime.Now.ToString("HH:mm:ss"),
            };
        }

        private void Tick()
        {
            int n = _rows.Count;
            if (n == 0) return;

            // Touch a fixed slice per tick so the update cost does not scale with the row count -
            // this mirrors how a real market feed behaves.
            int updates = Math.Min(2000, n);
            string now = DateTime.Now.ToString("HH:mm:ss");
            for (int i = 0; i < updates; i++)
            {
                var r = _rows[_rand.Next(n)];
                double move = (_rand.NextDouble() - 0.5) * 0.01 * r.Last;
                r.Last += move;
                r.Bid = r.Last - _rand.NextDouble() * 0.2;
                r.Ask = r.Last + _rand.NextDouble() * 0.2;
                r.ChangePercent += move;
                r.Volume += _rand.Next(1, 5000);
                r.UpdatedText = now;
            }
            skiaGrid.Refresh();
        }

        // ── readout ───────────────────────────────────────────────────────
        private void UpdateReadout()
        {
            var snap = GridMetrics.GetSnapshot();

            if (snap.Entries.TryGetValue(GridMetrics.RenderPaintSurface, out var paint) &&
                paint.CallCount > 0)
            {
                txtFrame.Text = paint.AvgMs.ToString("F2");
                txtFrame.Foreground = paint.AvgMs < 5
                    ? System.Windows.Media.Brushes.LimeGreen
                    : System.Windows.Media.Brushes.Orange;
                txtFrames.Text = paint.CallCount.ToString("N0");

                long painted = paint.CallCount - _lastPaintCount;
                _lastPaintCount = paint.CallCount;

                long cells = snap.Counters != null &&
                             snap.Counters.TryGetValue(GridMetrics.CounterCellsDrawn, out var c) ? c : 0;
                txtCells.Text = painted > 0 && paint.CallCount > 0
                    ? (cells / Math.Max(paint.CallCount, 1)).ToString("N0")
                    : "-";
            }

            var p = GridMetrics.GetPercentiles(GridMetrics.RenderPaintSurface);
            txtP95.Text = p is { SampleCount: > 0 } ? p.P95Ms.ToString("F2") : "-";

            txtMem.Text = (GC.GetTotalMemory(false) / (1024.0 * 1024.0)).ToString("F0");
        }

        // ── toolbar ───────────────────────────────────────────────────────
        private void LiveTicks_Changed(object sender, RoutedEventArgs e)
        {
            if (LiveTicks.IsChecked == true) _tickTimer.Start();
            else _tickTimer.Stop();
        }

        private void AutoScroll_Click(object sender, RoutedEventArgs e)
        {
            if (_autoScrollTimer != null) StopAutoScroll();
            else StartAutoScroll();
        }

        private void StartAutoScroll()
        {
            if (_autoScrollTimer != null || _rows.Count == 0) return;

            // ScrollToVerticalOffset drives the vertical scroll bar, whose value is a row index.
            double row = 0;
            _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _autoScrollTimer.Tick += (s, args) =>
            {
                int n = _rows.Count;
                if (n == 0) { StopAutoScroll(); return; }   // never divide by zero
                row = (row + 12) % n;
                skiaGrid.ScrollToVerticalOffset(row);
            };
            _autoScrollTimer.Start();
            AutoScrollButton.Content = "Stop scrolling";
        }

        private void StopAutoScroll()
        {
            if (_autoScrollTimer == null) return;
            _autoScrollTimer.Stop();
            _autoScrollTimer = null;
            AutoScrollButton.Content = "Auto-scroll";
        }

        private void ResetMetrics_Click(object sender, RoutedEventArgs e)
        {
            GridMetrics.Reset();
            _lastPaintCount = 0;
            StatusText.Text = "Metrics reset. Scroll to collect a fresh sample.";
            UpdateReadout();
        }
    }

    /// <summary>Plain POCO row - see the note on <see cref="StressTestView"/>.</summary>
    public class StressRow
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = "";
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Last { get; set; }
        public double ChangePercent { get; set; }
        public long Volume { get; set; }
        public string Exchange { get; set; } = "";
        public string Sector { get; set; } = "";
        public string UpdatedText { get; set; } = "";
    }
}
