using SampleApplicationV2.Models;
using SkiaSharpControlV2.Diagnostics;
using SkiaSharpControlV2.Renderer;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    public partial class PerformanceDashboardView : UserControl
    {
        private readonly PerformanceDashboardViewModel _vm = new();
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _uiTimer;
        private long _lastPaintCalls;
        private DateTime _lastFpsCheck = DateTime.UtcNow;

        public PerformanceDashboardView()
        {
            DataContext = _vm;
            InitializeComponent();

            // Enable metrics on load
            GridMetrics.IsEnabled = true;

            // Initialize metric items for all known operations
            InitializeMetrics();

            // Data collection timer (configurable rate)
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _refreshTimer.Tick += (s, e) => CollectMetrics();
            _refreshTimer.Start();

            // UI refresh timer (fixed 250ms for smooth grid updates)
            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _uiTimer.Tick += (s, e) => metricsGrid.Refresh();
            _uiTimer.Start();

            Unloaded += (s, e) =>
            {
                _refreshTimer?.Stop();
                _uiTimer?.Stop();
            };
        }

        private void InitializeMetrics()
        {
            var thresholds = PerformanceMetricItem.DefaultThresholds;

            // Only include operations that are timer-wrapped (not counter-only).
            // Per-cell, per-frame operations (CellDraw, TriggerEvaluate, Reflection.Get) are counter-only —
            // stopwatch overhead in hot loops would distort measurements. See the counters panel above.
            string[] operations = {
                // Rendering (per-frame)
                GridMetrics.RenderDraw,
                GridMetrics.RenderPaintSurface,
                // Data / CollectionView (per-batch)
                GridMetrics.CollectionViewRefresh,
                GridMetrics.DataUpdateCollection,
                GridMetrics.DataFlattenGrouped,
                GridMetrics.DataFlattenRows,
                GridMetrics.DataInsertNewItem,
                // Sort/Filter/Group (per-user-action)
                GridMetrics.SortApply,
                GridMetrics.FilterApply,
                GridMetrics.GroupAggregate,
                // Input (per-event)
                GridMetrics.InputMouseClick,
                GridMetrics.InputMouseWheel,
                GridMetrics.InputKeyDown,
                // Selection/Scroll (per-event)
                GridMetrics.SelectionUpdate,
                GridMetrics.ScrollUpdate,
                // Misc
                GridMetrics.ColumnUpdate,
                GridMetrics.ExportData,
            };

            foreach (var op in operations)
            {
                _vm.Metrics.Add(new PerformanceMetricItem
                {
                    Operation = op,
                    ThresholdMs = thresholds.GetValueOrDefault(op, 5.0)
                });
            }
        }

        private void CollectMetrics()
        {
            var snapshot = GridMetrics.GetSnapshot();

            // Update each metric item
            foreach (var item in _vm.Metrics)
            {
                if (snapshot.Entries.TryGetValue(item.Operation, out var entry))
                {
                    item.UpdateFrom(entry);
                }
            }

            // Add any new operations that weren't in the initial list
            foreach (var kvp in snapshot.Entries)
            {
                if (!_vm.Metrics.Any(m => m.Operation == kvp.Key))
                {
                    var newItem = new PerformanceMetricItem
                    {
                        Operation = kvp.Key,
                        ThresholdMs = PerformanceMetricItem.DefaultThresholds.GetValueOrDefault(kvp.Key, 5.0)
                    };
                    newItem.UpdateFrom(kvp.Value);
                    _vm.Metrics.Add(newItem);
                }
            }

            // Update key indicators
            UpdateIndicators(snapshot);
        }

        private void UpdateIndicators(MetricsSnapshot snapshot)
        {
            // Frame time
            if (snapshot.Entries.TryGetValue(GridMetrics.RenderDraw, out var renderEntry))
            {
                txtFrameTime.Text = renderEntry.AvgMs.ToString("F2");
                txtFrameTime.Foreground = renderEntry.AvgMs <= 3.0
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 204, 0))
                    : renderEntry.AvgMs <= 5.0
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 170, 0))
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68));
            }

            // FPS (from PaintSurface call rate)
            if (snapshot.Entries.TryGetValue(GridMetrics.RenderPaintSurface, out var paintEntry))
            {
                var elapsed = (DateTime.UtcNow - _lastFpsCheck).TotalSeconds;
                if (elapsed > 0.5)
                {
                    var fps = (paintEntry.CallCount - _lastPaintCalls) / elapsed;
                    txtFPS.Text = fps.ToString("F0");
                    _lastPaintCalls = paintEntry.CallCount;
                    _lastFpsCheck = DateTime.UtcNow;
                }
            }

            // Total calls
            long totalCalls = snapshot.Entries.Values.Sum(e => e.CallCount);
            txtTotalCalls.Text = totalCalls.ToString("N0");

            // Memory
            var workingSet = Process.GetCurrentProcess().WorkingSet64;
            txtMemory.Text = $"{workingSet / (1024 * 1024):N0} MB";

            // Cache sizes
            txtPaintCache.Text = SKPaintCache.Count.ToString();

            // Counters
            if (snapshot.Counters != null)
            {
                txtCellsDrawn.Text = snapshot.Counters.GetValueOrDefault(GridMetrics.CounterCellsDrawn).ToString("N0");
                txtTriggersEvaluated.Text = snapshot.Counters.GetValueOrDefault(GridMetrics.CounterTriggersEvaluated).ToString("N0");
                txtTriggersMatched.Text = snapshot.Counters.GetValueOrDefault(GridMetrics.CounterTriggersMatched).ToString("N0");
                txtPaintHits.Text = snapshot.Counters.GetValueOrDefault(GridMetrics.CounterPaintCacheHits).ToString("N0");
                txtPaintMisses.Text = snapshot.Counters.GetValueOrDefault(GridMetrics.CounterPaintCacheMisses).ToString("N0");
                txtReflectionCalls.Text = snapshot.Counters.GetValueOrDefault(GridMetrics.CounterReflectionCalls).ToString("N0");
                txtFilterEvaluations.Text = snapshot.Counters.GetValueOrDefault(GridMetrics.CounterFilterEvaluations).ToString("N0");
                txtCollectionChanges.Text = snapshot.Counters.GetValueOrDefault(GridMetrics.CounterCollectionChanges).ToString("N0");
            }
        }

        // ── Event Handlers ──

        private void EnableChanged(object sender, RoutedEventArgs e)
        {
            GridMetrics.IsEnabled = chkEnabled.IsChecked == true;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            GridMetrics.Reset();
            foreach (var item in _vm.Metrics)
            {
                item.Calls = 0;
                item.AvgMs = 0;
                item.MinMs = 0;
                item.MaxMs = 0;
                item.LastMs = 0;
                item.TotalMs = 0;
            }
            _lastPaintCalls = 0;
            _lastFpsCheck = DateTime.UtcNow;
        }

        private void Snapshot_Click(object sender, RoutedEventArgs e)
        {
            var snapshot = GridMetrics.GetSnapshot();
            Debug.WriteLine(snapshot.ToString());
        }

        private void RefreshRate_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshTimer != null && cmbRefreshRate.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _refreshTimer.Interval = TimeSpan.FromMilliseconds(int.Parse(tag));
            }
        }
    }

    public class PerformanceDashboardViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<PerformanceMetricItem> Metrics { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
