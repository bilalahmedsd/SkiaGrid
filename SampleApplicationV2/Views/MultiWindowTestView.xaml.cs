using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SampleApplicationV2.Views
{
    /// <summary>
    /// Multi-window stress-test harness for verifying the shared-static-state fixes.
    /// Launches additional windows hosting live-data demo views so the user can confirm:
    ///   1. Selection HashSet is per-instance — selected rows do not flicker across windows.
    ///   2. SKPaintCache / SKTriggerStateCache are refcounted — closing one window does not
    ///      invalidate paints/state used by other still-live windows.
    /// </summary>
    public partial class MultiWindowTestView : UserControl
    {
        private readonly List<GridHostWindow> _openWindows = new();
        private int _openIndex; // staggers position of single-launch windows

        public MultiWindowTestView()
        {
            InitializeComponent();
        }

        // ── Single-window launchers ────────────────────────────────────

        private void OpenWatchlist_Click(object sender, RoutedEventArgs e)
            => OpenStaggered("Watchlist", new WatchlistView());

        private void OpenTimeAndSale_Click(object sender, RoutedEventArgs e)
            => OpenStaggered("Time & Sales", new TimeAndSaleView());

        private void OpenTradingMonitor_Click(object sender, RoutedEventArgs e)
            => OpenStaggered("Trading Monitor", new TradingMonitorView());

        private void OpenOptionsChain_Click(object sender, RoutedEventArgs e)
            => OpenStaggered("Options Chain", new OptionsChainView());

        private void OpenGroupedGrid_Click(object sender, RoutedEventArgs e)
            => OpenStaggered("Grouped Grid", new GroupedGridDemo());

        private void OpenBasicGrid_Click(object sender, RoutedEventArgs e)
            => OpenStaggered("Basic Grid", new BasicGridDemo());

        private void OpenPositionSummary_Click(object sender, RoutedEventArgs e)
            => OpenStaggered("Position Summary", new GlobalPositionSummaryView());

        private void OpenPerformance_Click(object sender, RoutedEventArgs e)
            => OpenStaggered("Performance Dashboard", new PerformanceDashboardView());

        // ── Tiled multi-window scenarios ───────────────────────────────

        private void Tile2_Click(object sender, RoutedEventArgs e)
        {
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;
            double w = screenW / 2;
            double h = screenH * 0.7;
            double top = screenH * 0.15;
            Open("Watchlist (1 of 2)", new WatchlistView(),    0,     top, w, h);
            Open("Time & Sales (2 of 2)", new TimeAndSaleView(), w,    top, w, h);
        }

        private void Tile4_Click(object sender, RoutedEventArgs e)
        {
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;
            double w = screenW / 2;
            double h = screenH / 2;
            Open("Watchlist (TL)",       new WatchlistView(),     0, 0, w, h);
            Open("Time & Sales (TR)",    new TimeAndSaleView(),   w, 0, w, h);
            Open("Trading Monitor (BL)", new TradingMonitorView(), 0, h, w, h);
            Open("Options Chain (BR)",   new OptionsChainView(),  w, h, w, h);
        }

        private void Tile6_Click(object sender, RoutedEventArgs e)
        {
            // 3x2 grid — heavy stress test
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;
            double w = screenW / 3;
            double h = screenH / 2;
            Open("Watchlist 1",     new WatchlistView(),     0,     0, w, h);
            Open("Watchlist 2",     new WatchlistView(),     w,     0, w, h);
            Open("Time & Sales",    new TimeAndSaleView(),   w * 2, 0, w, h);
            Open("Trading Monitor", new TradingMonitorView(), 0,     h, w, h);
            Open("Options Chain",   new OptionsChainView(),   w,     h, w, h);
            Open("Grouped Grid",    new GroupedGridDemo(),    w * 2, h, w, h);
        }

        // ── Window management ──────────────────────────────────────────

        private void CloseAll_Click(object sender, RoutedEventArgs e)
        {
            // Snapshot + iterate — Closed handler will mutate _openWindows
            foreach (var w in _openWindows.ToArray())
                w.Close();
            _openIndex = 0;
        }

        private void CloseLast_Click(object sender, RoutedEventArgs e)
        {
            if (_openWindows.Count == 0) return;
            _openWindows[_openWindows.Count - 1].Close();
        }

        // ── Internals ──────────────────────────────────────────────────

        private void OpenStaggered(string title, UserControl view)
        {
            // Stagger non-tiled windows diagonally so they don't fully cover each other
            const double w = 900;
            const double h = 550;
            double left = 80 + (_openIndex * 30) % 400;
            double top = 80 + (_openIndex * 30) % 250;
            _openIndex++;
            Open(title, view, left, top, w, h);
        }

        private void Open(string title, UserControl view, double left, double top, double width, double height)
        {
            var win = new GridHostWindow($"[Test] {title}", view, left, top, width, height);
            win.Closed += (s, e) =>
            {
                _openWindows.Remove(win);
                UpdateStatus();
            };
            _openWindows.Add(win);
            win.Show();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            txtCount.Text = _openWindows.Count.ToString();
            if (_openWindows.Count == 0)
            {
                txtStatus.Text = "No test windows open";
            }
            else
            {
                txtStatus.Text = $"{_openWindows.Count} window(s) running. Click rows to verify highlight persists across all windows.";
            }
        }
    }
}
