using SkiaSharpControlV2;
using SampleApplicationV2.Models;


using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    public partial class TimeAndSaleView : UserControl
    {
        private readonly TimeAndSaleViewModel _vm = new();
        private DispatcherTimer? _dataTimer;
        private DispatcherTimer? _refreshTimer;
        private bool _isPaused;
        private bool _initialized;

        public TimeAndSaleView()
        {
            DataContext = _vm;
            InitializeComponent();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _refreshTimer.Tick += (s, e) => skiaGrid.Refresh();
            _refreshTimer.Start();

            _dataTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _dataTimer.Tick += (s, e) => AddTrades();
            _dataTimer.Start();

            _initialized = true;

            // The XAML default selection is made before _initialized flips, and the grid's
            // CollectionView doesn't exist until it loads — so apply the starting symbol filter here.
            Loaded += (s, e) => ApplySymbolFilter();
        }

        private void AddTrades()
        {
            for (int i = 0; i < 3; i++)
            {
                var trade = MarketDataSimulator.GenerateTrade();

                if (_vm.Trades.Count >= 500)
                    _vm.Trades.RemoveAt(_vm.Trades.Count - 1);

                _vm.Trades.Insert(0, trade);
            }
            TradeCountText.Text = $"Trades: {_vm.Trades.Count}";
        }

        private void SymbolFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            ApplySymbolFilter();
        }

        /// <summary>
        /// Filter the tape to the selected symbol (or clear it for "ALL"). Filtering is by data
        /// property, not by column, so this still works even though the tape shows no Symbol column.
        /// </summary>
        private void ApplySymbolFilter()
        {
            if (SymbolFilter.SelectedItem is not ComboBoxItem item) return;

            var text = item.Content?.ToString();
            if (text == "ALL")
            {
                skiaGrid.RemoveFilter(new Filter { Column = "Symbol" });
            }
            else
            {
                skiaGrid.AddOrUpdateFilter(new Filter
                {
                    Column = "Symbol",
                    FilterType = FilterType.List,
                    List = new List<string> { text! }
                });
            }
        }

        private void Speed_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_dataTimer != null && SpeedSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                _dataTimer.Interval = TimeSpan.FromMilliseconds(int.Parse(tag));
        }

        private void PauseResume_Click(object sender, RoutedEventArgs e)
        {
            if (_dataTimer == null) return;
            _isPaused = !_isPaused;
            if (_isPaused)
                _dataTimer.Stop();
            else
                _dataTimer.Start();
        }
    }

    public class TimeAndSaleViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<TradeRecord> Trades { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
