using SkiaSharpControlV2;
using SampleApplicationV2.Models;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    public partial class WatchlistView : UserControl
    {
        private readonly WatchlistViewModel _vm = new();
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _dataTimer;

        public WatchlistView()
        {
            DataContext = _vm;
            InitializeComponent();

            foreach (var q in MarketDataSimulator.GenerateWatchlist())
                _vm.Quotes.Add(q);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _refreshTimer.Tick += (s, e) => skiaGrid.Refresh();
            _refreshTimer.Start();

            _dataTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _dataTimer.Tick += (s, e) => MarketDataSimulator.UpdateQuotes(_vm.Quotes);
            _dataTimer.Start();
        }

        private void FilterSector_Click(object sender, RoutedEventArgs e)
            => skiaGrid.AddListFilter("Sector", new List<string> { "Technology" });

        private void SymbolTextFilter_Click(object sender, RoutedEventArgs e)
        {
            var pattern = txtSymbolFilter.Text?.Trim();
            if (string.IsNullOrEmpty(pattern))
                skiaGrid.RemoveFilter("Symbol");
            else
                skiaGrid.AddTextFilter("Symbol", pattern);
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.RemoveFilter("Sector");
            skiaGrid.RemoveFilter("Symbol");
            if (txtSymbolFilter != null) txtSymbolFilter.Text = "";
        }

        private void TimerSort_Changed(object sender, RoutedEventArgs e)
            => _vm.SortEvery = chkTimerSort.IsChecked == true ? 5 : 0;

        private void LiveSort_Changed(object sender, RoutedEventArgs e)
            => _vm.IsLiveSort = chkLiveSort.IsChecked == true;

        private void SelectionStyle_Changed(object sender, RoutedEventArgs e)
        {
            skiaGrid.SelectionStyle = chkBorderSelect.IsChecked == true
                ? SKSelectionStyle.Border : SKSelectionStyle.Fill;
        }
    }

    public class WatchlistViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<QuoteItem> Quotes { get; set; } = new();
        public ObservableCollection<object> SelectedItems { get; set; } = new();

        private bool _isLiveSort = true;
        public bool IsLiveSort
        {
            get => _isLiveSort;
            set { _isLiveSort = value; OnPropertyChanged(nameof(IsLiveSort)); }
        }

        private int _sortEvery;
        public int SortEvery
        {
            get => _sortEvery;
            set { _sortEvery = value; OnPropertyChanged(nameof(SortEvery)); }
        }

        private bool _isRowHighlight;
        public bool IsRowHighlight
        {
            get => _isRowHighlight;
            set { _isRowHighlight = value; OnPropertyChanged(nameof(IsRowHighlight)); }
        }

        private bool _isTimerHighlight = true;
        public bool IsTimerHighlight
        {
            get => _isTimerHighlight;
            set { _isTimerHighlight = value; OnPropertyChanged(nameof(IsTimerHighlight)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
