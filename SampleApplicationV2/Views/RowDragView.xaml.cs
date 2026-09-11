using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using SkiaSharpControlV2;

namespace SampleApplicationV2.Views
{
    /// <summary>
    /// Row drag (2.18.0) demo. Exercises every branch of the feature by hand, because none of it is
    /// reachable from a unit test: the drag needs a physically pressed mouse button.
    ///
    /// What to check here:
    ///  • Reorder — drag a row, the source collection order changes (the # column renumbers).
    ///  • Notify  — the indicator still tracks, but nothing moves; only the log fires.
    ///  • None    — no indicator, no events (this is what every existing grid gets by default).
    ///  • Ctrl/Shift-select several rows, then drag one of them: the whole block moves together.
    ///  • Tick "Sort by Symbol": CanReorderRows goes false and a drop no longer moves anything.
    ///  • Tick "Live ticks": a 250 ms feed keeps repainting mid-drag — the indicator must not flicker
    ///    or lose the drop position.
    ///  • Escape mid-drag abandons it; dragging past the top/bottom edge auto-scrolls.
    /// </summary>
    public partial class RowDragView : UserControl
    {
        private readonly ObservableCollection<DragRow> _rows = new();
        private readonly DispatcherTimer _tickTimer;
        private readonly Random _random = new(7);
        private readonly List<string> _log = new();
        private DropTargetWindow? _dropTarget;

        private static readonly string[] Symbols =
        {
            "AAPL", "MSFT", "NVDA", "AMZN", "GOOGL", "META", "TSLA", "AMD",
            "NFLX", "JPM", "XOM", "BAC", "WFC", "INTC", "MU", "SPY", "QQQ", "IWM",
        };

        public RowDragView()
        {
            InitializeComponent();

            BuildRows();
            skiaGrid.ItemsSource = _rows;

            skiaGrid.RowDragStarting += OnRowDragStarting;
            skiaGrid.RowDropped += OnRowDropped;
            skiaGrid.RowDragCompleted += OnRowDragCompleted;

            _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _tickTimer.Tick += (_, _) => Tick();

            Loaded += (_, _) => UpdateStatus();
            Unloaded += (_, _) =>
            {
                _tickTimer.Stop();
                _dropTarget?.Close();
            };
        }

        private void BuildRows()
        {
            _rows.Clear();
            for (int i = 0; i < Symbols.Length; i++)
            {
                _rows.Add(new DragRow
                {
                    Rank = i + 1,
                    Symbol = Symbols[i],
                    Last = Math.Round(20 + _random.NextDouble() * 400, 2),
                    ChangePercent = Math.Round(_random.NextDouble() * 6 - 3, 2),
                    Volume = _random.Next(50_000, 9_000_000),
                });
            }
        }

        // The # column is the visible proof that the SOURCE order changed, not just the view.
        private void Renumber()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Rank = i + 1;
        }

        // ── drag events ─────────────────────────────────────────────────────────

        private void OnRowDragStarting(object? sender, SKRowDragStartingEventArgs e)
        {
            if (VetoDrag.IsChecked == true)
            {
                e.Cancel = true;
                Log($"RowDragStarting  row={e.RowIndex} items={e.Items.Count}  → CANCELLED");
                return;
            }
            Log($"RowDragStarting  row={e.RowIndex} items={e.Items.Count} ({Describe(e.Items)})" +
                (e.Column != null ? $"  cell=[{e.Column.Header}]={e.CellText}" : ""));
        }

        private void OnRowDropped(object? sender, SKRowDroppedEventArgs e)
        {
            var target = e.TargetItem is DragRow r ? r.Symbol : "<end>";
            Log($"RowDropped       gap={e.InsertIndex} above={target} items={e.Items.Count}");

            if (HandleDrop.IsChecked == true)
            {
                // Shows off Handled: take over from the built-in move entirely.
                e.Handled = true;
                foreach (var item in e.Items.OfType<DragRow>().ToList())
                {
                    _rows.Remove(item);
                    _rows.Add(item);
                }
                Log("                 → Handled: appended to the end instead");
            }

            // The built-in Reorder move runs after this handler returns, so renumber on the next
            // dispatcher pass rather than reading a half-applied order here.
            Dispatcher.BeginInvoke(new Action(() => { Renumber(); UpdateStatus(); }),
                                   DispatcherPriority.Background);
        }

        private void OnRowDragCompleted(object? sender, SKRowDragCompletedEventArgs e)
        {
            // DragOut only. Effects is whatever the drop target applied — None means nobody took it.
            // The grid deliberately does NOT remove rows on Move; that is this handler's call.
            Log($"RowDragCompleted effects={e.Effects} items={e.Items.Count} cell={e.CellText ?? "-"}");
        }

        private void OpenTarget_Click(object sender, RoutedEventArgs e)
        {
            if (_dropTarget == null || !_dropTarget.IsLoaded)
            {
                _dropTarget = new DropTargetWindow { Owner = Window.GetWindow(this) };
                _dropTarget.Closed += (_, _) => _dropTarget = null;
                // Off to the side of the main window so both are visible for the drag.
                var owner = Window.GetWindow(this);
                if (owner != null)
                {
                    _dropTarget.Left = Math.Max(0, owner.Left + owner.Width - _dropTarget.Width - 40);
                    _dropTarget.Top = owner.Top + 120;
                }
                _dropTarget.Show();
            }
            else
            {
                _dropTarget.Activate();
            }
        }

        private static string Describe(IReadOnlyList<object> items)
            => string.Join(",", items.OfType<DragRow>().Take(4).Select(r => r.Symbol))
             + (items.Count > 4 ? ",…" : "");

        private void Log(string line)
        {
            _log.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}  {line}");
            if (_log.Count > 16) _log.RemoveAt(_log.Count - 1);
            LogText.Text = string.Join(Environment.NewLine, _log);
        }

        // ── controls ────────────────────────────────────────────────────────────

        private void ModeBox_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (skiaGrid == null) return;
            skiaGrid.RowDragMode = SelectedText(ModeBox) switch
            {
                "DragOut" => SKRowDragMode.DragOut,
                "Reorder" => SKRowDragMode.Reorder,
                "Notify" => SKRowDragMode.Notify,
                _ => SKRowDragMode.None,
            };
            UpdateStatus();
        }

        private void ColorBox_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (skiaGrid == null) return;
            var choice = SelectedText(ColorBox);
            skiaGrid.RowDragIndicatorColor = choice == "default" ? null : choice;
        }

        private void SortOn_Changed(object sender, RoutedEventArgs e)
        {
            if (skiaGrid?.Columns == null) return;

            foreach (var col in skiaGrid.Columns)
                col.GridViewColumnSort = SkGridViewColumnSort.None;

            if (SortOn.IsChecked == true)
            {
                var sym = skiaGrid.Columns.FirstOrDefault(c => c.BindingPath == nameof(DragRow.Symbol));
                if (sym != null) sym.GridViewColumnSort = SkGridViewColumnSort.Ascending;
            }
            UpdateStatus();
        }

        private void LiveTicks_Changed(object sender, RoutedEventArgs e)
        {
            if (LiveTicks.IsChecked == true) _tickTimer.Start();
            else _tickTimer.Stop();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            BuildRows();
            _log.Clear();
            LogText.Text = string.Empty;
            skiaGrid.RefreshCollection();
            UpdateStatus();
        }

        private void Tick()
        {
            foreach (var row in _rows)
            {
                row.Last = Math.Round(Math.Max(1, row.Last + (_random.NextDouble() - 0.5) * 1.5), 2);
                row.ChangePercent = Math.Round(row.ChangePercent + (_random.NextDouble() - 0.5) * 0.2, 2);
                row.Volume += _random.Next(0, 25_000);
            }
            skiaGrid.Refresh();
        }

        private void UpdateStatus()
        {
            StatusText.Text =
                $"RowDragMode={skiaGrid.RowDragMode}   CanReorderRows={skiaGrid.CanReorderRows}   " +
                $"rows={_rows.Count}   first={_rows.FirstOrDefault()?.Symbol}";
        }

        private static string SelectedText(ComboBox box)
            => (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
    }

    public class DragRow : INotifyPropertyChanged
    {
        private int _rank;
        private double _last;
        private double _changePercent;
        private long _volume;

        public string Symbol { get; set; } = string.Empty;

        public int Rank
        {
            get => _rank;
            set { _rank = value; Raise(nameof(Rank)); }
        }

        public double Last
        {
            get => _last;
            set { _last = value; Raise(nameof(Last)); }
        }

        public double ChangePercent
        {
            get => _changePercent;
            set { _changePercent = value; Raise(nameof(ChangePercent)); }
        }

        public long Volume
        {
            get => _volume;
            set { _volume = value; Raise(nameof(Volume)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
