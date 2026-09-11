using SkiaSharpControlV2;
using SampleApplicationV2.Models;


using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SampleApplicationV2.Views
{
    public partial class GlobalPositionSummaryView : UserControl
    {
        private readonly GlobalPositionViewModel _vm = new();
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _dataTimer;

        public GlobalPositionSummaryView()
        {
            DataContext = _vm;
            InitializeComponent();

            foreach (var pos in MarketDataSimulator.GeneratePositions(30))
                _vm.Positions.Add(pos);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _refreshTimer.Tick += (s, e) => skiaGrid.Refresh();
            _refreshTimer.Start();

            _dataTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _dataTimer.Tick += (s, e) => MarketDataSimulator.UpdatePositionPrices(_vm.Positions);
            _dataTimer.Start();
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e) => skiaGrid.ExpandAll();
        private void CollapseAll_Click(object sender, RoutedEventArgs e) => skiaGrid.CollapseAll();

        private void UpdatePrices_Click(object sender, RoutedEventArgs e)
        {
            MarketDataSimulator.UpdatePositionPrices(_vm.Positions);
        }

        private void FilterAccount_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.AddOrUpdateFilter(new Filter
            {
                Column = "Account",
                FilterType = FilterType.List,
                List = new List<string> { "ACCT-001" }
            });
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            skiaGrid.RemoveFilter(new Filter { Column = "Account" });
        }

        private void TimerSort_Changed(object sender, RoutedEventArgs e)
        {
            _vm.SortEvery = chkTimerSort.IsChecked == true ? 3 : 0;
        }
    }

    public class GlobalPositionViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<PositionItem> Positions { get; set; } = new();
        public ObservableCollection<object> SelectedItems { get; set; } = new();

        private int _sortEvery;
        public int SortEvery
        {
            get => _sortEvery;
            set { _sortEvery = value; OnPropertyChanged(nameof(SortEvery)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
