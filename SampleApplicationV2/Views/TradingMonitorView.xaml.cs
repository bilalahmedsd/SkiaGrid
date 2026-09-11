using SkiaSharpControlV2;
using SampleApplicationV2.Models;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    public partial class TradingMonitorView : UserControl
    {
        private readonly TradingMonitorViewModel _vm = new();
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _simTimer;

        public TradingMonitorView()
        {
            DataContext = _vm;
            InitializeComponent();

            foreach (var order in MarketDataSimulator.GenerateOrders(30))
                _vm.Orders.Add(order);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _refreshTimer.Tick += (s, e) => skiaGrid.Refresh();
            _refreshTimer.Start();

            _simTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _simTimer.Tick += (s, e) => MarketDataSimulator.SimulateOrderUpdates(_vm.Orders);
            _simTimer.Start();

            // ── ExpandChanged (tree mode): subscribe leg data only while the parent is expanded ──
            skiaGrid.ExpandChanged += OnExpandChanged;
        }

        /// <summary>Stands in for a leg-level market-data subscription set.</summary>
        private readonly HashSet<object> _subscribedLegs = new();

        private void OnExpandChanged(object? sender, SkExpandChangedEventArgs e)
        {
            foreach (var leg in e.AffectedItems)
            {
                if (e.IsExpanded) _subscribedLegs.Add(leg);
                else _subscribedLegs.Remove(leg);
            }

            var parent = e.ParentItem is OrderRecord o ? o.OrderId : (e.IsBulk ? "ALL" : "?");
            txtLegSubs.Text =
                $"Leg subs: {_subscribedLegs.Count}  (last: {(e.IsExpanded ? "+" : "-")}{e.AffectedItems.Count} {parent}, {e.Reason})";
        }

        private void ExpandAllLegs_Click(object sender, RoutedEventArgs e) => skiaGrid.ExpandAllRows();
        private void CollapseAllLegs_Click(object sender, RoutedEventArgs e) => skiaGrid.CollapseAllRows();

        private void AddOrder_Click(object sender, RoutedEventArgs e)
        {
            var orders = MarketDataSimulator.GenerateOrders(1);
            orders[0].Status = "Open";
            orders[0].Filled = 0;
            orders[0].Remaining = orders[0].Quantity;
            orders[0].Time = DateTime.Now;
            _vm.Orders.Insert(0, orders[0]);
        }

        private void AddSpread_Click(object sender, RoutedEventArgs e)
        {
            // Generate a spread order with legs — uses ITreeItem for expand/collapse
            var orders = MarketDataSimulator.GenerateOrders(5);
            // Find the first spread order from the batch
            var spread = orders.FirstOrDefault(o => o.IsSpread);
            if (spread != null)
            {
                spread.Status = "Open";
                spread.Time = DateTime.Now;
                _vm.Orders.Insert(0, spread);
            }
        }

        private void SimulateFills_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 5; i++)
                MarketDataSimulator.SimulateOrderUpdates(_vm.Orders);
        }

        private void FilterOpen_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.AddListFilter("Status", new List<string> { "Open", "Part Fill" });
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.RemoveFilter("Status");
        }

        private void LiveSort_Changed(object sender, RoutedEventArgs e)
        {
            _vm.IsLiveSort = chkLiveSort.IsChecked == true;
        }

        private void SelectionStyle_Changed(object sender, RoutedEventArgs e)
        {
            skiaGrid.SelectionStyle = chkBorderSelect.IsChecked == true
                ? SKSelectionStyle.Border : SKSelectionStyle.Fill;
        }
    }

    public class TradingMonitorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<OrderRecord> Orders { get; set; } = new();
        public ObservableCollection<object> SelectedItems { get; set; } = new();

        private bool _isLiveSort = true;
        public bool IsLiveSort
        {
            get => _isLiveSort;
            set { _isLiveSort = value; OnPropertyChanged(nameof(IsLiveSort)); }
        }

        private string _lastAction = "";
        public string LastAction
        {
            get => _lastAction;
            set { _lastAction = value; OnPropertyChanged(nameof(LastAction)); }
        }

        // ICommand demos
        public ICommand RowClickedCommand => new RelayCommand<object>(item =>
        {
            if (item is Models.OrderRecord order)
                LastAction = $"Row clicked: {order.OrderId} {order.Symbol}";
        });

        public ICommand RowDoubleClickedCommand => new RelayCommand<object>(item =>
        {
            if (item is Models.OrderRecord order)
                System.Windows.MessageBox.Show($"Open detail for {order.OrderId} — {order.Symbol} {order.Side} {order.Quantity}@{order.Price:N2}",
                    "Order Detail", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        });

        // ── ICommand demos for SkButton ────────────────────────────

        public ICommand CancelOrderCommand => new RelayCommand<object>(item =>
        {
            if (item is Models.OrderRecord order && order.Status != "Cancelled" && order.Status != "Filled")
            {
                order.Status = "Cancelled";
                order.Remaining = 0;
                LastAction = $"Cancelled: {order.OrderId}";
            }
        });

        public ICommand FillOrderCommand => new RelayCommand<object>(item =>
        {
            if (item is Models.OrderRecord order && (order.Status == "Open" || order.Status == "Part Fill"))
            {
                order.Status = "Filled";
                order.Filled = order.Quantity;
                order.Remaining = 0;
                LastAction = $"Filled: {order.OrderId}";
            }
        });

        // Dynamic button factory — returns different buttons per row based on order status
        public Func<object, List<SkiaSharpControlV2.SkButton>> ButtonFactory => (object data) =>
        {
            if (data is not Models.OrderRecord order) return null!;

            var buttons = new List<SkiaSharpControlV2.SkButton>();

            if (order.Status == "Open" || order.Status == "Part Fill")
            {
                // Open orders: show Fill + Cancel buttons
                buttons.Add(new SkiaSharpControlV2.SkButton
                {
                    Name = "FillBtn", Text = "✓", Width = 25,
                    ForegroundColor = "#00CC00", BackgroundColor = "#0A2A0A",
                    Command = FillOrderCommand
                });
                buttons.Add(new SkiaSharpControlV2.SkButton
                {
                    Name = "CancelBtn", Text = "✕", Width = 25, MarginLeft = 3,
                    ForegroundColor = "#FF4444", BackgroundColor = "#1A0A0A",
                    Command = CancelOrderCommand
                });
            }
            else if (order.Status == "Filled")
            {
                // Filled: show info only
                buttons.Add(new SkiaSharpControlV2.SkButton
                {
                    Name = "DoneBtn", Text = "●", Width = 25,
                    ForegroundColor = "#00CC00", BackgroundColor = "#0A2A0A",
                });
            }
            // Cancelled/Rejected: no buttons

            return buttons;
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
